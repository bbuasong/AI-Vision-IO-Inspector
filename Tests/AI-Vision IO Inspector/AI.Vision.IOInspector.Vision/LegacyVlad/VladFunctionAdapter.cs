using System;
using System.Text;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD 함수명과 현재 런타임 컨텍스트를 연결하는 호환 어댑터입니다.
    /// 기존 코드 담당자가 `VLAD_Registration`, `VLAD_Inference_Mat` 같은 이름으로 흐름을 따라갈 수 있게 유지합니다.
    /// </summary>
    public class VladFunctionAdapter
    {
        private readonly VladRuntimeContext _runtimeContext;

        public VladFunctionAdapter()
        {
            _runtimeContext = new VladRuntimeContext();
        }

        public IntPtr VLAD_Registration(int userId, int messageVersion, int majorVersion)
        {
            _runtimeContext.Register(userId, messageVersion, majorVersion);
            return _runtimeContext.VladId;
        }

        public long VLAD_Custom_ID_Generate(int userId, int messageVersion, int majorVersion, int minorVersion)
        {
            return VladNativeMethods.VLAD_Custom_ID_Generate(userId, messageVersion, majorVersion, minorVersion);
        }

        public IntPtr VLAD_Custom_Registration(
            long customId,
            string uiName,
            string rootName,
            string siteName,
            string modelPath,
            string customInfo,
            int gpuId)
        {
            _runtimeContext.RegisterCustomModel(customId, uiName, rootName, siteName, modelPath, customInfo, gpuId);
            return _runtimeContext.VladId;
        }

        public void VLAD_Ops_Inference_Registration(
            string kindName,
            string siteName,
            string modelPath,
            string customInfo,
            int gpuId)
        {
            _runtimeContext.RegisterOpsInferenceModel(kindName, siteName, modelPath, customInfo, gpuId);
        }

        public VisionInspectionOutput VLAD_Inference_Mat(VisionInspectionInput input, float threshold, int drawMode)
        {
            return _runtimeContext.Inference(input, threshold, drawMode);
        }

        public IntPtr VLAD_Inference_Mat(IntPtr rawData, float threshold, int drawMode)
        {
            return _runtimeContext.InferenceMat(rawData, threshold, drawMode);
        }

        public int VLAD_InferenceData_Get_Valid_Count(IntPtr detectData)
        {
            return _runtimeContext.GetValidDetectCount(detectData);
        }

        public int VLAD_InferenceData_V1_Draw(
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize)
        {
            return _runtimeContext.DrawInferenceDataV1(detectData, rawData, classCount, detectText, customParameter, tlvInfo, tlvSize);
        }

        public int VLAD_InferenceData_V2_Draw(IntPtr detectData, IntPtr rawData, IntPtr classCount, StringBuilder detectText)
        {
            return _runtimeContext.DrawInferenceDataV2(detectData, rawData, classCount, detectText);
        }
    }
}
