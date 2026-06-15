using System;
using System.Text;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD 등록, 모델 등록, Mat 포인터 추론 호출 상태를 보관하는 런타임 컨텍스트입니다.
    /// 기존 VLAD_Ops의 전역 상태를 현재 프로젝트에서는 명시적인 객체 상태로 관리합니다.
    /// </summary>
    public class VladRuntimeContext
    {
        public IntPtr VladId { get; private set; }

        public IntPtr InferenceId { get; private set; }

        public bool IsRegistered { get; private set; }

        public bool IsModelRegistered { get; private set; }

        public void Register(int userId, int messageVersion, int majorVersion)
        {
            try
            {
                VladId = VladNativeMethods.VLAD_Registration(userId, messageVersion, majorVersion);
                if (VladId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD_Registration이 빈 핸들을 반환했습니다.");
                }

                IsRegistered = true;
            }
            catch (DllNotFoundException ex)
            {
                throw BuildMissingVladSdkException(ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new InvalidOperationException("VLAD_SDK.dll의 x86/x64 비트수가 현재 프로세스와 맞지 않습니다.", ex);
            }
        }

        public void RegisterOpsInferenceModel(string kindName, string siteName, string modelPath, string customInfo, int gpuId)
        {
            EnsureRegistered();

            try
            {
                InferenceId = VladNativeMethods.VLAD_Ops_Inference_Registration(VladId, kindName, siteName,
                    NormalizeModelPath(modelPath), customInfo, gpuId);

                if (InferenceId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD_Ops_Inference_Registration이 빈 핸들을 반환했습니다.");
                }

                IsModelRegistered = true;
            }
            catch (DllNotFoundException ex)
            {
                throw BuildMissingVladSdkException(ex);
            }
        }

        public void RegisterInferenceModel(string modelPath, int gpuId)
        {
            EnsureRegistered();

            try
            {
                InferenceId = VladNativeMethods.VLAD_Inference_Registration(VladId, NormalizeModelPath(modelPath), gpuId);
                if (InferenceId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD_Inference_Registration이 빈 핸들을 반환했습니다.");
                }

                IsModelRegistered = true;
            }
            catch (DllNotFoundException ex)
            {
                throw BuildMissingVladSdkException(ex);
            }
        }

        public void RegisterCustomModel(long customId, string uiName, string rootName, string siteName, string modelPath, string customInfo, int gpuId)
        {
            try
            {
                VladId = VladNativeMethods.VLAD_Custom_Registration(customId, uiName, rootName, siteName,
                    NormalizeModelPath(modelPath), customInfo, gpuId);

                if (VladId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD_Custom_Registration이 빈 핸들을 반환했습니다.");
                }

                InferenceId = VladId;
                IsRegistered = true;
                IsModelRegistered = true;
            }
            catch (DllNotFoundException ex)
            {
                throw BuildMissingVladSdkException(ex);
            }
        }

        public void SetLog(int logType)
        {
            EnsureRegistered();
            VladNativeMethods.VLAD_Set_Log(VladId, logType);
        }

        public void UnsetLog(int logType)
        {
            EnsureRegistered();
            VladNativeMethods.VLAD_Unset_Log(VladId, logType);
        }

        public bool GetLog(int logType)
        {
            EnsureRegistered();
            return VladNativeMethods.VLAD_Get_Log(VladId, logType);
        }

        public bool WarmUp()
        {
            EnsureRegistered();
            return VladNativeMethods.VLAD_Warm_Up(VladId);
        }

        public bool Unregister()
        {
            if (VladId == IntPtr.Zero)
            {
                return true;
            }

            bool result = VladNativeMethods.VLAD_Unregistration(VladId);
            if (result)
            {
                VladId = IntPtr.Zero;
                InferenceId = IntPtr.Zero;
                IsRegistered = false;
                IsModelRegistered = false;
            }

            return result;
        }

        public int GetClassCount()
        {
            EnsureRegistered();
            return VladNativeMethods.VLAD_Get_Class_Count(VladId);
        }

        public VisionInspectionOutput Inference(VisionInspectionInput input, float threshold, int drawMode)
        {
            EnsureRegistered();
            EnsureModelRegistered();

            VisionInspectionOutput output = new VisionInspectionOutput();
            output.IsSuccess = false;
            output.IsMatched = false;
            output.Confidence = 0m;
            output.ModelVersion = "VLAD";
            output.Message = "VisionInspectionInput은 파일 경로 중심 입력입니다. VLAD_Inference_Mat 호출은 OpenCV Mat 포인터가 필요하므로 InferenceMat을 사용해야 합니다.";
            return output;
        }

        public IntPtr InferenceMat(IntPtr rawData, float threshold, int drawMode)
        {
            EnsureRegistered();
            EnsureModelRegistered();

            if (rawData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_Inference_Mat rawData 포인터가 비어 있습니다.", "rawData");
            }

            return VladNativeMethods.VLAD_Inference_Mat(VladId, rawData, threshold, drawMode);
        }

        public int GetValidDetectCount(IntPtr detectData)
        {
            EnsureRegistered();
            if (detectData == IntPtr.Zero)
            {
                return 0;
            }

            return VladNativeMethods.VLAD_InferenceData_Get_Valid_Count(VladId, detectData);
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
            EnsureRegistered();
            return VladNativeMethods.VLAD_InferenceData_V1_Draw(VladId, detectData, rawData, classCount,
                detectText, customParameter, tlvInfo, tlvSize);
        }

        public int DrawInferenceDataV2(IntPtr detectData, IntPtr rawData, IntPtr classCount, StringBuilder detectText)
        {
            EnsureRegistered();
            return VladNativeMethods.VLAD_InferenceData_V2_Draw(VladId, detectData, rawData, classCount, detectText);
        }

        public bool DrawCustomInferenceDataV1(
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize)
        {
            EnsureRegistered();
            return VladNativeMethods.VLAD_Custom_InferenceData_V1(VladId, detectData, rawData, classCount,
                detectText, customParameter, tlvInfo, tlvSize);
        }

        public int DrawCustomInferenceDataV1Only(
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter)
        {
            EnsureRegistered();
            return VladNativeMethods.VLAD_Custom_InferenceData_V1_Draw(VladId, detectData, rawData, classCount,
                detectText, customParameter);
        }

        private static string NormalizeModelPath(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return modelPath;
            }

            return modelPath;
        }

        private void EnsureRegistered()
        {
            if (!IsRegistered || VladId == IntPtr.Zero)
            {
                throw new InvalidOperationException("VLAD_Registration 또는 VLAD_Custom_Registration을 먼저 완료해야 합니다.");
            }
        }

        private void EnsureModelRegistered()
        {
            if (!IsModelRegistered || InferenceId == IntPtr.Zero)
            {
                throw new InvalidOperationException("VLAD inference model registration을 먼저 완료해야 합니다.");
            }
        }

        private static InvalidOperationException BuildMissingVladSdkException(DllNotFoundException ex)
        {
            return new InvalidOperationException(
                "VLAD_SDK.dll 또는 종속 DLL을 찾지 못했습니다. Native\\VLAD 경로와 plugins 폴더 배치를 확인하세요.",
                ex);
        }
    }
}
