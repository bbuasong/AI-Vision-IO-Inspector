using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Web.Script.Serialization;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_SDK가 반환한 detectData 포인터를 현재 프로젝트에서 쓰기 쉬운 결과 모델로 변환합니다.
    /// 기존 VLAD_Ops 흐름처럼 Draw 함수를 먼저 호출해 detectText와 classCount를 채우고,
    /// MSG_V1 형식으로 보이는 경우에는 bbox 정보도 직접 해석합니다.
    /// </summary>
    public class VladInferenceResultParser
    {
        private const int DltLength = 4;
        private const int DefaultClassBufferLength = 256;
        private const int MaxDetectionCount = 1024;
        private const int MaxStringFieldLength = 260;
        private readonly JavaScriptSerializer _jsonSerializer;

        public VladInferenceResultParser()
        {
            _jsonSerializer = new JavaScriptSerializer();
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        /// <summary>
        /// 전체 이미지와 Crop 이미지용 VLAD ID를 함께 받는 결과 해석 진입점입니다.
        /// 현재 배포 DLL은 구버전 Draw/TLV 결과만 제공하므로 호환 경로에서는 전체 이미지 ID로 결과를 읽습니다.
        /// 새 DLL의 VLAD_HD_InferenceData_Result가 제공되면 이 메서드에서 두 ID와 JSON 결과를 함께 사용합니다.
        /// </summary>
        public VladInferenceResult Parse(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr detectData,
            IntPtr rawMatPointer,
            ImageViewType viewType,
            string imagePath)
        {
            VladInferenceResult result = new VladInferenceResult();
            if (fullImageVladId == IntPtr.Zero || croppedImageVladId == IntPtr.Zero)
            {
                result.Message = "전체 이미지 또는 Crop 이미지용 VLAD_ID가 비어 있어 결과를 해석할 수 없습니다.";
                return result;
            }

            if (detectData == IntPtr.Zero)
            {
                result.Message = "VLAD detectData 포인터가 비어 있습니다.";
                return result;
            }

            // 새 HD DLL로 두 ID 추론이 실제 수행된 경우에는 구버전 Draw/TLV 메모리를 해석하지 않습니다.
            // 결과 JSON을 먼저 받아 기존 Application 측정값 처리 형식으로 변환합니다.
            if (VLAD_Ops_Ai.IsHdInferenceApiActive || VLAD_Ops_Ai.IsTestResultJsonEnabled)
            {
                return ParseHdInferenceResult(fullImageVladId, croppedImageVladId, detectData);
            }

            // 구버전 SDK 호환 경로에서는 전체 이미지 ID의 class/detect 결과를 사용합니다.
            IntPtr vladId = fullImageVladId;
            int classBufferLength = GetClassBufferLength(vladId);
            int[] classCounts = new int[classBufferLength];
            StringBuilder detectTextBuilder = new StringBuilder(8192);
            int validDetectionCount = GetSafeValidDetectionCount(vladId, detectData);
            Custom_Info_Struct[] customInfos = new Custom_Info_Struct[0];

            try
            {
                // classCounts 배열은 SDK가 직접 값을 채우므로 GC 이동을 막기 위해 pinning합니다.
                GCHandle classCountHandle = GCHandle.Alloc(classCounts, GCHandleType.Pinned);
                try
                {
                    // detectText와 TLV(Custom_Info_Struct) 결과를 SDK Draw 함수에서 함께 채웁니다.
                    customInfos = FillDrawResult(vladId, detectData, rawMatPointer, classCountHandle.AddrOfPinnedObject(), detectTextBuilder, validDetectionCount);    // VLAD_Custom_InferenceData_V1
                }
                finally
                {
                    classCountHandle.Free();
                }

                result.ClassCounts = classCounts;
                result.DetectText = detectTextBuilder.ToString();
                result.ValidDetectionCount = validDetectionCount;
                AddCustomInfos(result, customInfos);
                result.IsSuccess = true;

                // 원본 VLAD_Ops는 detectData 메모리를 C#에서 직접 파싱하지 않고 SDK Draw 함수 결과를 사용합니다.
                // SDK 메시지 구조가 다를 때 직접 Marshal.Copy를 수행하면 프로세스가 종료될 수 있으므로 기본은 비활성화합니다.
                if (IsRawDetectDataParsingEnabled())
                {
                    TryParseV1Detections(vladId, detectData, viewType, imagePath, result);
                }

                if (result.Detections.Count == 0)
                {
                    // TLV 구조체에 검출 좌표가 있으면 이를 현재 프로젝트의 VladDetection으로 변환합니다.
                    AddCustomInfoDetections(viewType, imagePath, result);
                }

                if (result.Detections.Count == 0)
                {
                    AddClassCountDetections(vladId, viewType, imagePath, result);
                }

                result.Message = "VLAD detectData 해석 완료";
                return result;
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD detectData 결과 파싱 중 보호 메모리 예외가 발생했습니다. VLAD_Inference_Mat 반환 detectData와 SDK 메시지 버전을 확인하십시오.";
                VLAD_Ops_Ai.BlockNativeInference(message);
                throw new InvalidOperationException(message, ex);
            }
        }

        private int GetClassBufferLength(IntPtr vladId)
        {
            int classCount = VLAD_Ops_Ai.VLAD_Get_Class_Count(vladId);
            if (classCount < DefaultClassBufferLength)
            {
                return DefaultClassBufferLength;
            }

            return classCount + 16;
        }

        private Custom_Info_Struct[] FillDrawResult(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawMatPointer,
            IntPtr classCountPointer,
            StringBuilder detectTextBuilder,
            int validDetectionCount)
        {
            // VLAD SDK 버전에 따라 결과를 채우는 함수가 다릅니다.
            // 현재 HD 커스텀 모델은 USER_CUS_STD + MSG_V1 흐름에서 VLAD_Custom_InferenceData_V1을 사용합니다.
            int aiVersion = VLAD_Ops_Ai.VLAD_Get_Ai_Ver(vladId);
            int messageVersion = VLAD_Ops_Ai.VLAD_Get_Msg_Ver(vladId);

            if (messageVersion == (int)SDK_MSG.MSG_V2)
            {
                VLAD_Ops_Ai.VLAD_InferenceData_V2_Draw(vladId, detectData, rawMatPointer, classCountPointer, detectTextBuilder);
                return new Custom_Info_Struct[0];
            }

            if (aiVersion == (int)SDK_USER.USER_CUS_STD ||
                aiVersion == (int)SDK_USER.USER_SRD ||
                aiVersion == (int)SDK_USER.USER_MPS ||
                aiVersion == (int)SDK_USER.USER_ATS)
            {
                return FillCustomDrawResult(vladId, detectData, rawMatPointer, classCountPointer, detectTextBuilder, validDetectionCount);
            }

            VLAD_Ops_Ai.VLAD_InferenceData_V1_Draw(vladId, detectData, rawMatPointer, classCountPointer, detectTextBuilder, string.Empty, IntPtr.Zero, 0);
            return new Custom_Info_Struct[0];
        }

        private Custom_Info_Struct[] FillCustomDrawResult(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawMatPointer,
            IntPtr classCountPointer,
            StringBuilder detectTextBuilder,
            int validDetectionCount)
        {
            if (validDetectionCount <= 0 || validDetectionCount > MaxDetectionCount)
            {
                // 검출 수가 없거나 비정상적으로 크면 TLV 버퍼 없이 detectText/classCount만 받습니다.
                VLAD_Ops_Ai.VLAD_Custom_InferenceData_V1(vladId, detectData, rawMatPointer, classCountPointer, detectTextBuilder, string.Empty, IntPtr.Zero, 0);
                return new Custom_Info_Struct[0];
            }

            // TLV 버퍼는 validDetectionCount개 구조체를 받을 수 있게 직접 할당합니다.
            // SDK 호출 후 즉시 관리 메모리 구조체 배열로 복사하고 finally에서 해제합니다.
            int tlvSize = Marshal.SizeOf(typeof(Custom_Info_Struct));
            int bufferSize = checked(tlvSize * validDetectionCount);
            IntPtr tlvInfo = Marshal.AllocHGlobal(bufferSize);

            try
            {
                byte[] empty = new byte[bufferSize];
                Marshal.Copy(empty, 0, tlvInfo, bufferSize);

                VLAD_Ops_Ai.VLAD_Custom_InferenceData_V1(vladId, detectData, rawMatPointer, classCountPointer, detectTextBuilder, string.Empty, tlvInfo, tlvSize);
                // FreeHGlobal 이후에는 tlvInfo를 읽을 수 없으므로 여기서 반드시 복사합니다.
                return ReadCustomInfoStructs(tlvInfo, tlvSize, validDetectionCount);
            }
            finally
            {
                Marshal.FreeHGlobal(tlvInfo);
            }
        }

        /// <summary>
        /// 새 HD DLL이 반환하는 InspectionResult JSON을 현재 검사 엔진의 DetectText 형식으로 변환합니다.
        /// 이 변환으로 기존 VladMeasurementMapper가 IndexNo 순서의 측정값을 계속 처리할 수 있습니다.
        /// </summary>
        private VladInferenceResult ParseHdInferenceResult(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr detectData)
        {
            VladInferenceResult result = new VladInferenceResult();

            try
            {
                result.RawResultJson = VLAD_Ops_Ai.VLAD_HD_InferenceData_Result(
                    fullImageVladId, croppedImageVladId, detectData);
                string parseErrorMessage;
                if (TryApplyHdResultJson(result.RawResultJson, result, out parseErrorMessage) == false)
                {
                    result.Message = parseErrorMessage;
                    return result;
                }

                result.ClassCounts = new int[0];
                result.IsSuccess = true;
                return result;
            }
            catch (EntryPointNotFoundException ex)
            {
                result.Message = "VLAD_HD_InferenceData_Result export가 없습니다. VLAD_HD_Inference_Mat와 같은 DLL에 함께 배포해야 합니다. " + ex.Message;
                return result;
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_HD_InferenceData_Result JSON 파싱 중 보호 메모리 예외가 발생했습니다.";
                VLAD_Ops_Ai.BlockNativeInference(message);
                throw new InvalidOperationException(message, ex);
            }
        }

        /// <summary>
        /// HD JSON의 숫자 viewName/viewJudge, Score, W/D/H와 measurements[]를 결과 객체에 보존하고,
        /// 기존 검사 엔진 호환용 DetectText도 함께 생성합니다.
        /// 측정값은 indexNo 오름차순으로 정렬해 DB 측정부 IndexNo와 같은 순서를 유지합니다.
        /// </summary>
        private bool TryApplyHdResultJson(string resultJson, VladInferenceResult result, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(resultJson))
            {
                errorMessage = "VLAD HD 결과 JSON이 비어 있습니다.";
                return false;
            }

            Dictionary<string, object> root;
            try
            {
                root = _jsonSerializer.DeserializeObject(resultJson) as Dictionary<string, object>;
            }
            catch (Exception ex)
            {
                errorMessage = "VLAD HD 결과 JSON 형식이 올바르지 않습니다. " + ex.Message;
                return false;
            }

            if (root == null)
            {
                errorMessage = "VLAD HD 결과 JSON 최상위 객체를 읽을 수 없습니다.";
                return false;
            }

            string partNo = GetString(root, "partNo");
            if (string.IsNullOrWhiteSpace(partNo))
            {
                errorMessage = "VLAD HD 결과 JSON에 partNo 값이 없습니다.";
                return false;
            }

            if (Encoding.UTF8.GetByteCount(partNo) > 63)
            {
                errorMessage = "VLAD HD 결과 JSON의 partNo가 UTF-8 63 byte 제한을 초과했습니다.";
                return false;
            }

            int viewCode;
            if (!TryGetInt32(root, "viewName", out viewCode) || viewCode < 1 || viewCode > 6)
            {
                errorMessage = "VLAD HD 결과 JSON의 viewName은 1~6 정수여야 합니다.";
                return false;
            }

            int viewJudgeCode;
            if (!TryGetInt32(root, "viewJudge", out viewJudgeCode) ||
                (viewJudgeCode != 0 && viewJudgeCode != 1))
            {
                errorMessage = "VLAD HD 결과 JSON의 viewJudge는 0(PASS) 또는 1(FAIL)이어야 합니다.";
                return false;
            }

            string viewJudge = viewJudgeCode == 0 ? "PASS" : "FAIL";

            decimal score;
            if (!TryGetDecimal(root, "score", out score))
            {
                errorMessage = "VLAD HD 결과 JSON에 score 값이 없습니다.";
                return false;
            }

            decimal scoreThreshold;
            if (!TryGetDecimal(root, "scoreThreshold", out scoreThreshold))
            {
                errorMessage = "VLAD HD 결과 JSON에 scoreThreshold 값이 없습니다.";
                return false;
            }

            List<VladInferenceMeasurement> measurements = ReadHdMeasurements(root);
            measurements.Sort(new VladInferenceMeasurementComparer());

            result.Status = "SUCCESS";
            result.PartNo = partNo;
            result.ViewCode = viewCode;
            result.ViewName = GetViewName(viewCode);
            result.ViewJudgeCode = viewJudgeCode;
            result.ViewJudge = viewJudge;
            result.OverallJudge = viewJudge;
            result.ImageJudge = viewJudge;
            result.MeasurementJudge = viewCode == 6 ? viewJudge : "NOT_APPLICABLE";

            result.Score = score;
            result.ScoreThreshold = scoreThreshold;
            result.Dimensions = ReadHdDimensions(root);
            result.Measurements.Clear();
            foreach (VladInferenceMeasurement measurement in measurements)
            {
                result.Measurements.Add(measurement);
            }

            result.FailureReasons.Clear();

            StringBuilder detectText = new StringBuilder();
            detectText.Append(IsPassJudge(viewJudge) ? "true" : "false");
            detectText.Append(',');
            detectText.Append(score.ToString("0.00", CultureInfo.InvariantCulture));
            foreach (VladInferenceMeasurement measurement in measurements)
            {
                detectText.Append(',');
                detectText.Append(measurement.MeasuredValue.ToString("0.00", CultureInfo.InvariantCulture));
            }

            result.DetectText = detectText.ToString();
            result.ValidDetectionCount = measurements.Count;
            result.Message = "VLAD HD 결과 JSON 해석 완료";

            return true;
        }

        /// <summary>
        /// 검사 결과 이미지 하단 표시에 사용하는 대략적인 W/D/H 값을 읽습니다.
        /// AI가 계산하지 못한 값은 null로 유지합니다.
        /// </summary>
        private VladInferenceDimensions ReadHdDimensions(Dictionary<string, object> root)
        {
            object dimensionsObject;
            Dictionary<string, object> source;
            if (!root.TryGetValue("dimensions", out dimensionsObject) ||
                (source = dimensionsObject as Dictionary<string, object>) == null)
            {
                return null;
            }

            VladInferenceDimensions dimensions = new VladInferenceDimensions();
            dimensions.Width = GetNullableDecimal(source, "width");
            dimensions.Depth = GetNullableDecimal(source, "depth");
            dimensions.Height = GetNullableDecimal(source, "height");
            dimensions.Unit = "mm";
            return dimensions;
        }

        private string GetViewName(int viewCode)
        {
            switch (viewCode)
            {
                case 1:
                    return "Top";
                case 2:
                    return "Front";
                case 3:
                    return "Back";
                case 4:
                    return "Left";
                case 5:
                    return "Right";
                case 6:
                    return "Thickness";
                default:
                    return string.Empty;
            }
        }

        private decimal? GetNullableDecimal(Dictionary<string, object> source, string key)
        {
            decimal value;
            return TryGetDecimal(source, key, out value) ? (decimal?)value : null;
        }

        private bool IsErrorStatusOrJudge(string status, string judge)
        {
            return string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(judge, "ERROR", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 이전 결과 필드 계약을 보존한 참고 구현입니다. 신규 호출 경로에서는 사용하지 않습니다.
        /// </summary>
        private bool TryApplyHdResultJsonLegacy(string resultJson, VladInferenceResult result, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(resultJson))
            {
                errorMessage = "VLAD HD 결과 JSON이 비어 있습니다.";
                return false;
            }

            Dictionary<string, object> root;
            try
            {
                root = _jsonSerializer.DeserializeObject(resultJson) as Dictionary<string, object>;
            }
            catch (Exception ex)
            {
                errorMessage = "VLAD HD 결과 JSON 형식이 올바르지 않습니다. " + ex.Message;
                return false;
            }

            if (root == null)
            {
                errorMessage = "VLAD HD 결과 JSON 최상위 객체를 읽을 수 없습니다.";
                return false;
            }

            string imageJudge = GetString(root, "imageJudge");
            if (string.IsNullOrWhiteSpace(imageJudge))
            {
                errorMessage = "VLAD HD 결과 JSON에 imageJudge 값이 없습니다.";
                return false;
            }

            string measurementJudge = GetString(root, "measurementJudge");
            if (string.IsNullOrWhiteSpace(measurementJudge))
            {
                errorMessage = "VLAD HD 결과 JSON에 measurementJudge 값이 없습니다.";
                return false;
            }

            string overallJudge = GetString(root, "overallJudge");
            if (string.IsNullOrWhiteSpace(overallJudge))
            {
                errorMessage = "VLAD HD 결과 JSON에 overallJudge 값이 없습니다.";
                return false;
            }

            if (root.ContainsKey("failureReasons") == false)
            {
                errorMessage = "VLAD HD 결과 JSON에 failureReasons 배열이 없습니다.";
                return false;
            }

            decimal score;
            if (TryGetDecimal(root, "score", out score) == false)
            {
                errorMessage = "VLAD HD 결과 JSON에 score 값이 없습니다.";
                return false;
            }

            decimal scoreThreshold;
            TryGetDecimal(root, "scoreThreshold", out scoreThreshold);

            bool isMatched = IsPassJudge(overallJudge);
            List<VladInferenceMeasurement> measurements = ReadHdMeasurements(root);
            measurements.Sort(new VladInferenceMeasurementComparer());

            List<string> failureReasons;
            if (TryReadHdFailureReasons(root, out failureReasons, out errorMessage) == false)
            {
                return false;
            }

            result.ImageJudge = imageJudge;
            result.ViewName = GetString(root, "viewName");
            result.MeasurementJudge = measurementJudge;
            result.OverallJudge = overallJudge;
            result.Score = score;
            result.ScoreThreshold = scoreThreshold;
            result.Measurements.Clear();
            foreach (VladInferenceMeasurement measurement in measurements)
            {
                result.Measurements.Add(measurement);
            }

            result.FailureReasons.Clear();
            foreach (string failureReason in failureReasons)
            {
                result.FailureReasons.Add(failureReason);
            }

            StringBuilder detectText = new StringBuilder();
            detectText.Append(isMatched ? "true" : "false");
            detectText.Append(',');
            detectText.Append(score.ToString("0.00", CultureInfo.InvariantCulture));
            foreach (VladInferenceMeasurement measurement in measurements)
            {
                detectText.Append(',');
                detectText.Append(measurement.MeasuredValue.ToString("0.00", CultureInfo.InvariantCulture));
            }

            result.DetectText = detectText.ToString();
            result.ValidDetectionCount = measurements.Count;
            result.Message = GetString(root, "message");
            if (string.IsNullOrWhiteSpace(result.Message))
            {
                result.Message = "VLAD HD 결과 JSON 해석 완료";
            }

            return true;
        }

        /// <summary>
        /// measurements 배열에서 indexNo와 measuredValue를 읽습니다.
        /// 단위는 프로그램 공통 기준인 mm로 설정하고, 요청 기준값과 허용오차는 기존 측정부 정보에서 비교합니다.
        /// </summary>
        private List<VladInferenceMeasurement> ReadHdMeasurements(Dictionary<string, object> root)
        {
            List<VladInferenceMeasurement> values = new List<VladInferenceMeasurement>();
            object measurementObject;
            if (root.TryGetValue("measurements", out measurementObject) == false)
            {
                return values;
            }

            IList measurementItems = measurementObject as IList;
            if (measurementItems == null)
            {
                return values;
            }

            foreach (object item in measurementItems)
            {
                Dictionary<string, object> measurement = item as Dictionary<string, object>;
                if (measurement == null)
                {
                    continue;
                }

                int indexNo;
                decimal measuredValue;
                if (TryGetInt32(measurement, "indexNo", out indexNo) == false ||
                    TryGetDecimal(measurement, "measuredValue", out measuredValue) == false)
                {
                    continue;
                }

                VladInferenceMeasurement value = new VladInferenceMeasurement();
                value.MeasurementRegionId = GetInt32OrDefault(measurement, "measurementRegionId");
                value.IndexNo = indexNo;
                value.ItemType = GetString(measurement, "itemType");
                value.MeasuredValue = measuredValue;
                value.SpecValue = GetDecimalOrDefault(measurement, "specValue");
                value.ToleranceMin = GetDecimalOrDefault(measurement, "toleranceMin", "lowerTolerance");
                value.ToleranceMax = GetDecimalOrDefault(measurement, "toleranceMax", "upperTolerance");
                value.Judge = GetString(measurement, "judge");
                value.Unit = "mm";
                values.Add(value);

                if (values.Count >= 5)
                {
                    break;
                }
            }

            return values;
        }

        /// <summary>
        /// failureReasons 배열의 문자열을 순서대로 보존합니다.
        /// 빈 배열은 정상 PASS 응답에서 유효한 값입니다.
        /// </summary>
        private bool TryReadHdFailureReasons(
            Dictionary<string, object> root,
            out List<string> failureReasons,
            out string errorMessage)
        {
            failureReasons = new List<string>();
            errorMessage = string.Empty;
            object failureReasonObject;
            if (root.TryGetValue("failureReasons", out failureReasonObject) == false)
            {
                errorMessage = "VLAD HD 결과 JSON에 failureReasons 배열이 없습니다.";
                return false;
            }

            IList failureReasonItems = failureReasonObject as IList;
            if (failureReasonItems == null)
            {
                errorMessage = "VLAD HD 결과 JSON의 failureReasons는 배열이어야 합니다.";
                return false;
            }

            foreach (object item in failureReasonItems)
            {
                string failureReason = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(failureReason) == false)
                {
                    failureReasons.Add(failureReason);
                }
            }

            return true;
        }

        private bool IsPassJudge(string judge)
        {
            return string.Equals(judge, "PASS", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(judge, "OK", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(judge, "TRUE", StringComparison.OrdinalIgnoreCase);
        }

        private string GetString(Dictionary<string, object> source, string key)
        {
            object value;
            if (source == null || source.TryGetValue(key, out value) == false || value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private bool TryGetInt32(Dictionary<string, object> source, string key, out int value)
        {
            value = 0;
            object rawValue;
            if (source == null || source.TryGetValue(key, out rawValue) == false || rawValue == null)
            {
                return false;
            }

            return int.TryParse(Convert.ToString(rawValue, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private bool TryGetDecimal(Dictionary<string, object> source, string key, out decimal value)
        {
            value = 0m;
            object rawValue;
            if (source == null || source.TryGetValue(key, out rawValue) == false || rawValue == null)
            {
                return false;
            }

            return decimal.TryParse(Convert.ToString(rawValue, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private decimal GetDecimalOrDefault(Dictionary<string, object> source, params string[] keys)
        {
            if (keys != null)
            {
                foreach (string key in keys)
                {
                    decimal value;
                    if (TryGetDecimal(source, key, out value))
                    {
                        return value;
                    }
                }
            }

            return 0m;
        }

        private int GetInt32OrDefault(Dictionary<string, object> source, params string[] keys)
        {
            if (keys != null)
            {
                foreach (string key in keys)
                {
                    int value;
                    if (TryGetInt32(source, key, out value))
                    {
                        return value;
                    }
                }
            }

            return 0;
        }

        private sealed class VladInferenceMeasurementComparer : IComparer<VladInferenceMeasurement>
        {
            public int Compare(VladInferenceMeasurement left, VladInferenceMeasurement right)
            {
                if (left == null && right == null)
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                return left.IndexNo.CompareTo(right.IndexNo);
            }
        }

        private bool IsRawDetectDataParsingEnabled()
        {
            string value = Environment.GetEnvironmentVariable("AI_VISION_VLAD_PARSE_RAW_DETECT_DATA");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private int GetSafeValidDetectionCount(IntPtr vladId, IntPtr detectData)
        {
            int validCount = VLAD_Ops_Ai.VLAD_InferenceData_Get_Valid_Count(vladId, detectData);
            if (validCount < 0)
            {
                return 0;
            }

            if (validCount > MaxDetectionCount)
            {
                return MaxDetectionCount;
            }

            return validCount;
        }

        private Custom_Info_Struct[] ReadCustomInfoStructs(IntPtr tlvInfo, int tlvSize, int validCount)
        {
            Custom_Info_Struct[] customInfos = new Custom_Info_Struct[validCount];
            for (int index = 0; index < validCount; index++)
            {
                // SDK가 연속 배열 형태로 채운 TLV 메모리를 구조체 단위로 이동하며 읽습니다.
                IntPtr itemPointer = IntPtr.Add(tlvInfo, tlvSize * index);
                customInfos[index] = (Custom_Info_Struct)Marshal.PtrToStructure(itemPointer, typeof(Custom_Info_Struct));
            }

            return customInfos;
        }

        private void AddCustomInfos(VladInferenceResult result, Custom_Info_Struct[] customInfos)
        {
            if (customInfos == null)
            {
                return;
            }

            foreach (Custom_Info_Struct customInfo in customInfos)
            {
                // 네이티브 구조체를 UI/검사 로직에서 안전하게 사용할 관리 모델로 변환합니다.
                VladCustomInferenceInfo info = new VladCustomInferenceInfo();
                info.ClassId = customInfo.class_id;
                info.ClassName = customInfo.cls_name ?? string.Empty;
                info.Score = NormalizeScore((decimal)customInfo.score);
                info.X1 = customInfo.p1.x;
                info.Y1 = customInfo.p1.y;
                info.X2 = customInfo.p2.x;
                info.Y2 = customInfo.p2.y;
                result.CustomInfos.Add(info);
            }
        }

        private void AddCustomInfoDetections(ImageViewType viewType, string imagePath, VladInferenceResult result)
        {
            if (result.CustomInfos == null)
            {
                return;
            }

            foreach (VladCustomInferenceInfo info in result.CustomInfos)
            {
                if (info == null)
                {
                    continue;
                }

                VladDetection detection = new VladDetection();
                detection.ViewType = viewType;
                detection.ClassId = info.ClassId;
                detection.ClassName = string.IsNullOrWhiteSpace(info.ClassName)
                    ? "Class" + info.ClassId.ToString(CultureInfo.InvariantCulture)
                    : info.ClassName;
                detection.Score = info.Score;
                detection.X = Math.Min(info.X1, info.X2);
                detection.Y = Math.Min(info.Y1, info.Y2);
                detection.Width = Math.Abs(info.X2 - info.X1);
                detection.Height = Math.Abs(info.Y2 - info.Y1);
                detection.SourceImagePath = imagePath;
                result.Detections.Add(detection);
            }
        }

        private void TryParseV1Detections(IntPtr vladId, IntPtr detectData, ImageViewType viewType, string imagePath, VladInferenceResult result)
        {
            try
            {
                int offset = 0;
                string messageType = ReadAscii(detectData, ref offset, DltLength);
                if (string.IsNullOrWhiteSpace(messageType))
                {
                    return;
                }

                int folderLength = ReadFixedInt(detectData, ref offset);
                if (!IsValidLength(folderLength))
                {
                    return;
                }

                if (folderLength > 0)
                {
                    ReadAscii(detectData, ref offset, folderLength);
                }

                int validCount = ReadFixedInt(detectData, ref offset);
                if (validCount < 0 || validCount > MaxDetectionCount)
                {
                    return;
                }

                int loopCount = validCount;
                if (loopCount == 0 && result.ValidDetectionCount > 0 && result.ValidDetectionCount <= MaxDetectionCount)
                {
                    loopCount = result.ValidDetectionCount;
                }

                for (int index = 0; index < loopCount; index++)
                {
                    VladDetection detection = ReadV1Detection(vladId, detectData, ref offset, viewType, imagePath);
                    if (detection != null)
                    {
                        result.Detections.Add(detection);
                    }
                }
            }
            catch
            {
                // 모델/SDK 버전에 따라 detectData 내부 형식이 달라질 수 있으므로,
                // 직접 파싱이 실패하면 Draw 함수에서 얻은 classCount 기반 결과만 사용합니다.
            }
        }

        private VladDetection ReadV1Detection(IntPtr vladId, IntPtr detectData, ref int offset, ImageViewType viewType, string fallbackImagePath)
        {
            ReadFixedInt(detectData, ref offset);
            ReadFixedInt(detectData, ref offset);

            int imageFileLength = ReadFixedInt(detectData, ref offset);
            if (!IsValidLength(imageFileLength))
            {
                return null;
            }

            string imagePath = string.Empty;
            if (imageFileLength > 0)
            {
                imagePath = ReadAscii(detectData, ref offset, imageFileLength);
            }

            int classId = ReadFixedInt(detectData, ref offset);
            decimal score = ReadFixedDecimal(detectData, ref offset);
            int x = ReadFixedInt(detectData, ref offset);
            int y = ReadFixedInt(detectData, ref offset);
            int width = ReadFixedInt(detectData, ref offset);
            int height = ReadFixedInt(detectData, ref offset);

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                imagePath = fallbackImagePath;
            }

            VladDetection detection = new VladDetection();
            detection.ViewType = viewType;
            detection.ClassId = classId;
            detection.ClassName = GetClassName(vladId, classId);
            detection.Score = NormalizeScore(score);
            detection.X = x;
            detection.Y = y;
            detection.Width = width;
            detection.Height = height;
            detection.SourceImagePath = imagePath;
            return detection;
        }

        private void AddClassCountDetections(IntPtr vladId, ImageViewType viewType, string imagePath, VladInferenceResult result)
        {
            if (result.ClassCounts == null)
            {
                return;
            }

            for (int index = 0; index < result.ClassCounts.Length; index++)
            {
                int count = result.ClassCounts[index];
                if (count <= 0)
                {
                    continue;
                }

                for (int occurrence = 0; occurrence < count && occurrence < MaxDetectionCount; occurrence++)
                {
                    VladDetection detection = new VladDetection();
                    detection.ViewType = viewType;
                    detection.ClassId = index;
                    detection.ClassName = GetClassName(vladId, index);
                    detection.Score = 0m;
                    detection.SourceImagePath = imagePath;
                    result.Detections.Add(detection);
                }
            }
        }

        private string GetClassName(IntPtr vladId, int classId)
        {
            IntPtr namePointer = VLAD_Ops_Ai.VLAD_Get_Class_Str(vladId, classId);
            if (namePointer == IntPtr.Zero)
            {
                return "Class" + classId.ToString(CultureInfo.InvariantCulture);
            }

            string name = Marshal.PtrToStringAnsi(namePointer);
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Class" + classId.ToString(CultureInfo.InvariantCulture);
            }

            return name;
        }

        private bool IsValidLength(int length)
        {
            return length >= 0 && length <= MaxStringFieldLength;
        }

        private string ReadAscii(IntPtr source, ref int offset, int length)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            byte[] buffer = new byte[length];
            Marshal.Copy(IntPtr.Add(source, offset), buffer, 0, length);
            offset += length;
            return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        }

        private int ReadFixedInt(IntPtr source, ref int offset)
        {
            string text = ReadAscii(source, ref offset, DltLength).Trim();
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }

            return value;
        }

        private decimal ReadFixedDecimal(IntPtr source, ref int offset)
        {
            string text = ReadAscii(source, ref offset, DltLength).Trim();
            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return 0m;
            }

            return value;
        }

        private decimal NormalizeScore(decimal score)
        {
            if (score > 1m && score <= 100m)
            {
                return score / 100m;
            }

            return score;
        }
    }
}
