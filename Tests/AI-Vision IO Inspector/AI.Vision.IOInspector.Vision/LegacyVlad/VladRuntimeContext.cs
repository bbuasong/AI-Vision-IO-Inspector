using System;
using System.Text;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD의 등록/추론 생명주기를 현재 프로젝트에 맞게 감싸는 어댑터 자리입니다.
    /// AI 담당자는 VLAD_Registration, VLAD_Inference_* 계열 P/Invoke 호출을 이 클래스 안으로 옮기면 됩니다.
    /// </summary>
    public class VladRuntimeContext
    {
        public IntPtr VladId { get; private set; }

        public bool IsRegistered { get; private set; }

        public bool IsModelRegistered { get; private set; }

        public void Register(int userId, int messageVersion, int majorVersion)
        {
            // 기존 VLAD 대응 함수:
            // VLAD_Registration(userId, messageVersion, majorVersion)
            throw new NotSupportedException("VLAD runtime registration is not implemented yet.");
        }

        public void RegisterInferenceModel(string kindName, string siteName, string modelPath, string customInfo, int gpuId)
        {
            // 기존 VLAD 대응 함수:
            // VLAD_Ops_Inference_Registration(VladId, kindName, siteName, modelPath, customInfo, gpuId)
            throw new NotSupportedException("VLAD inference model registration is not implemented yet.");
        }

        public void SetLog(int logType)
        {
            // 기존 VLAD 대응 함수:
            // VLAD_Set_Log(VladId, logType)
            throw new NotSupportedException("VLAD log setting is not implemented yet.");
        }

        public VisionInspectionOutput Inference(VisionInspectionInput input, float threshold, int drawMode)
        {
            // 기존 VLAD 대응 함수:
            // VLAD_Inference_Mat(...)
            // VLAD_InferenceData_V1_Draw(...)
            // VLAD_InferenceData_V2_Draw(...)
            throw new NotSupportedException("VLAD inference is not implemented yet.");
        }

        public int GetValidDetectCount(IntPtr detectData)
        {
            // 기존 VLAD 대응 함수:
            // VLAD_InferenceData_Get_Valid_Count(VladId, detectData)
            throw new NotSupportedException("VLAD detect count parsing is not implemented yet.");
        }

        public int DrawInferenceDataV1(
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize)
        {
            // 기존 VLAD 대응 함수:
            // VLAD_InferenceData_V1_Draw(...)
            throw new NotSupportedException("VLAD V1 draw/parser is not implemented yet.");
        }

        public int DrawInferenceDataV2(IntPtr detectData, IntPtr rawData, IntPtr classCount, StringBuilder detectText)
        {
            // 기존 VLAD 대응 함수:
            // VLAD_InferenceData_V2_Draw(...)
            throw new NotSupportedException("VLAD V2 draw/parser is not implemented yet.");
        }
    }
}
