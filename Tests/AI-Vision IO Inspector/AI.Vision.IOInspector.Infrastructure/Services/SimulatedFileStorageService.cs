using System;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 검사 결과를 텍스트 파일로 남기는 개발용 저장 서비스입니다.
    /// 실제 운영에서는 이미지 원본, NG 이미지, 로그 저장 정책을 이 클래스 계열에서 구현합니다.
    /// </summary>
    public class SimulatedFileStorageService : IFileStorageService
    {
        private readonly string _rootPath;

        public SimulatedFileStorageService(string rootPath)
        {
            _rootPath = rootPath;
        }

        public void StoreInspection(Inspection inspection)
        {
            string dayPath = Path.Combine(_rootPath, "RuntimeData", "Inspections", DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dayPath);

            string filePath = Path.Combine(dayPath, "Inspection_" + inspection.Id.ToString("0000") + ".txt");
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("InspectionId=" + inspection.Id);
                writer.WriteLine("PartNo=" + inspection.PartNo);
                writer.WriteLine("PartName=" + inspection.PartName);
                writer.WriteLine("Result=" + inspection.Result);
                writer.WriteLine("Message=" + inspection.ResultMessage);
                writer.WriteLine("ElapsedMs=" + inspection.ElapsedMilliseconds);

                foreach (MeasurementResult measurement in inspection.Measurements)
                {
                    writer.WriteLine(measurement.Name + ": " + measurement.MeasuredValue + measurement.Unit + " / " + measurement.Message);
                }
            }
        }
    }
}
