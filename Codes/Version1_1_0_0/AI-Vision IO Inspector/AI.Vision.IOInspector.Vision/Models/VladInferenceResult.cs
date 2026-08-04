using System.Collections.Generic;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// VLAD_SDK detectData에서 해석한 1회 추론 결과입니다.
    /// 현재 프로젝트는 detectText의 true,score,measurement1...N 결과 문자열을 기준으로 치수값을 해석합니다.
    /// </summary>
    public class VladInferenceResult
    {
        public VladInferenceResult()
        {
            Detections = new List<VladDetection>();
            CustomInfos = new List<VladCustomInferenceInfo>();
            Measurements = new List<VladInferenceMeasurement>();
            FailureReasons = new List<string>();
            ClassCounts = new int[0];
        }

        public bool IsSuccess { get; set; }

        /// <summary>
        /// HD 결과 JSON의 처리 상태입니다. SUCCESS 또는 ERROR 값을 사용합니다.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 결과를 생성한 카메라 위치입니다. 예: Top, Front, Thickness.
        /// </summary>
        public string ViewName { get; set; }

        public int ValidDetectionCount { get; set; }

        public string DetectText { get; set; }              // 결과값 Data..

        /// <summary>
        /// 새 HD DLL의 VLAD_HD_InferenceData_Result가 반환한 원본 UTF-8 JSON입니다.
        /// 현재 레거시 Draw/TLV 경로에서는 빈 문자열로 유지합니다.
        /// </summary>
        public string RawResultJson { get; set; }

        /// <summary>
        /// HD 검사 결과 JSON의 이미지 정합 판정입니다. PASS, FAIL, ERROR 값을 사용합니다.
        /// </summary>
        public string ImageJudge { get; set; }

        /// <summary>
        /// HD 검사 결과 JSON의 측정부 기준값 비교 판정입니다.
        /// 측정부가 없는 View는 NOT_APPLICABLE 값을 사용합니다.
        /// </summary>
        public string MeasurementJudge { get; set; }

        /// <summary>
        /// 현재 카메라 View의 최종 AI 판정입니다. PASS, FAIL, ERROR 값을 사용합니다.
        /// </summary>
        public string ViewJudge { get; set; }

        /// <summary>
        /// 구형 DLL의 overallJudge 응답을 위한 호환 속성입니다.
        /// 신규 코드에서는 ViewJudge를 사용합니다.
        /// </summary>
        public string OverallJudge { get; set; }

        /// <summary>
        /// AI가 반환한 이미지 정합 Score와 요청에 사용한 기준 Score입니다.
        /// </summary>
        public decimal Score { get; set; }

        public decimal ScoreThreshold { get; set; }

        /// <summary>
        /// AI가 반환한 대략적인 폭/깊이/높이 정보입니다.
        /// 값을 계산하지 못한 경우 각 값은 null입니다.
        /// </summary>
        public VladInferenceDimensions Dimensions { get; set; }

        /// <summary>
        /// HD 결과 JSON measurements 배열을 보존한 측정부별 결과입니다.
        /// 기존 DetectText는 하위 호환용 변환값이고, 신규 기능은 이 목록을 우선 사용합니다.
        /// </summary>
        public IList<VladInferenceMeasurement> Measurements { get; private set; }

        /// <summary>
        /// 이미지 또는 측정부 NG의 기계 판독용 원인 코드 목록입니다.
        /// </summary>
        public IList<string> FailureReasons { get; private set; }

        public IList<VladDetection> Detections { get; private set; }

        /// <summary>
        /// VLAD_Custom_InferenceData_V1이 TLV 버퍼에 채운 커스텀 검출 정보입니다.
        /// 네이티브 포인터 해제 전에 관리 메모리로 복사한 값만 보관합니다.
        /// </summary>
        public IList<VladCustomInferenceInfo> CustomInfos { get; private set; }

        public int[] ClassCounts { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// 검사 결과 이미지 하단에 표시할 대략적인 W/D/H 정보입니다.
    /// </summary>
    public class VladInferenceDimensions
    {
        public decimal? Width { get; set; }

        public decimal? Depth { get; set; }

        public decimal? Height { get; set; }

        public string Unit { get; set; }
    }

    /// <summary>
    /// VLAD_HD_InferenceData_Result JSON의 measurements 배열 한 항목입니다.
    /// 값은 모두 mm 기준이며, DB 측정부 IndexNo와 연결합니다.
    /// </summary>
    public class VladInferenceMeasurement
    {
        public int MeasurementRegionId { get; set; }

        public int IndexNo { get; set; }

        public string ItemType { get; set; }

        public decimal MeasuredValue { get; set; }

        public decimal SpecValue { get; set; }

        public decimal ToleranceMin { get; set; }

        public decimal ToleranceMax { get; set; }

        public string Judge { get; set; }

        public string Unit { get; set; }
    }

    /// <summary>
    /// VLAD Custom_Info_Struct 1개를 C#에서 안전하게 보관하기 위한 관리 모델입니다.
    /// p1/p2는 검출 또는 측정부 표시 좌표로 사용됩니다.
    /// </summary>
    public class VladCustomInferenceInfo
    {
        public int ClassId { get; set; }

        public string ClassName { get; set; }

        public decimal Score { get; set; }

        public int X1 { get; set; }

        public int Y1 { get; set; }

        public int X2 { get; set; }

        public int Y2 { get; set; }
    }
}
