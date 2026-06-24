using System;
using System.IO;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_SDK가 실제로 로드할 수 있는 모델 폴더 구조인지 진단합니다.
    /// 원본 VLAD_Ops처럼 등록 흐름은 SDK에 맡기고, 이 클래스는 문제 원인을 로그로 남기는 데만 사용합니다.
    /// </summary>
    public static class VladModelPathInspector
    {
        public static string BuildDiagnosticMessage(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return string.Empty;
            }

            VladModelPathInspection inspection = Inspect(modelPath);
            if (!inspection.PathExists || inspection.IsLoadableCandidate)
            {
                return string.Empty;
            }

            if (inspection.HasCheckpointFiles)
            {
                return "VLAD MODEL 진단: 현재 경로에는 checkpoint 학습 산출물만 있고, VLAD_SDK가 추론용으로 직접 로드할 모델 파일은 확인되지 않았습니다.\r\n" +
                       "현재 경로: " + inspection.ModelPath + "\r\n" +
                       "확인된 파일: checkpoint/ckpt-*/pipeline.config\r\n" +
                       "원본 VLAD_SDK는 이 파일들을 생성하거나 SavedModel로 변환하지 않습니다. AI 담당자가 checkpoint를 추론 모델로 export한 뒤 " +
                       "nets_model.json + saved_model\\saved_model.pb 또는 model.onnx/model.pt/model.t7 구조를 MODEL 경로에 배치해야 합니다.";
            }

            return "VLAD MODEL 진단: 현재 경로에서 원본 VLAD_SDK가 로드할 수 있는 모델 파일을 찾지 못했습니다.\r\n" +
                   "현재 경로: " + inspection.ModelPath + "\r\n" +
                   "필요 구조: nets_model.json + saved_model\\saved_model.pb, 또는 model.onnx/model.pt/model.t7";
        }

        public static VladModelPathInspection Inspect(string modelPath)
        {
            VladModelPathInspection inspection = new VladModelPathInspection();
            inspection.ModelPath = modelPath ?? string.Empty;

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return inspection;
            }

            string fullPath = Path.GetFullPath(modelPath);
            inspection.ModelPath = fullPath;
            inspection.PathExists = Directory.Exists(fullPath);
            if (!inspection.PathExists)
            {
                return inspection;
            }

            string savedModelPath = fullPath.EndsWith("saved_model", StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : Path.Combine(fullPath, "saved_model");

            DirectoryInfo savedModelParent = Directory.GetParent(savedModelPath);
            string savedModelParentPath = savedModelParent == null ? fullPath : savedModelParent.FullName;

            inspection.HasSavedModelPb = File.Exists(Path.Combine(savedModelPath, "saved_model.pb")) ||
                                         File.Exists(Path.Combine(fullPath, "saved_model.pb"));
            inspection.HasNetsModelJson = File.Exists(Path.Combine(fullPath, "nets_model.json")) ||
                                          File.Exists(Path.Combine(savedModelParentPath, "nets_model.json"));
            inspection.HasOnnxModel = File.Exists(Path.Combine(fullPath, "model.onnx")) ||
                                      Directory.GetFiles(fullPath, "*.onnx", SearchOption.TopDirectoryOnly).Length > 0;
            inspection.HasPyTorchModel = File.Exists(Path.Combine(fullPath, "model.pt")) ||
                                         File.Exists(Path.Combine(fullPath, "model.t7")) ||
                                         Directory.GetFiles(fullPath, "*.pt", SearchOption.TopDirectoryOnly).Length > 0 ||
                                         Directory.GetFiles(fullPath, "*.t7", SearchOption.TopDirectoryOnly).Length > 0;
            inspection.HasCheckpointFiles = File.Exists(Path.Combine(fullPath, "checkpoint")) ||
                                            File.Exists(Path.Combine(fullPath, "pipeline.config")) ||
                                            Directory.GetFiles(fullPath, "*.index", SearchOption.TopDirectoryOnly).Length > 0 ||
                                            Directory.GetFiles(fullPath, "*.data-*", SearchOption.TopDirectoryOnly).Length > 0;

            inspection.IsLoadableCandidate = (inspection.HasSavedModelPb && inspection.HasNetsModelJson) ||
                                             inspection.HasOnnxModel ||
                                             inspection.HasPyTorchModel;
            return inspection;
        }
    }

    public class VladModelPathInspection
    {
        public string ModelPath { get; set; }

        public bool PathExists { get; set; }

        public bool HasSavedModelPb { get; set; }

        public bool HasNetsModelJson { get; set; }

        public bool HasOnnxModel { get; set; }

        public bool HasPyTorchModel { get; set; }

        public bool HasCheckpointFiles { get; set; }

        public bool IsLoadableCandidate { get; set; }
    }
}
