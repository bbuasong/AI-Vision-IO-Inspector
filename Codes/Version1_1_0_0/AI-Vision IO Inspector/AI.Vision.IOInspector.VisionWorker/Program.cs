using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure;
using AI.Vision.IOInspector.Infrastructure.Services;
using AI.Vision.IOInspector.Infrastructure.Services.Camera;
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
        private const int StartupPreflightSkippedExitCode = 3;
        private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("VLAD VisionWorker started.");

            if (args != null && args.Length > 0 && string.Equals(args[0], "--initialize", StringComparison.OrdinalIgnoreCase))
            {
                return RunStartupInitialization(args);
            }

            if (args != null && args.Length > 0 && string.Equals(args[0], "--test-config-rtsp", StringComparison.OrdinalIgnoreCase))
            {
                return RunConfigRtspTest(args);
            }

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

        private static int RunConfigRtspTest(string[] args)
        {
            string applicationRootPath = args.Length > 1 ? args[1] : AppContext.BaseDirectory;

            try
            {
                NativeDependencyLoader.Configure(applicationRootPath);

                CameraConfigurationStore configurationStore = new CameraConfigurationStore(applicationRootPath);
                IList<CameraChannelConfig> channels = configurationStore.Load();
                CameraChannelConfig rtspChannel = FindFirstRtspChannel(channels);
                if (rtspChannel == null)
                {
                    Console.WriteLine("CONFIG_RTSP_RESULT=NO_RTSP_CHANNEL");
                    Console.WriteLine("Config.json CAMS 안에서 사용 가능한 RTSP 채널을 찾지 못했습니다.");
                    return 4;
                }

                string rtspUrl = RtspUrlBuilder.Build(rtspChannel);
                Console.WriteLine("CONFIG_RTSP_CHANNEL=" + rtspChannel.DisplayName + " / " + rtspChannel.ViewType.ToString());
                Console.WriteLine("CONFIG_RTSP_URL=" + rtspUrl);

                ConfiguredCameraService cameraService = new ConfiguredCameraService(applicationRootPath);
                CameraChannelStatus status = cameraService.TestChannelConnection(rtspChannel.ViewType);

                Console.WriteLine("CONFIG_RTSP_CONNECTED=" + status.IsConnected.ToString());
                Console.WriteLine("CONFIG_RTSP_MESSAGE=" + status.Message);
                if (!string.IsNullOrWhiteSpace(status.LastFramePath))
                {
                    Console.WriteLine("CONFIG_RTSP_LAST_FRAME=" + status.LastFramePath);
                }

                return status.IsConnected ? 0 : 5;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static CameraChannelConfig FindFirstRtspChannel(IList<CameraChannelConfig> channels)
        {
            if (channels == null)
            {
                return null;
            }

            foreach (CameraChannelConfig channel in channels)
            {
                if (IsRtspChannel(channel))
                {
                    return channel;
                }
            }

            return null;
        }

        private static bool IsRtspChannel(CameraChannelConfig channel)
        {
            if (channel == null || !channel.IsEnabled)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(channel.RtspUrl))
            {
                return true;
            }

            return channel.ConnectionType == CameraConnectionType.Rtsp ||
                   channel.ConnectionType == CameraConnectionType.NvrRtsp;
        }

        private static int RunStartupInitialization(string[] args)
        {
            string applicationRootPath = args.Length > 1 ? args[1] : AppContext.BaseDirectory;
            string logFilePath = args.Length > 2 ? args[2] : string.Empty;

            try
            {
                ConfigureRegistrationLog(logFilePath);
                AppendStartupLog(logFilePath, "WORKER_START", "VLAD 시작 초기화 Worker가 실행되었습니다.");

                NativeDependencyLoader.Configure(applicationRootPath);

                VladVisionSettings settings = VladVisionSettings.Load(applicationRootPath);
                VladRuntimePreflightResult preflight = VladRuntimePreflight.Inspect(settings);

                AppendStartupLog(logFilePath, "SETTINGS", "MODEL=" + settings.ModelPath + ", GPU=" + settings.GpuId.ToString() + ", SITE=" + settings.SiteName);
                AppendStartupLog(logFilePath, "DEPENDENCY", VladRuntimePreflight.BuildCudaDependencyMessage(preflight));


                string modelDiagnostic = VladModelPathInspector.BuildDiagnosticMessage(settings.ModelPath);
                if (!string.IsNullOrWhiteSpace(modelDiagnostic))
                {
                    AppendStartupLog(logFilePath, "MODEL_DIAGNOSTIC", modelDiagnostic);
                }

                if (!preflight.CanCallNative)
                {
                    AppendStartupLog(logFilePath, "REGISTRATION_NOT_STARTED", "VLAD_Ops_Ai_Env_Start 및 VLAD_Custom_Registration은 호출되지 않았습니다.");
                    AppendStartupLog(logFilePath, "WORKER_SKIPPED", preflight.BuildBlockingMessage());
                    return StartupPreflightSkippedExitCode;
                }

                VladSdkSession session = new VladSdkSession();
                VladCamModeRuntime camModeRuntime = new VladCamModeRuntime(session, settings);
                VladCamModeState state = camModeRuntime.EnsureLoaded();

                AppendStartupLog(
                    logFilePath,
                    "WORKER_SUCCESS",
                    "VLAD_Ops_Ai_Env_Start 실행 완료. VladId=" +
                    state.VladId.ToString() +
                    ", ClassCount=" +
                    state.ClassCount.ToString());

                return 0;
            }
            catch (Exception ex)
            {
                AppendStartupLog(logFilePath, "WORKER_FAILED", ex.ToString());
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static void AppendStartupLog(string logFilePath, string status, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logFilePath))
                {
                    return;
                }

                string directoryPath = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                              " [" + status + "] " +
                              message +
                              Environment.NewLine;

                File.AppendAllText(logFilePath, line, Encoding.UTF8);
            }
            catch
            {
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
            ConfigureRegistrationLog(BuildRegistrationLogFilePath(applicationRootPath));

            VladVisionSettings settings = VladVisionSettings.Load(applicationRootPath);
            VladRuntimePreflightResult preflight = VladRuntimePreflight.Inspect(settings);
            if (!preflight.CanCallNative)
            {
                AppendWorkerLog(
                    BuildRegistrationLogFilePath(applicationRootPath),
                    "REGISTRATION_NOT_STARTED",
                    "VLAD_Ops_Ai_Env_Start 및 VLAD_Custom_Registration은 호출되지 않았습니다. " + preflight.BuildBlockingMessage());

                VisionInspectionOutput failureOutput = new VisionInspectionOutput();
                failureOutput.IsSuccess = false;
                failureOutput.IsMatched = false;
                failureOutput.PredictedClass = string.Empty;
                failureOutput.Confidence = 0m;
                failureOutput.Message = preflight.BuildBlockingMessage();
                failureOutput.ModelVersion = "VLAD";
                return IsolatedInferenceResponse.FromVisionOutput(failureOutput);
            }

            VladSdkSession session = new VladSdkSession();
            VladCamModeRuntime camModeRuntime = new VladCamModeRuntime(session, settings);
            VladRuntimeLifecycleService runtimeLifecycleService = new VladRuntimeLifecycleService(camModeRuntime);
            IVisionInferenceEngine engine = new VladVisionInferenceEngine(
                applicationRootPath,
                camModeRuntime,
                runtimeLifecycleService);

            VisionInspectionInput input = request.ToVisionInspectionInput();
            VisionInspectionOutput output = engine.Inspect(input);
            return IsolatedInferenceResponse.FromVisionOutput(output);
        }

        private static void ConfigureRegistrationLog(string logFilePath)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                return;
            }

            Environment.SetEnvironmentVariable(VLAD_Ops_Ai.RegistrationLogEnvironmentVariableName, logFilePath);
        }

        private static string BuildRegistrationLogFilePath(string applicationRootPath)
        {
            string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            string logDirectoryPath = Path.Combine(projectRootPath, "DB", "Logs");
            Directory.CreateDirectory(logDirectoryPath);
            return Path.Combine(logDirectoryPath, "vlad-registration.log");
        }

        private static void AppendWorkerLog(string logFilePath, string status, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logFilePath))
                {
                    return;
                }

                string directoryPath = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                              " [" + status + "] " +
                              message +
                              Environment.NewLine;

                File.AppendAllText(logFilePath, line, Encoding.UTF8);
            }
            catch
            {
            }
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
