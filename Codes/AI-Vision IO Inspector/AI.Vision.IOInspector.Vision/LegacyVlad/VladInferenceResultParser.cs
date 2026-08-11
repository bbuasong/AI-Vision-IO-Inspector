using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_HD_Inference_Mat이 반환한 결과 JSON을 현재 프로젝트에서 쓰기 쉬운 결과 모델로 변환합니다.
    /// 요청과 결과가 같은 버퍼를 공유하는 구조라 별도 네이티브 조회 없이 문자열만 파싱합니다.
    /// </summary>
    public class VladInferenceResultParser
    {
        private readonly JavaScriptSerializer _jsonSerializer;

        public VladInferenceResultParser()
        {
            _jsonSerializer = new JavaScriptSerializer();
        }

        /// <summary>
        /// VLAD_HD_Inference_Mat 결과 JSON을 해석합니다.
        /// </summary>
        public VladInferenceResult Parse(string resultJson, ImageViewType viewType, string imagePath)
        {
            VladInferenceResult result = new VladInferenceResult();
            result.RawResultJson = resultJson;

            string parseErrorMessage;
            if (!TryApplyHdResultJson(resultJson, result, out parseErrorMessage))
            {
                result.Message = parseErrorMessage;
                return result;
            }

            result.ClassCounts = new int[0];
            result.IsSuccess = true;
            return result;
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

            if (System.Text.Encoding.UTF8.GetByteCount(partNo) > 63)
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

            System.Text.StringBuilder detectText = new System.Text.StringBuilder();
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
    }
}
