using System;
using AI.Vision.IOInspector.Vision.LegacyVlad;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// 학습 완료 후 기존 VladId를 해제하고 새 모델로 VLAD 및 RTSP 채널을 다시 초기화합니다.
    /// 검사와 재초기화가 동시에 VLAD 네이티브 함수를 호출하지 않도록 공용 작업 잠금을 제공합니다.
    /// </summary>
    public sealed class VladRuntimeLifecycleService
    {
        private readonly object _operationSyncRoot;
        private readonly VladCamModeRuntime _camModeRuntime;
        private VisionCameraService _cameraService;

        public VladRuntimeLifecycleService(VladCamModeRuntime camModeRuntime)
        {
            _operationSyncRoot = new object();
            _camModeRuntime = camModeRuntime ?? throw new ArgumentNullException("camModeRuntime");
        }

        public object OperationSyncRoot
        {
            get { return _operationSyncRoot; }
        }

        public void AttachCameraService(VisionCameraService cameraService)
        {
            lock (_operationSyncRoot)
            {
                _cameraService = cameraService;
            }
        }

        /// <summary>
        /// DONE 수신 및 정상 프로세스 종료가 확인된 뒤 호출합니다.
        /// </summary>
        public VladCamModeState ReloadAfterTraining()
        {
            lock (_operationSyncRoot)
            {
                lock (VLAD_Ops_Ai.NativeInferenceSyncRoot)
                {
                    if (_cameraService != null)
                    {
                        _cameraService.PrepareForVladRuntimeReload();
                    }

                    VladCamModeState state = _camModeRuntime.Reload();
                    VLAD_Ops_Ai.ResetNativeInferenceBlock();

                    if (_cameraService != null)
                    {
                        _cameraService.ResumeAfterVladRuntimeReload();
                    }

                    return state;
                }
            }
        }
    }
}
