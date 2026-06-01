using System;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD_Ops_Ai.cs를 보던 담당자가 같은 함수명으로 현재 구조를 찾을 수 있게 하는 호환 계층입니다.
    /// 실제 VLAD DLL 호출은 VladRuntimeContext 안에 구현하고, 앱은 VisionAiInferenceService를 통해서만 호출합니다.
    /// </summary>
    public static class VLAD_Ops_Ai_Compat
    {
        private static readonly object SyncRoot = new object();
        private static VladFunctionAdapter _adapter;
        private static IntPtr _vladId;

        /// <summary>
        /// 기존 VLAD_Ops_Ai_Env_Start 흐름입니다.
        /// 기존 코드 기준으로 VLAD_Registration 후 VLAD_Ops_Inference_Registration을 수행합니다.
        /// </summary>
        public static IntPtr VLAD_Ops_Ai_Env_Start(
            int user,
            string rootName,
            string siteName,
            int messageVersion,
            int majorVersion,
            string modelPath,
            int gpuId)
        {
            lock (SyncRoot)
            {
                EnsureAdapter();

                _vladId = _adapter.VLAD_Registration(user, messageVersion, majorVersion);
                if (string.IsNullOrWhiteSpace(modelPath) == false)
                {
                    _adapter.VLAD_Ops_Inference_Registration(rootName, siteName, modelPath, "{}", gpuId);
                }

                return _vladId;
            }
        }

        /// <summary>
        /// 기존 코드의 VLAD_Inference_Mat 호출 지점을 현재 표준 입력/출력 모델에 연결합니다.
        /// </summary>
        public static VisionInspectionOutput VLAD_Ops_Ai_Inference_Mat(
            VisionInspectionInput input,
            float threshold,
            int drawMode)
        {
            lock (SyncRoot)
            {
                EnsureAdapter();
                return _adapter.VLAD_Inference_Mat(input, threshold, drawMode);
            }
        }

        /// <summary>
        /// 기존 detect_data 유효 객체 개수 확인 함수명입니다.
        /// </summary>
        public static int VLAD_InferenceData_Get_Valid_Count(IntPtr detectData)
        {
            lock (SyncRoot)
            {
                EnsureAdapter();
                return _adapter.VLAD_InferenceData_Get_Valid_Count(detectData);
            }
        }

        private static void EnsureAdapter()
        {
            if (_adapter == null)
            {
                _adapter = new VladFunctionAdapter();
            }
        }
    }
}
