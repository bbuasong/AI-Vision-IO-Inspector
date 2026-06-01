using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.App.Stores;
using AI.Vision.IOInspector.App.ViewModels;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Services;
using AI.Vision.IOInspector.Infrastructure.Repositories;
using AI.Vision.IOInspector.Infrastructure.Services;
using AI.Vision.IOInspector.Vision;

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
            NativeDependencyLoader.Configure(applicationRootPath);

            SqliteDatabase sqliteDatabase = new SqliteDatabase(applicationRootPath);
            IPartRepository partRepository = new SqlitePartRepository(sqliteDatabase);
            IInspectionRepository inspectionRepository = new SqliteInspectionRepository(sqliteDatabase);
            ICameraService cameraService = VisionRuntimeFactory.CreateCameraService(applicationRootPath);
            IAiInferenceService aiInferenceService = VisionRuntimeFactory.CreateAiInferenceService(applicationRootPath);
            IFileStorageService fileStorageService = new SimulatedFileStorageService(applicationRootPath);
            IReferenceImageFileService referenceImageFileService = new LocalReferenceImageFileService(applicationRootPath);
            IFileDialogService fileDialogService = new WpfFileDialogService();

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
                measurementService,
                judgmentService);
            StatisticsService statisticsService = new StatisticsService(partRepository, inspectionRepository);

            return new MainWindowViewModel(
                partDataStore,
                inspectionWorkflowService,
                statisticsService,
                inspectionRepository,
                cameraService,
                referenceImageFileService,
                fileDialogService);
        }
    }
}
