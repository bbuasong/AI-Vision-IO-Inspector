using System;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD SDK 등록 핸들을 애플리케이션 전체에서 공유하기 위한 세션 객체입니다.
    /// 원본 VLAD_Ops의 Vlad_id 전역 상태를 WPF/MVVM 구조에 맞게 명시적으로 보관합니다.
    /// </summary>
    public class VladSdkSession
    {
        private readonly object _syncRoot = new object();
        private IntPtr _vladId;
        private bool _isWarmedUp;

        public IntPtr CurrentVladId
        {
            get { return _vladId; }
        }

        public bool IsWarmedUp
        {
            get { return _isWarmedUp; }
        }

        public IntPtr EnsureStarted(int user, string rootName, string siteName, int messageVersion, int majorVersion, string modelPath, int gpuId)
        {
            lock (_syncRoot)
            {
                if (_vladId != IntPtr.Zero)
                {
                    return _vladId;
                }

                try
                {
                    _vladId = VLAD_Ops_Ai.VLAD_Ops_Ai_Env_Start(user, rootName, siteName, messageVersion, majorVersion, modelPath, gpuId);
                }
                catch (Exception ex)
                {
                    _vladId = IntPtr.Zero;
                    throw new InvalidOperationException("VLAD SDK 초기화 호출 중 오류가 발생했습니다. " + ex.Message, ex);
                }

                if (_vladId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD SDK 초기화 실패: vladId가 비어 있습니다.");
                }

                return _vladId;
            }
        }

        public bool TryWarmUp()
        {
            lock (_syncRoot)
            {
                if (_vladId == IntPtr.Zero)
                {
                    return false;
                }

                _isWarmedUp = VLAD_Ops_Ai.VLAD_Warm_Up(_vladId);
                return _isWarmedUp;
            }
        }

        public bool Unregister()
        {
            lock (_syncRoot)
            {
                if (_vladId == IntPtr.Zero)
                {
                    return true;
                }

                bool result = VLAD_Ops_Ai.VLAD_Unregistration(_vladId);
                if (result)
                {
                    _vladId = IntPtr.Zero;
                    _isWarmedUp = false;
                }

                return result;
            }
        }
    }
}
