using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.App.ViewModels;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Services;
using AI.Vision.IOInspector.Infrastructure.Repositories;
using AI.Vision.IOInspector.Infrastructure.Services;

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
            IPartRepository partRepository = new InMemoryPartRepository();
            IInspectionRepository inspectionRepository = new InMemoryInspectionRepository();
            ICameraService cameraService = new SimulatedCameraService();
            IAiInferenceService aiInferenceService = new SimulatedAiInferenceService();
            IFileStorageService fileStorageService = new SimulatedFileStorageService(applicationRootPath);
            IReferenceImageFileService referenceImageFileService = new LocalReferenceImageFileService(applicationRootPath);
            IFileDialogService fileDialogService = new WpfFileDialogService();

            MeasurementService measurementService = new MeasurementService();
            JudgmentService judgmentService = new JudgmentService();
            PartCatalogService partCatalogService = new PartCatalogService(partRepository);
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
                partCatalogService,
                inspectionWorkflowService,
                statisticsService,
                inspectionRepository,
                referenceImageFileService,
                fileDialogService);
        }
    }
}
