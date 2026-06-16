using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure;
using AI.Vision.IOInspector.Vision.Isolation;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// VLAD SDK 추론을 별도 프로세스에서 실행합니다.
    /// 네이티브 DLL이 프로세스를 종료시켜도 WPF 본체가 같이 종료되지 않도록 분리하는 보호 계층입니다.
    /// </summary>
    public class ProcessIsolatedAiInferenceService : IAiInferenceService
    {
        private const int WorkerTimeoutMilliseconds = 90000;
        private const string WorkerExecutableName = "AI.Vision.IOInspector.VisionWorker.exe";

        private readonly string _applicationRootPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProcessIsolatedAiInferenceService(string applicationRootPath)
        {
            _applicationRootPath = applicationRootPath;
            _jsonOptions = new JsonSerializerOptions();
            _jsonOptions.WriteIndented = true;
        }

        public AiInferenceResult Inspect(Part part, IList<CapturedImage> capturedImages)
        {
            string workerPath = LocateWorkerExecutable();
            if (string.IsNullOrWhiteSpace(workerPath))
            {
                return CreateFailureResult("VLAD 추론 워커 실행 파일을 찾을 수 없습니다. " + WorkerExecutableName);
            }

            string requestPath = Path.Combine(Path.GetTempPath(), "AI_Vision_Inference_Request_" + Guid.NewGuid().ToString("N") + ".json");
            string responsePath = Path.Combine(Path.GetTempPath(), "AI_Vision_Inference_Response_" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                WriteRequestFile(requestPath, part, capturedImages);
                WorkerExecutionResult executionResult = ExecuteWorker(workerPath, requestPath, responsePath);
                if (!executionResult.IsSuccess)
                {
                    return CreateFailureResult(executionResult.Message);
                }

                if (!File.Exists(responsePath))
                {
                    return CreateFailureResult("VLAD 추론 워커가 결과 파일을 생성하지 않았습니다.");
                }

                string responseJson = File.ReadAllText(responsePath, Encoding.UTF8);
                IsolatedInferenceResponse response = JsonSerializer.Deserialize<IsolatedInferenceResponse>(responseJson, _jsonOptions);
                if (response == null)
                {
                    return CreateFailureResult("VLAD 추론 워커 결과를 읽을 수 없습니다.");
                }

                return response.ToAiInferenceResult();
            }
            catch (Exception ex)
            {
                return CreateFailureResult("VLAD 추론 워커 실행 중 오류가 발생했습니다. " + ex.Message);
            }
            finally
            {
                DeleteTemporaryFile(requestPath);
                DeleteTemporaryFile(responsePath);
            }
        }

        private void WriteRequestFile(string requestPath, Part part, IList<CapturedImage> capturedImages)
        {
            IsolatedInferenceRequest request = IsolatedInferenceRequest.FromInspectionInput(_applicationRootPath, part, capturedImages);
            string json = JsonSerializer.Serialize(request, _jsonOptions);
            File.WriteAllText(requestPath, json, Encoding.UTF8);
        }

        private WorkerExecutionResult ExecuteWorker(string workerPath, string requestPath, string responsePath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = workerPath;
            startInfo.WorkingDirectory = Path.GetDirectoryName(workerPath);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.ArgumentList.Add(requestPath);
            startInfo.ArgumentList.Add(responsePath);

            Process process = new Process();
            process.StartInfo = startInfo;

            try
            {
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                bool exited = process.WaitForExit(WorkerTimeoutMilliseconds);
                if (!exited)
                {
                    TryKillProcess(process);
                    return WorkerExecutionResult.CreateFailure("VLAD 추론 워커가 제한 시간 안에 응답하지 않았습니다.");
                }

                string standardOutput = ReadTaskResult(outputTask);
                string standardError = ReadTaskResult(errorTask);
                if (process.ExitCode != 0)
                {
                    return WorkerExecutionResult.CreateFailure(BuildWorkerFailureMessage(process.ExitCode, standardOutput, standardError));
                }

                return WorkerExecutionResult.CreateSuccess();
            }
            finally
            {
                process.Dispose();
            }
        }

        private string LocateWorkerExecutable()
        {
            string applicationBasePath = AppContext.BaseDirectory;
            string dataRootPath = ProjectDataRootResolver.Resolve(_applicationRootPath);

            IList<string> candidates = new List<string>();
            candidates.Add(Path.Combine(applicationBasePath, "VisionWorker", WorkerExecutableName));
            candidates.Add(Path.Combine(applicationBasePath, WorkerExecutableName));
            candidates.Add(Path.Combine(dataRootPath, "AI.Vision.IOInspector.VisionWorker", "bin", "Debug", "net9.0", WorkerExecutableName));
            candidates.Add(Path.Combine(dataRootPath, "AI.Vision.IOInspector.VisionWorker", "bin", "Release", "net9.0", WorkerExecutableName));

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private string BuildWorkerFailureMessage(int exitCode, string standardOutput, string standardError)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("VLAD 추론 워커가 비정상 종료되었습니다. ExitCode=");
            builder.Append(exitCode.ToString());

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                builder.Append(" Error=");
                builder.Append(TrimMessage(standardError));
            }

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                builder.Append(" Output=");
                builder.Append(TrimMessage(standardOutput));
            }

            return builder.ToString();
        }

        private string TrimMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            string trimmed = message.Replace(Environment.NewLine, " ").Trim();
            if (trimmed.Length <= 500)
            {
                return trimmed;
            }

            return trimmed.Substring(0, 500);
        }

        private string ReadTaskResult(Task<string> task)
        {
            try
            {
                return task.Result;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void TryKillProcess(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }

        private void DeleteTemporaryFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private AiInferenceResult CreateFailureResult(string message)
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = false;
            result.IsMatched = false;
            result.PredictedClass = string.Empty;
            result.Confidence = 0m;
            result.Message = message;
            result.ModelVersion = "VLAD";
            return result;
        }

        private class WorkerExecutionResult
        {
            public bool IsSuccess { get; set; }

            public string Message { get; set; }

            public static WorkerExecutionResult CreateSuccess()
            {
                WorkerExecutionResult result = new WorkerExecutionResult();
                result.IsSuccess = true;
                result.Message = string.Empty;
                return result;
            }

            public static WorkerExecutionResult CreateFailure(string message)
            {
                WorkerExecutionResult result = new WorkerExecutionResult();
                result.IsSuccess = false;
                result.Message = message;
                return result;
            }
        }
    }
}
