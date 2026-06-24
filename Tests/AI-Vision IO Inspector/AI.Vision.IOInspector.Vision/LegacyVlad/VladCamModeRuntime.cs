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
                if (_state != null && _state.VladId != IntPtr.Zero)
                {
                    return _state;
                }

                int actionMode = VLAD_Ops_Mode.MODE_TYPE_CAM;
                string rootName = VLAD_Ops_Mode.GetRootName(actionMode);
                string userName = string.IsNullOrWhiteSpace(_settings.SiteName) ? "HD" : _settings.SiteName;

                // Sample_VLAD_SDK는 VLAD_Custom_Registration 전에 TensorFlow/CUDA 환경변수를 별도로 세팅하지 않습니다.
                // 샘플과 동일한 초기화 조건을 유지하기 위해 여기서는 사전 환경변수 변경을 하지 않습니다.

                IntPtr vladId = _vladSdkSession.EnsureStarted(
                    (int)SDK_USER.USER_CUS_STD,
                    rootName,
                    userName,
                    (int)SDK_MSG.MSG_V1,
                    (int)SDK_MAJ.MAJ_V1,
                    _settings.ModelPath,
                    _settings.GpuId);

                int classCount = VLAD_Ops_Ai.VLAD_Get_Class_Count(vladId);
                _state = new VladCamModeState(
                    actionMode,
                    rootName,
                    userName,
                    vladId,
                    classCount,
                    _settings.Threshold);

                return _state;
            }
        }

        private static void ApplyVladTensorflowEnvironment(int gpuId)
        {
            // 기존 VLAD_Ops의 모델 로드 전 GPU 환경변수 설정 흐름을 유지합니다.
            Environment.SetEnvironmentVariable("TF_FORCE_GPU_ALLOW_GROWTH", "true");
            Environment.SetEnvironmentVariable("CUDA_DEVICE_ORDER", "PCI_BUS_ID");
            Environment.SetEnvironmentVariable("CUDA_VISIBLE_DEVICES", gpuId.ToString());
        }
    }

    public class VladCamModeState
    {
        public VladCamModeState(int actionMode, string rootName, string userName, IntPtr vladId, int classCount, float threshold)
        {
            ActionMode = actionMode;
            RootName = rootName;
            UserName = userName;
            VladId = vladId;
            ClassCount = classCount;
            Threshold = threshold;
        }

        public int ActionMode { get; private set; }

        public string RootName { get; private set; }

        public string UserName { get; private set; }

        public IntPtr VladId { get; private set; }

        public int ClassCount { get; private set; }

        public float Threshold { get; private set; }
    }
}
