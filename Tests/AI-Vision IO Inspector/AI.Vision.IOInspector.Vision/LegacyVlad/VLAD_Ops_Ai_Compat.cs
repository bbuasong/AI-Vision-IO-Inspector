using System;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD_Ops_Ai.cs를 보던 담당자가 같은 함수명으로 현재 구조를 찾을 수 있게 해주는 호환 계층입니다.
    /// 실제 DLL 호출은 VladRuntimeContext와 VladNativeMethods가 담당합니다.
    /// </summary>
    public static class VLAD_Ops_Ai_Compat
    {
        private static readonly object SyncRoot = new object();
        private static VladFunctionAdapter _adapter;
        private static IntPtr _vladId;

        public static IntPtr CurrentVladId
        {
            get { return _vladId; }
        }

        /// <summary>
        /// 기존 VLAD_Ops_Ai_Env_Start 흐름을 유지합니다.
        /// USER_CUS_STD 계열은 기존 메일 설명처럼 VLAD_Custom_Registration을 우선 사용하고, 그 외에는 VLAD_Registration 후 모델을 등록합니다.
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

                if (user == (int)SDK_USER.USER_CUS_STD)
                {
                    long customId = _adapter.VLAD_Custom_ID_Generate(user, messageVersion, majorVersion, 0);
                    string para = "{\"MODEL\":0,\"CAM\":0}";
                    _vladId = _adapter.VLAD_Custom_Registration(customId, "VLAD_OPS", rootName, siteName, modelPath, para, gpuId);
                    return _vladId;
                }

                _vladId = _adapter.VLAD_Registration(user, messageVersion, majorVersion);
                if (string.IsNullOrWhiteSpace(modelPath) == false)
                {
                    _adapter.VLAD_Ops_Inference_Registration(rootName, siteName, modelPath, "{}", gpuId);
                }

                return _vladId;
            }
        }

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

        public static IntPtr VLAD_Inference_Mat(IntPtr rawData, float threshold, int drawMode)
        {
            lock (SyncRoot)
            {
                EnsureAdapter();
                return _adapter.VLAD_Inference_Mat(rawData, threshold, drawMode);
            }
        }

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
