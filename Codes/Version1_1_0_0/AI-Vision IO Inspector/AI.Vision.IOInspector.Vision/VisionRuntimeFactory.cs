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
        private static readonly object StartupInitializationSyncRoot = new object();
        private static readonly VladSdkSession SharedVladSdkSession = new VladSdkSession();
        private static VladVisionSettings SharedSettings;
        private static VladCamModeRuntime SharedCamModeRuntime;
        private static VladRuntimeLifecycleService SharedRuntimeLifecycleService;
        private static VisionCameraService SharedCameraService;
        private static string SharedProjectRootPath;
        private static bool StartupInitializationInProgress;

        public static VladCamModeState InitializeVladRuntime(string applicationRootPath)
        {
            VladCamModeRuntime camModeRuntime = EnsureSharedCamModeRuntime(applicationRootPath);
            return camModeRuntime.EnsureLoaded();
        }

        public static void InitializeVladRuntimeOnStartup(string applicationRootPath)
        {
            InitializeVladRuntimeOnCurrentThread(applicationRootPath);
        }

        public static void BeginInitializeVladRuntimeOnStartup(string applicationRootPath)
        {
            string capturedRootPath = applicationRootPath;
            string logFilePath = BuildStartupLogFilePath(capturedRootPath);

            lock (StartupInitializationSyncRoot)
            {
                if (StartupInitializationInProgress)
                {
                    AppendStartupLog(logFilePath, "SCHEDULE_SKIPPED", "VLAD_Ops_Ai_Env_Start 초기화가 이미 진행 중이어서 중복 예약을 건너뜁니다.");
                    return;
                }

                if (SharedVladSdkSession.CurrentFullImageVladId != IntPtr.Zero &&
                    SharedVladSdkSession.CurrentCroppedImageVladId != IntPtr.Zero)
                {
                    AppendStartupLog(logFilePath, "SCHEDULE_SKIPPED", "전체 이미지와 Crop 이미지용 VLAD_ID가 이미 생성되어 있어 시작 초기화 예약을 건너뜁니다.");
                    return;
                }

                StartupInitializationInProgress = true;
            }

            AppendStartupLog(logFilePath, "SCHEDULED", "VLAD_Ops_Ai_Env_Start 초기화를 현재 스레드에서 시작합니다.");

            try
            {
                // VLAD 샘플은 Form 생성자에서 VLAD_Custom_Registration을 호출합니다.
                // 초기 등록은 네이티브 전역 상태를 만들기 때문에 백그라운드 Task가 아닌 시작 스레드에서 1회 수행합니다.
                InitializeVladRuntimeOnCurrentThread(capturedRootPath);
            }
            finally
            {
                lock (StartupInitializationSyncRoot)
                {
                    StartupInitializationInProgress = false;
                }
            }
        }

        public static void ShutdownVladRuntime(string applicationRootPath)
        {
            string logFilePath = BuildStartupLogFilePath(applicationRootPath);
            AppendStartupLog(logFilePath, "SHUTDOWN_START", "VLAD RTSP 캐시 무효화와 VLAD_Unregistration을 시작합니다.");

            try
            {
                lock (SharedRuntimeSyncRoot)
                {
                    if (SharedCameraService != null)
                    {
                        SharedCameraService.PrepareForVladRuntimeReload();
                    }
                    else
                    {
                        VLAD_Ops_RTSP.PrepareForVladRuntimeReload();
                    }
                }

                bool unregistered = SharedVladSdkSession.Unregister();
                AppendStartupLog(logFilePath, "SHUTDOWN_UNREGISTER", "VLAD_Unregistration 결과=" + unregistered.ToString());
            }
            catch (Exception ex)
            {
                AppendStartupLog(logFilePath, "SHUTDOWN_FAILED", "VLAD 종료 처리 실패: " + ex.ToString());
                Debug.WriteLine("VLAD 종료 처리 실패: " + ex.Message);
            }
            finally
            {
                lock (SharedRuntimeSyncRoot)
                {
                    SharedCameraService = null;
                    SharedCamModeRuntime = null;
                    SharedRuntimeLifecycleService = null;
                    SharedSettings = null;
                    SharedProjectRootPath = null;
                }

                AppendStartupLog(logFilePath, "SHUTDOWN_END", "VLAD 종료 처리 완료.");
            }
        }

        public static ICameraService CreateCameraService(string applicationRootPath)
        {
            lock (SharedRuntimeSyncRoot)
            {
                VladCamModeRuntime camModeRuntime = EnsureSharedCamModeRuntime(applicationRootPath);
                if (SharedCameraService == null)
                {
                    SharedCameraService = new VisionCameraService(applicationRootPath, camModeRuntime);
                    SharedRuntimeLifecycleService.AttachCameraService(SharedCameraService);
                }

                return SharedCameraService;
            }
        }

        public static IAiInferenceService CreateAiInferenceService(string applicationRootPath)
        {
            // 디버깅 단계에서는 VLAD SDK를 같은 WPF 프로세스 안에서 실행합니다.
            // 실제 추론은 VisionInferenceWorker 전용 스레드에서 수행하므로 UI 스레드는 직접 점유하지 않습니다.
            VladCamModeRuntime camModeRuntime = EnsureSharedCamModeRuntime(applicationRootPath);
            return new VisionAiInferenceService(
                new VladVisionInferenceEngine(
                    applicationRootPath,
                    camModeRuntime,
                    SharedRuntimeLifecycleService));
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
                    "VLAD_Ops_Ai_Env_Start 실행 완료. FullImageVladId=" +
                    state.FullImageVladId.ToString() +
                    ", CroppedImageVladId=" +
                    state.CroppedImageVladId.ToString() +
                    ", UsesSeparateNativeRegistrations=" +
                    state.UsesSeparateNativeRegistrations.ToString() +
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
                AppendStartupLog(
                    BuildStartupLogFilePath(applicationRootPath),
                    "CONFIG_LOADED",
                    "VLAD Config.json 로드. Path=" + SharedSettings.ConfigFilePath +
                    ", GPU_ID=" + SharedSettings.GpuId.ToString());
                SharedCamModeRuntime = new VladCamModeRuntime(SharedVladSdkSession, SharedSettings);
                SharedRuntimeLifecycleService = new VladRuntimeLifecycleService(SharedCamModeRuntime);
                SharedCameraService = null;
                SharedProjectRootPath = projectRootPath;
                return SharedCamModeRuntime;
            }
        }
    }
}
