using System;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD_Ops의 ACTION_MODE -> Kind_Load -> Env_Start 흐름 중
    /// 현재 프로젝트에서 사용하는 CAM 모드 초기화만 명시적으로 옮긴 런타임 계층입니다.
    /// </summary>
    public class VladCamModeRuntime
    {
        private readonly object _syncRoot;
        private readonly VladSdkSession _vladSdkSession;
        private readonly VladVisionSettings _settings;
        private VladCamModeState _state;

        public VladCamModeRuntime(VladSdkSession vladSdkSession, VladVisionSettings settings)
        {
            _syncRoot = new object();
            _vladSdkSession = vladSdkSession ?? throw new ArgumentNullException(nameof(vladSdkSession));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public VladCamModeState CurrentState
        {
            get
            {
                lock (_syncRoot)
                {
                    return _state;
                }
            }
        }

        public VladVisionSettings Settings
        {
            get { return _settings; }
        }

        public VladCamModeState EnsureLoaded()
        {
            lock (_syncRoot)
            {
                if (_state != null &&
                    _state.FullImageVladId != IntPtr.Zero &&
                    _state.CroppedImageVladId != IntPtr.Zero)
                {
                    return _state;
                }

                int actionMode = VLAD_Ops_Mode.MODE_TYPE_CAM;
                string rootName = VLAD_Ops_Mode.GetRootName(actionMode);
                string userName = string.IsNullOrWhiteSpace(_settings.SiteName) ? "HD" : _settings.SiteName;
                VladRuntimeSettings runtimeSettings = VladRuntimeSettings.Load();

                // 결과 JSON 테스트는 네이티브 DLL, GPU, RTSP 연결을 호출하지 않고
                // "AI 결과 수신 이후"의 관리 코드 흐름만 검증하는 전용 모드입니다.
                VLAD_Ops_Ai.SetTestResultJsonEnabled(runtimeSettings.UseTestResultJson);
                if (runtimeSettings.UseTestResultJson)
                {
                    _state = new VladCamModeState(
                        actionMode,
                        rootName,
                        userName,
                        new IntPtr(1),
                        new IntPtr(2),
                        0,
                        _settings.Threshold,
                        false);
                    return _state;
                }

                // Sample_VLAD_SDK는 VLAD_Custom_Registration 전에 TensorFlow/CUDA 환경변수를 별도로 세팅하지 않습니다.
                // 샘플과 동일한 초기화 조건을 유지하기 위해 여기서는 사전 환경변수 변경을 하지 않습니다.

                VladRuntimeIds vladIds = _vladSdkSession.EnsureStarted(
                    (int)SDK_USER.USER_CUS_STD,
                    rootName,
                    userName,
                    (int)SDK_MSG.MSG_V1,
                    (int)SDK_MAJ.MAJ_V1,
                    _settings.FullImageModelPath,
                    _settings.CroppedModelPath,
                    _settings.GpuId,
                    runtimeSettings.UseSeparateVladRegistration);

                int classCount = VLAD_Ops_Ai.VLAD_Get_Class_Count(vladIds.FullImageVladId);
                _state = new VladCamModeState(
                    actionMode,
                    rootName,
                    userName,
                    vladIds.FullImageVladId,
                    vladIds.CroppedImageVladId,
                    classCount,
                    _settings.Threshold,
                    vladIds.UsesSeparateNativeRegistrations);

                return _state;
            }
        }

        /// <summary>
        /// 학습 프로그램이 새 모델 파일 쓰기를 완료한 뒤 기존 VladId를 해제하고 같은 설정으로 다시 등록합니다.
        /// VLAD_Unregistration이 실패하면 기존 상태를 임의로 버리지 않고 오류를 호출자에게 전달합니다.
        /// </summary>
        public VladCamModeState Reload()
        {
            lock (_syncRoot)
            {
                if (!_vladSdkSession.Unregister())
                {
                    throw new InvalidOperationException("VLAD_Unregistration이 실패하여 새 모델 초기화를 진행하지 못했습니다.");
                }

                _state = null;
                return EnsureLoaded();
            }
        }

    }

    public class VladCamModeState
    {
        public VladCamModeState(
            int actionMode,
            string rootName,
            string userName,
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            int classCount,
            float threshold,
            bool usesSeparateNativeRegistrations)
        {
            ActionMode = actionMode;
            RootName = rootName;
            UserName = userName;
            FullImageVladId = fullImageVladId;
            CroppedImageVladId = croppedImageVladId;
            ClassCount = classCount;
            Threshold = threshold;
            UsesSeparateNativeRegistrations = usesSeparateNativeRegistrations;
        }

        public int ActionMode { get; private set; }

        public string RootName { get; private set; }

        public string UserName { get; private set; }

        /// <summary>
        /// 전체 이미지 추론 및 RTSP callback 등록에 사용하는 VLAD ID입니다.
        /// </summary>
        public IntPtr FullImageVladId { get; private set; }

        /// <summary>
        /// Crop 이미지 추론에 사용하는 VLAD ID입니다. RTSP callback은 등록하지 않습니다.
        /// </summary>
        public IntPtr CroppedImageVladId { get; private set; }

        /// <summary>
        /// 기존 단일 ID 참조 호환용 속성입니다. 새 코드에서는 FullImageVladId를 사용합니다.
        /// </summary>
        public IntPtr VladId
        {
            get { return FullImageVladId; }
        }

        public int ClassCount { get; private set; }

        public float Threshold { get; private set; }

        /// <summary>
        /// 현재 세션이 실제로 네이티브 등록 ID 두 개를 보유하는지 나타냅니다.
        /// false이면 Full/Crop 입력 API에는 같은 ID가 전달됩니다.
        /// </summary>
        public bool UsesSeparateNativeRegistrations { get; private set; }
    }
}
