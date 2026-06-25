using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Infrastructure;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using AI.Vision.IOInspector.Vision.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AI.Vision.IOInspector.Vision
{
    /// <summary>
    /// Vision 프로젝트가 소유하는 카메라/AI 서비스 구현체를 생성합니다.
    /// 기존 VLAD_Ops와 같이 CAM 모드 초기화에서 VLAD_ID를 한 번 만들고 같은 런타임에서 재사용합니다.
    /// </summary>
    public static class VisionRuntimeFactory
    {
        private static readonly object SharedRuntimeSyncRoot = new object();
        private static readonly VladSdkSession SharedVladSdkSession = new VladSdkSession();
        private static VladVisionSettings SharedSettings;
        private static VladCamModeRuntime SharedCamModeRuntime;
        private static string SharedProjectRootPath;

        public static VladCamModeState InitializeVladRuntime(string applicationRootPath)
        {
            VladCamModeRuntime camModeRuntime = EnsureSharedCamModeRuntime(applicationRootPath);
            return camModeRuntime.EnsureLoaded();
        }

        public static void InitializeVladRuntimeOnStartup(string applicationRootPath)
        {
            InitializeVladRuntimeOnCurrentThread(applicationRootPath);
        }

        public static ICameraService CreateCameraService(string applicationRootPath)
        {
            VladCamModeRuntime camModeRuntime = EnsureSharedCamModeRuntime(applicationRootPath);
            return new VisionCameraService(applicationRootPath, camModeRuntime);
        }

        public static IAiInferenceService CreateAiInferenceService(string applicationRootPath)
        {
            // 디버깅 단계에서는 VLAD SDK를 같은 WPF 프로세스 안에서 실행합니다.
            // 실제 추론은 VisionInferenceWorker 전용 스레드에서 수행하므로 UI 스레드는 직접 점유하지 않습니다.
            VladCamModeRuntime camModeRuntime = EnsureSharedCamModeRuntime(applicationRootPath);
            return new VisionAiInferenceService(
                new VladVisionInferenceEngine(applicationRootPath, camModeRuntime),
                applicationRootPath);
        }

        private static void InitializeVladRuntimeOnCurrentThread(string applicationRootPath)
        {
            string logFilePath = BuildStartupLogFilePath(applicationRootPath);
            Environment.SetEnvironmentVariable(VLAD_Ops_Ai.RegistrationLogEnvironmentVariableName, logFilePath);
            AppendStartupLog(logFilePath, "START_REQUEST", "WPF 시작 UI 스레드에서 VLAD_Ops_Ai_Env_Start 초기화를 요청했습니다.");

            try
            {
                VladCamModeState state = InitializeVladRuntime(applicationRootPath);
                AppendStartupLog(
                    logFilePath,
                    "SUCCESS",
                    "VLAD_Ops_Ai_Env_Start 실행 완료. VladId=" +
                    state.VladId.ToString() +
                    ", ClassCount=" +
                    state.ClassCount.ToString());
            }
            catch (Exception ex)
            {
                AppendStartupLog(logFilePath, "FAILED", "WPF 시작 UI 스레드 VLAD 초기화 실패: " + ex.ToString());
                Debug.WriteLine("WPF 시작 UI 스레드 VLAD 초기화 실패: " + ex.Message);
            }
        }

        private static string BuildStartupLogFilePath(string applicationRootPath)
        {
            string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            string logDirectoryPath = Path.Combine(projectRootPath, "DB", "Logs");
            Directory.CreateDirectory(logDirectoryPath);
            return Path.Combine(logDirectoryPath, "vlad-startup.log");
        }

        private static void AppendStartupLog(string logFilePath, string status, string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                              " [" + status + "] " +
                              message +
                              Environment.NewLine;

                File.AppendAllText(logFilePath, line, Encoding.UTF8);
                Debug.WriteLine(line);
            }
            catch
            {
            }
        }

        private static VladCamModeRuntime EnsureSharedCamModeRuntime(string applicationRootPath)
        {
            lock (SharedRuntimeSyncRoot)
            {
                string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
                if (SharedCamModeRuntime != null &&
                    string.Equals(SharedProjectRootPath, projectRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    return SharedCamModeRuntime;
                }

                SharedSettings = VladVisionSettings.Load(applicationRootPath);
                SharedCamModeRuntime = new VladCamModeRuntime(SharedVladSdkSession, SharedSettings);
                SharedProjectRootPath = projectRootPath;
                return SharedCamModeRuntime;
            }
        }
    }
}
