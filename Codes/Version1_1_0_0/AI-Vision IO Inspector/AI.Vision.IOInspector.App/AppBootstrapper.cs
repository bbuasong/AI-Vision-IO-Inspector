using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.App.Stores;
using AI.Vision.IOInspector.App.ViewModels;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Services;
using AI.Vision.IOInspector.Infrastructure.Repositories;
using AI.Vision.IOInspector.Infrastructure.Services;
using AI.Vision.IOInspector.Infrastructure.Services.Camera;
using AI.Vision.IOInspector.Infrastructure.Services.Ocr;
using AI.Vision.IOInspector.Infrastructure.Services.Retention;
using AI.Vision.IOInspector.Vision;
using AI.Vision.IOInspector.Vision.Services;
using AI.Vision.IOInspector.Infrastructure;
using System;
using System.IO;
using System.Text;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// 외부 DI 패키지 없이 Application, Infrastructure, ViewModel을 조립합니다.
    /// 실제 DB/SDK가 들어오면 여기에서 구현체만 교체하면 됩니다.
    /// </summary>
    public static class AppBootstrapper
    {
        public static MainWindowViewModel CreateMainWindowViewModel(string applicationRootPath)
        {
            AppendStartupTrace(applicationRootPath, "BOOTSTRAP_START");
            NativeDependencyLoader.Configure(applicationRootPath);
            AppendStartupTrace(applicationRootPath, "NATIVE_DEPENDENCY_CONFIGURED");
            VisionRuntimeFactory.BeginInitializeVladRuntimeOnStartup(applicationRootPath);
            AppendStartupTrace(applicationRootPath, "VLAD_INITIALIZE_SCHEDULED");

            SqliteDatabase sqliteDatabase = new SqliteDatabase(applicationRootPath);
            AppendStartupTrace(applicationRootPath, "SQLITE_INITIALIZED");
            IPartRepository partRepository = new SqlitePartRepository(sqliteDatabase);
            IInspectionRepository inspectionRepository = new SqliteInspectionRepository(sqliteDatabase);
            ICameraService cameraService = VisionRuntimeFactory.CreateCameraService(applicationRootPath);
            AppendStartupTrace(applicationRootPath, "CAMERA_SERVICE_CREATED");
            IAiInferenceService aiInferenceService = VisionRuntimeFactory.CreateAiInferenceService(applicationRootPath);
            AppendStartupTrace(applicationRootPath, "AI_SERVICE_CREATED");
            IReferenceImageSimilarityService referenceImageSimilarityService =
                aiInferenceService as IReferenceImageSimilarityService;
            IFileStorageService fileStorageService = new SimulatedFileStorageService(applicationRootPath);
            IReferenceImageFileService referenceImageFileService = new LocalReferenceImageFileService(applicationRootPath);
            IImageMergeService imageMergeService = new VladImageMergeService();
            IFileDialogService fileDialogService = new WpfFileDialogService();
            IMessageDialogService messageDialogService = new WpfMessageDialogService();
            IMeasurementPositionDialogService measurementPositionDialogService = new WpfMeasurementPositionDialogService();
            IReferenceCoordinateImageService referenceCoordinateImageService = new WpfReferenceCoordinateImageService();
            IReferenceImagePopupService referenceImagePopupService = new WpfReferenceImagePopupService();
            CameraConfigurationStore cameraConfigurationStore = new CameraConfigurationStore(applicationRootPath);
            InspectionDataRetentionSettingsStore retentionSettingsStore = new InspectionDataRetentionSettingsStore(applicationRootPath);
            InspectionDataRetentionService inspectionDataRetentionService = new InspectionDataRetentionService(applicationRootPath, inspectionRepository);
            IOcrScanService ocrScanService = new EpsonEsC320wOcrService(applicationRootPath);

            MeasurementService measurementService = new MeasurementService();
            JudgmentService judgmentService = new JudgmentService();
            PartCatalogService partCatalogService = new PartCatalogService(partRepository);
            PartDataStore partDataStore = new PartDataStore(partCatalogService);
            InspectionWorkflowService inspectionWorkflowService = new InspectionWorkflowService(
                partRepository,
                inspectionRepository,
                cameraService,
                aiInferenceService,
                fileStorageService,
                imageMergeService,
                measurementService,
                judgmentService);
            StatisticsService statisticsService = new StatisticsService(partRepository, inspectionRepository);

            AppendStartupTrace(applicationRootPath, "MAIN_VIEW_MODEL_CREATE_START");
            MainWindowViewModel viewModel = new MainWindowViewModel(
                partDataStore,
                inspectionWorkflowService,
                aiInferenceService,
                referenceImageSimilarityService,
                statisticsService,
                inspectionRepository,
                cameraService,
                referenceImageFileService,
                imageMergeService,
                fileDialogService,
                messageDialogService,
                measurementPositionDialogService,
                referenceCoordinateImageService,
                referenceImagePopupService,
                cameraConfigurationStore,
                retentionSettingsStore,
                inspectionDataRetentionService,
                ocrScanService);
            AppendStartupTrace(applicationRootPath, "MAIN_VIEW_MODEL_CREATE_END");
            return viewModel;
        }

        internal static void AppendStartupTrace(string applicationRootPath, string step)
        {
            try
            {
                string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
                string logDirectoryPath = Path.Combine(projectRootPath, "DB", "Logs");
                Directory.CreateDirectory(logDirectoryPath);
                string logFilePath = Path.Combine(logDirectoryPath, "app-startup.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                              " " +
                              step +
                              Environment.NewLine;
                File.AppendAllText(logFilePath, line, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
