using System;
using System.Threading;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 전체 이미지와 Crop 이미지용 VLAD SDK 등록 핸들을 애플리케이션 전체에서 공유합니다.
    /// 두 핸들은 같은 수명주기로 생성/해제하며, RTSP callback은 전체 이미지 핸들에만 등록합니다.
    /// </summary>
    public class VladSdkSession
    {
        private const string RegistrationMutexName = @"Local\AI.Vision.IOInspector.VLAD.CustomRegistration";
        private const int RegistrationMutexTimeoutMilliseconds = 180000;

        private readonly object _syncRoot = new object();
        private IntPtr _fullImageVladId;
        private IntPtr _croppedImageVladId;
        private bool _usesSeparateNativeRegistrations;
        private bool _isWarmedUp;

        /// <summary>
        /// 전체 이미지 추론과 RTSP callback 등록에 사용하는 VLAD ID입니다.
        /// </summary>
        public IntPtr CurrentFullImageVladId
        {
            get { return _fullImageVladId; }
        }

        /// <summary>
        /// Crop 이미지 추론에 사용하는 VLAD ID입니다. RTSP callback은 중복 등록하지 않습니다.
        /// </summary>
        public IntPtr CurrentCroppedImageVladId
        {
            get { return _croppedImageVladId; }
        }

        /// <summary>
        /// 기존 단일 ID 참조를 위한 호환 속성입니다. 새 코드에서는 CurrentFullImageVladId를 사용합니다.
        /// </summary>
        public IntPtr CurrentVladId
        {
            get { return _fullImageVladId; }
        }

        public bool IsWarmedUp
        {
            get { return _isWarmedUp; }
        }

        public VladRuntimeIds EnsureStarted(
            int user,
            string rootName,
            string siteName,
            int messageVersion,
            int majorVersion,
            string fullImageModelPath,
            string croppedImageModelPath,
            int gpuId,
            bool useSeparateVladRegistration)
        {
            lock (_syncRoot)
            {
                if (_fullImageVladId != IntPtr.Zero && _croppedImageVladId != IntPtr.Zero)
                {
                    return new VladRuntimeIds(
                        _fullImageVladId,
                        _croppedImageVladId,
                        _usesSeparateNativeRegistrations);
                }

                // 이전 초기화가 중간에 실패해 ID 하나만 남은 경우에는 재사용하지 않습니다.
                if (_fullImageVladId != IntPtr.Zero || _croppedImageVladId != IntPtr.Zero)
                {
                    if (!UnregisterCore())
                    {
                        throw new InvalidOperationException("이전 VLAD 등록 핸들을 정리하지 못해 두 ID 초기화를 시작할 수 없습니다.");
                    }
                }

                try
                {
                    // VLAD_Custom_Registration은 TensorFlow/GPU 전역 상태를 초기화하므로
                    // 같은 PC에서 다른 프로세스와 동시에 진입하지 않도록 OS Mutex로 보호합니다.
                    using (VladRegistrationMutex registrationMutex = VladRegistrationMutex.Acquire())
                    {
                        // 전체 이미지 ID만 RTSP callback을 등록합니다. 현재 VLAD_Ops_RTSP는 활성 VladId 하나의 프레임 캐시만 관리합니다.
                        //
                        // RTSP 등록 경로가 두 곳입니다. 여기(Env_Start의 첫 채널)와
                        // VisionCameraCoordinator의 채널 일괄 등록입니다.
                        // EnableRtspCallbackRegistration=false로 회수하려면 두 곳이 모두 꺼져야 하므로
                        // 여기서도 같은 설정을 따릅니다. 한쪽만 막으면 연결이 계속 남습니다.
                        bool bRegisterRtsp = VladRuntimeSettings.Load().EnableRtspCallbackRegistration;
                        _fullImageVladId = VLAD_Ops_Ai.VLAD_Ops_Ai_Env_Start(
                            user,
                            rootName,
                            siteName,
                            messageVersion,
                            majorVersion,
                            fullImageModelPath,
                            gpuId,
                            bRegisterRtsp);

                        if (_fullImageVladId == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("전체 이미지용 VLAD SDK 초기화 실패: VladId가 비어 있습니다.");
                        }

                        if (useSeparateVladRegistration)
                        {
                            // AI 담당자가 이중 등록을 지원한다고 확인한 DLL에서만 사용합니다.
                            // Crop 이미지 ID는 RTSP callback을 중복 등록하지 않습니다.
                            _croppedImageVladId = VLAD_Ops_Ai.VLAD_Ops_Ai_Env_Start(
                                user,
                                rootName,
                                siteName,
                                messageVersion,
                                majorVersion,
                                croppedImageModelPath,
                                gpuId,
                                false);
                            _usesSeparateNativeRegistrations = true;
                        }
                        else
                        {
                            // 현재 VLAD_SDK.dll은 두 번째 VLAD_Custom_Registration에서 네이티브 힙이 손상됩니다.
                            // 상위 API의 두 ID 인자 형태는 유지하되, 현재 DLL에서는 동일한 하나의 등록 ID를 두 슬롯에 전달합니다.
                            _croppedImageVladId = _fullImageVladId;
                            _usesSeparateNativeRegistrations = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnregisterCore();
                    throw new InvalidOperationException("VLAD SDK 초기화 호출 중 오류가 발생했습니다. " + ex.Message, ex);
                }

                if (_croppedImageVladId == IntPtr.Zero)
                {
                    UnregisterCore();
                    throw new InvalidOperationException("Crop 이미지용 VLAD SDK 초기화 실패: VladId가 비어 있습니다.");
                }

                return new VladRuntimeIds(
                    _fullImageVladId,
                    _croppedImageVladId,
                    _usesSeparateNativeRegistrations);
            }
        }

        public bool TryWarmUp()
        {
            lock (_syncRoot)
            {
                if (_fullImageVladId == IntPtr.Zero || _croppedImageVladId == IntPtr.Zero)
                {
                    return false;
                }

                bool fullImageWarmedUp = VLAD_Ops_Ai.VLAD_Warm_Up(_fullImageVladId);
                bool croppedImageWarmedUp = _usesSeparateNativeRegistrations
                    ? VLAD_Ops_Ai.VLAD_Warm_Up(_croppedImageVladId)
                    : fullImageWarmedUp;
                _isWarmedUp = fullImageWarmedUp && croppedImageWarmedUp;
                return _isWarmedUp;
            }
        }

        public bool Unregister()
        {
            lock (_syncRoot)
            {
                return UnregisterCore();
            }
        }

        /// <summary>
        /// 별도 등록인 경우에는 Crop ID를 먼저 해제해 전체 이미지 RTSP 세션을 마지막까지 유지합니다.
        /// 단일 등록 호환 모드에서는 같은 포인터를 두 번 해제하지 않고 한 번만 해제합니다.
        /// </summary>
        private bool UnregisterCore()
        {
            bool croppedImageResult = true;
            bool fullImageResult = true;

            if (_usesSeparateNativeRegistrations && _croppedImageVladId != IntPtr.Zero)
            {
                croppedImageResult = VLAD_Ops_Ai.VLAD_Unregistration(_croppedImageVladId);
                if (croppedImageResult)
                {
                    _croppedImageVladId = IntPtr.Zero;
                }
            }

            if (_fullImageVladId != IntPtr.Zero)
            {
                fullImageResult = VLAD_Ops_Ai.VLAD_Unregistration(_fullImageVladId);
                if (fullImageResult)
                {
                    _fullImageVladId = IntPtr.Zero;
                    _croppedImageVladId = IntPtr.Zero;
                }
            }

            if (croppedImageResult && fullImageResult)
            {
                _isWarmedUp = false;
                _usesSeparateNativeRegistrations = false;
            }

            return croppedImageResult && fullImageResult;
        }

        private sealed class VladRegistrationMutex : IDisposable
        {
            private readonly Mutex _mutex;
            private readonly bool _ownsMutex;

            private VladRegistrationMutex(Mutex mutex, bool ownsMutex)
            {
                _mutex = mutex;
                _ownsMutex = ownsMutex;
            }

            public static VladRegistrationMutex Acquire()
            {
                Mutex mutex = new Mutex(false, RegistrationMutexName);
                bool ownsMutex = false;
                try
                {
                    try
                    {
                        ownsMutex = mutex.WaitOne(RegistrationMutexTimeoutMilliseconds);
                    }
                    catch (AbandonedMutexException)
                    {
                        ownsMutex = true;
                    }

                    if (!ownsMutex)
                    {
                        throw new TimeoutException(
                            "다른 프로세스가 VLAD_Custom_Registration을 실행 중이어서 " +
                            (RegistrationMutexTimeoutMilliseconds / 1000).ToString() +
                            "초 안에 VLAD 등록 잠금을 얻지 못했습니다.");
                    }

                    return new VladRegistrationMutex(mutex, ownsMutex);
                }
                catch
                {
                    mutex.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                if (_ownsMutex)
                {
                    _mutex.ReleaseMutex();
                }

                _mutex.Dispose();
            }
        }
    }

    /// <summary>
    /// 전체 이미지와 Crop 이미지용 VLAD 등록 핸들을 함께 전달합니다.
    /// 두 IntPtr은 같은 프로세스에서만 유효하며 외부 학습 프로세스에 전달하거나 저장하면 안 됩니다.
    /// </summary>
    public sealed class VladRuntimeIds
    {
        public VladRuntimeIds(IntPtr fullImageVladId, IntPtr croppedImageVladId, bool usesSeparateNativeRegistrations)
        {
            FullImageVladId = fullImageVladId;
            CroppedImageVladId = croppedImageVladId;
            UsesSeparateNativeRegistrations = usesSeparateNativeRegistrations;
        }

        public IntPtr FullImageVladId { get; private set; }

        public IntPtr CroppedImageVladId { get; private set; }

        /// <summary>
        /// true이면 네이티브 등록을 두 번 수행해 서로 다른 두 ID를 사용합니다.
        /// false이면 현재 SDK 호환을 위해 하나의 등록 ID를 두 입력 슬롯에 함께 사용합니다.
        /// </summary>
        public bool UsesSeparateNativeRegistrations { get; private set; }
    }
}
