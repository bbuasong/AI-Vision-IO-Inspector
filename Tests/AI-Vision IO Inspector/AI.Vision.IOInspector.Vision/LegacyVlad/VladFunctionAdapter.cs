using System;
using System.Text;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD 함수명을 그대로 노출하는 호환용 파사드입니다.
    /// VLAD 담당자가 예전 함수명으로 검색했을 때 현재 프로젝트의 변환 지점을 빠르게 찾기 위한 클래스입니다.
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

        public void VLAD_Ops_Inference_Registration(
            string kindName,
            string siteName,
            string modelPath,
            string customInfo,
            int gpuId)
        {
            _runtimeContext.RegisterInferenceModel(kindName, siteName, modelPath, customInfo, gpuId);
        }

        public VisionInspectionOutput VLAD_Inference_Mat(VisionInspectionInput input, float threshold, int drawMode)
        {
            return _runtimeContext.Inference(input, threshold, drawMode);
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
