using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AI.Vision.IOInspector.Infrastructure.Services;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.Isolation;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using AI.Vision.IOInspector.Vision.Models;
using AI.Vision.IOInspector.Vision.Services;

namespace AI.Vision.IOInspector.VisionWorker
{
    /// <summary>
    /// VLAD SDK 추론만 담당하는 별도 프로세스입니다.
    /// 네이티브 SDK가 비정상 종료되더라도 WPF 본체가 같이 종료되지 않도록 분리합니다.
    /// </summary>
    internal static class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Error.WriteLine("VLAD VisionWorker started.");

            if (args == null || args.Length < 2)
            {
                Console.Error.WriteLine("Usage: AI.Vision.IOInspector.VisionWorker.exe <request.json> <response.json>");
                return 2;
            }

            string requestPath = args[0];
            string responsePath = args[1];

            try
            {
                IsolatedInferenceRequest request = ReadRequest(requestPath);
                IsolatedInferenceResponse response = RunInference(request);
                WriteResponse(responsePath, response);
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    WriteResponse(responsePath, IsolatedInferenceResponse.CreateFailure("VLAD 추론 워커 오류: " + ex.Message));
                    return 0;
                }
                catch
                {
                    Console.Error.WriteLine(ex.ToString());
                    return 1;
                }
            }
        }

        private static IsolatedInferenceRequest ReadRequest(string requestPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath) || !File.Exists(requestPath))
            {
                throw new FileNotFoundException("추론 요청 파일을 찾을 수 없습니다.", requestPath);
            }

            string json = File.ReadAllText(requestPath, Encoding.UTF8);
            IsolatedInferenceRequest request = JsonSerializer.Deserialize<IsolatedInferenceRequest>(json, JsonOptions);
            if (request == null)
            {
                throw new InvalidOperationException("추론 요청 파일을 읽을 수 없습니다.");
            }

            return request;
        }

        private static IsolatedInferenceResponse RunInference(IsolatedInferenceRequest request)
        {
            string applicationRootPath = request.ApplicationRootPath;
            if (string.IsNullOrWhiteSpace(applicationRootPath))
            {
                applicationRootPath = AppContext.BaseDirectory;
            }

            NativeDependencyLoader.Configure(applicationRootPath);

            VladVisionSettings settings = VladVisionSettings.Load(applicationRootPath);
            VladSdkSession session = new VladSdkSession();
            IVisionInferenceEngine engine = new VladVisionInferenceEngine(applicationRootPath, session, settings);

            VisionInspectionInput input = request.ToVisionInspectionInput();
            VisionInspectionOutput output = engine.Inspect(input);
            return IsolatedInferenceResponse.FromVisionOutput(output);
        }

        private static void WriteResponse(string responsePath, IsolatedInferenceResponse response)
        {
            string responseDirectoryPath = Path.GetDirectoryName(responsePath);
            if (!string.IsNullOrWhiteSpace(responseDirectoryPath) && !Directory.Exists(responseDirectoryPath))
            {
                Directory.CreateDirectory(responseDirectoryPath);
            }

            string json = JsonSerializer.Serialize(response, JsonOptions);
            File.WriteAllText(responsePath, json, Encoding.UTF8);
        }

        private static JsonSerializerOptions BuildJsonOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.WriteIndented = true;
            return options;
        }
    }
}
