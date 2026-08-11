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
        /// 구형 결과 JSON과 화면 표시를 위한 호환 상태입니다.
        /// 신규 HD 계약은 별도 상태를 반환하지 않으므로 정상 파싱 시 SUCCESS로 설정합니다.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// HD 결과 JSON이 반환한 품번입니다.
        /// </summary>
        public string PartNo { get; set; }

        /// <summary>
        /// 결과를 생성한 카메라 위치입니다. 예: Top, Front, Thickness.
        /// </summary>
        public string ViewName { get; set; }

        /// <summary>
        /// HD JSON의 카메라 위치 코드입니다. 1=Top, 2=Front, 3=Back, 4=Left, 5=Right, 6=Thickness입니다.
        /// </summary>
        public int ViewCode { get; set; }

        public int ValidDetectionCount { get; set; }

        public string DetectText { get; set; }              // 결과값 Data..

        /// <summary>
        /// HD DLL의 VLAD_HD_Inference_Mat이 반환한 원본 UTF-8 JSON입니다.
        /// 요청과 결과가 같은 버퍼를 공유하는 in-place 방식이므로 별도 조회 호출 없이 이 값을 그대로 씁니다.
        /// </summary>
        public string RawResultJson { get; set; }

        /// <summary>
        /// 기존 화면과 이력 처리 호환용 이미지 판정입니다.
        /// 신규 HD 계약에서는 ViewJudge의 PASS/FAIL 값을 사용합니다.
        /// </summary>
        public string ImageJudge { get; set; }

        /// <summary>
        /// HD 검사 결과 JSON의 측정부 기준값 비교 판정입니다.
        /// 측정부가 없는 View는 NOT_APPLICABLE 값을 사용합니다.
        /// </summary>
        public string MeasurementJudge { get; set; }

        /// <summary>
        /// 현재 카메라 View의 AI 판정입니다. PASS 또는 FAIL 값을 사용합니다.
        /// </summary>
        public string ViewJudge { get; set; }

        /// <summary>
        /// HD JSON의 현재 View 판정 코드입니다. 0=PASS, 1=FAIL입니다.
        /// </summary>
        public int ViewJudgeCode { get; set; }

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
        /// 구형 결과 JSON과의 호환을 위한 원인 코드 목록입니다.
        /// 신규 HD 계약에서는 별도 원인 배열을 반환하지 않습니다.
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
    /// VLAD_HD_Inference_Mat 결과 JSON의 measurements 배열 한 항목입니다.
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
