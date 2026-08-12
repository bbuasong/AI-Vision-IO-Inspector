using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Repositories
{
    /// <summary>
    /// 검사 이력을 로컬 HDD의 JSON 파일로 저장합니다.
    /// 저장 위치는 RuntimeData\InspectionHistory이며 날짜 폴더 기준으로 보관/삭제 정책을 적용합니다.
    /// </summary>
    public class LocalInspectionRepository : IInspectionRepository
    {
        private readonly string _historyRootPath;
        private readonly InspectionHistoryRetentionOptions _retentionOptions;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IList<Inspection> _inspections;
        private int _nextId;

        public LocalInspectionRepository(string applicationRootPath)
            : this(applicationRootPath, new InspectionHistoryRetentionOptions())
        {
        }

        public LocalInspectionRepository(string applicationRootPath, InspectionHistoryRetentionOptions retentionOptions)
        {
            _historyRootPath = Path.Combine(applicationRootPath, "RuntimeData", "InspectionHistory");
            _retentionOptions = retentionOptions;
            _jsonOptions = new JsonSerializerOptions();
            _jsonOptions.WriteIndented = true;

            Directory.CreateDirectory(_historyRootPath);
            ApplyRetentionPolicy();
            _inspections = LoadInspections();
            _nextId = ResolveNextId();
        }

        public IList<Inspection> GetAll()
        {
            List<Inspection> sortedInspections = new List<Inspection>();
            foreach (Inspection inspection in _inspections)
            {
                sortedInspections.Add(inspection);
            }

            sortedInspections.Sort(CompareInspectionDescending);
            return sortedInspections;
        }

        public void Save(Inspection inspection)
        {
            Inspection existing = FindInspectionById(inspection.Id);
            if (existing != null)
            {
                _inspections.Remove(existing);
            }

            _inspections.Add(inspection);
            SaveInspectionFile(inspection);
            ApplyRetentionPolicy();
            RemoveDeletedFileItems();
        }

        public int GetNextId()
        {
            int id = _nextId;
            _nextId++;
            return id;
        }

        public DateTime? GetOldestInspectedAt()
        {
            if (_inspections.Count == 0)
            {
                return null;
            }

            DateTime oldest = DateTime.MaxValue;
            foreach (Inspection inspection in _inspections)
            {
                if (inspection.InspectedAt < oldest)
                {
                    oldest = inspection.InspectedAt;
                }
            }

            return oldest == DateTime.MaxValue ? (DateTime?)null : oldest;
        }

        public int DeleteInspectionsBefore(DateTime cutoffExclusive)
        {
            int deleteCount = 0;
            foreach (string filePath in Directory.GetFiles(_historyRootPath, "*.json", SearchOption.AllDirectories))
            {
                Inspection inspection = LoadInspectionFile(filePath);
                if (inspection == null || inspection.InspectedAt >= cutoffExclusive)
                {
                    continue;
                }

                try
                {
                    File.Delete(filePath);
                    deleteCount++;
                }
                catch
                {
                    // 개별 파일 삭제 실패가 전체 정리 흐름을 막지 않도록 합니다.
                }
            }

            RemoveDeletedFileItems();
            DeleteEmptyDirectories(new DirectoryInfo(_historyRootPath));
            return deleteCount;
        }

        private Inspection FindInspectionById(int id)
        {
            foreach (Inspection inspection in _inspections)
            {
                if (inspection.Id == id)
                {
                    return inspection;
                }
            }

            return null;
        }

        private int CompareInspectionDescending(Inspection left, Inspection right)
        {
            return right.InspectedAt.CompareTo(left.InspectedAt);
        }

        private IList<Inspection> LoadInspections()
        {
            IList<Inspection> inspections = new List<Inspection>();
            foreach (string filePath in Directory.GetFiles(_historyRootPath, "*.json", SearchOption.AllDirectories))
            {
                Inspection inspection = LoadInspectionFile(filePath);
                if (inspection != null)
                {
                    inspections.Add(inspection);
                }
            }

            return inspections;
        }

        private Inspection LoadInspectionFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                InspectionFileDto dto = JsonSerializer.Deserialize<InspectionFileDto>(json, _jsonOptions);
                if (dto == null)
                {
                    return null;
                }

                return ConvertToInspection(dto);
            }
            catch
            {
                return null;
            }
        }

        private int ResolveNextId()
        {
            int maxId = 0;
            foreach (Inspection inspection in _inspections)
            {
                if (inspection.Id > maxId)
                {
                    maxId = inspection.Id;
                }
            }

            return maxId + 1;
        }

        private void SaveInspectionFile(Inspection inspection)
        {
            string dayPath = Path.Combine(_historyRootPath, inspection.InspectedAt.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dayPath);

            string filePath = Path.Combine(dayPath, "Inspection_" + inspection.Id.ToString("000000") + ".json");
            InspectionFileDto dto = ConvertToDto(inspection);
            string json = JsonSerializer.Serialize(dto, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        private void ApplyRetentionPolicy()
        {
            DeleteExpiredDayFolders();
            DeleteOldestDayFoldersUntilFreeSpaceIsEnough();
        }

        private void DeleteExpiredDayFolders()
        {
            if (_retentionOptions.RetentionDays <= 0)
            {
                return;
            }

            DateTime cutoffDate = DateTime.Now.Date.AddDays(-_retentionOptions.RetentionDays);
            foreach (DirectoryInfo dayFolder in GetDayFolders())
            {
                DateTime folderDate;
                if (DateTime.TryParseExact(dayFolder.Name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out folderDate))
                {
                    if (folderDate < cutoffDate)
                    {
                        DeleteDirectory(dayFolder);
                    }
                }
            }
        }

        private void DeleteOldestDayFoldersUntilFreeSpaceIsEnough()
        {
            if (_retentionOptions.MinimumFreeSpaceBytes <= 0)
            {
                return;
            }

            DriveInfo drive = new DriveInfo(Path.GetPathRoot(_historyRootPath));
            while (drive.AvailableFreeSpace < _retentionOptions.MinimumFreeSpaceBytes)
            {
                DirectoryInfo oldestFolder = GetOldestDayFolder();
                if (oldestFolder == null)
                {
                    return;
                }

                DeleteDirectory(oldestFolder);
                drive = new DriveInfo(Path.GetPathRoot(_historyRootPath));
            }
        }

        private IEnumerable<DirectoryInfo> GetDayFolders()
        {
            DirectoryInfo root = new DirectoryInfo(_historyRootPath);
            if (!root.Exists)
            {
                return new List<DirectoryInfo>();
            }

            return root.GetDirectories();
        }

        private DirectoryInfo GetOldestDayFolder()
        {
            DirectoryInfo oldestFolder = null;
            foreach (DirectoryInfo dayFolder in GetDayFolders())
            {
                if (oldestFolder == null || string.Compare(dayFolder.Name, oldestFolder.Name, StringComparison.Ordinal) < 0)
                {
                    oldestFolder = dayFolder;
                }
            }

            return oldestFolder;
        }

        private void DeleteDirectory(DirectoryInfo directory)
        {
            try
            {
                directory.Delete(true);
            }
            catch
            {
                // 삭제 실패는 검사 흐름을 막지 않습니다. 운영 로그 저장이 확정되면 이 예외를 별도 Event로 기록합니다.
            }
        }

        private void DeleteEmptyDirectories(DirectoryInfo directory)
        {
            if (directory == null || !directory.Exists)
            {
                return;
            }

            foreach (DirectoryInfo child in directory.GetDirectories())
            {
                DeleteEmptyDirectories(child);
            }

            if (string.Equals(directory.FullName, _historyRootPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                if (directory.GetFiles().Length == 0 && directory.GetDirectories().Length == 0)
                {
                    directory.Delete();
                }
            }
            catch
            {
            }
        }

        private void RemoveDeletedFileItems()
        {
            IList<Inspection> existingItems = LoadInspections();
            _inspections.Clear();
            foreach (Inspection inspection in existingItems)
            {
                _inspections.Add(inspection);
            }
        }

        private InspectionFileDto ConvertToDto(Inspection inspection)
        {
            InspectionFileDto dto = new InspectionFileDto();
            dto.Id = inspection.Id;
            dto.PartNo = inspection.PartNo;
            dto.PartName = inspection.PartName;
            dto.CategoryCode = inspection.CategoryCode;
            dto.CategoryDescription = inspection.CategoryDescription;
            dto.PartType = inspection.PartType;
            dto.InputCode = inspection.InputCode;
            dto.Result = inspection.Result;
            dto.InspectedAt = inspection.InspectedAt;
            dto.ElapsedMilliseconds = inspection.ElapsedMilliseconds;
            dto.ResultMessage = inspection.ResultMessage;
            dto.Measurements = new List<MeasurementResultDto>();
            dto.Images = new List<CapturedImageDto>();
            dto.Events = new List<EventLogEntryDto>();

            foreach (MeasurementResult measurement in inspection.Measurements)
            {
                MeasurementResultDto measurementDto = new MeasurementResultDto();
                measurementDto.MeasurementRegionId = measurement.MeasurementRegionId;
                measurementDto.Name = measurement.Name;
                measurementDto.NominalValue = measurement.NominalValue;
                measurementDto.MeasuredValue = measurement.MeasuredValue;
                measurementDto.ToleranceMin = measurement.ToleranceMin;
                measurementDto.ToleranceMax = measurement.ToleranceMax;
                measurementDto.Unit = measurement.Unit;
                measurementDto.IsOk = measurement.IsPass;
                measurementDto.Message = measurement.Message;
                dto.Measurements.Add(measurementDto);
            }

            foreach (CapturedImage image in inspection.Images)
            {
                CapturedImageDto imageDto = new CapturedImageDto();
                imageDto.ViewType = image.ViewType;
                imageDto.DisplayName = image.DisplayName;
                imageDto.FilePath = image.FilePath;
                imageDto.CapturedAt = image.CapturedAt;
                dto.Images.Add(imageDto);
            }

            foreach (EventLogEntry entry in inspection.Events)
            {
                EventLogEntryDto entryDto = new EventLogEntryDto();
                entryDto.Severity = entry.Severity;
                entryDto.Source = entry.Source;
                entryDto.Message = entry.Message;
                entryDto.CreatedAt = entry.CreatedAt;
                dto.Events.Add(entryDto);
            }

            return dto;
        }

        private Inspection ConvertToInspection(InspectionFileDto dto)
        {
            Inspection inspection = new Inspection();
            inspection.Id = dto.Id;
            inspection.PartNo = dto.PartNo;
            inspection.PartName = dto.PartName;
            inspection.CategoryCode = dto.CategoryCode;
            inspection.CategoryDescription = dto.CategoryDescription;
            inspection.PartType = dto.PartType;
            inspection.InputCode = dto.InputCode;
            inspection.Result = dto.Result;
            inspection.InspectedAt = dto.InspectedAt == DateTime.MinValue ? DateTime.Now : dto.InspectedAt;
            inspection.ElapsedMilliseconds = dto.ElapsedMilliseconds;
            inspection.ResultMessage = dto.ResultMessage;

            if (dto.Measurements != null)
            {
                foreach (MeasurementResultDto measurementDto in dto.Measurements)
                {
                    MeasurementResult measurement = new MeasurementResult();
                    measurement.MeasurementRegionId = measurementDto.MeasurementRegionId;
                    measurement.Name = measurementDto.Name;
                    measurement.NominalValue = measurementDto.NominalValue;
                    measurement.MeasuredValue = measurementDto.MeasuredValue;
                    measurement.ToleranceMin = measurementDto.ToleranceMin;
                    measurement.ToleranceMax = measurementDto.ToleranceMax;
                    measurement.Unit = measurementDto.Unit;
                    measurement.IsPass = measurementDto.IsOk;
                    measurement.Message = measurementDto.Message;
                    inspection.Measurements.Add(measurement);
                }
            }

            if (dto.Images != null)
            {
                foreach (CapturedImageDto imageDto in dto.Images)
                {
                    CapturedImage image = new CapturedImage();
                    image.ViewType = imageDto.ViewType;
                    image.DisplayName = imageDto.DisplayName;
                    image.FilePath = imageDto.FilePath;
                    image.CapturedAt = imageDto.CapturedAt;
                    inspection.Images.Add(image);
                }
            }

            if (dto.Events != null)
            {
                foreach (EventLogEntryDto entryDto in dto.Events)
                {
                    EventLogEntry entry = new EventLogEntry();
                    entry.Severity = entryDto.Severity;
                    entry.Source = entryDto.Source;
                    entry.Message = entryDto.Message;
                    entry.CreatedAt = entryDto.CreatedAt;
                    inspection.Events.Add(entry);
                }
            }

            return inspection;
        }

        private class InspectionFileDto
        {
            public int Id { get; set; }

            public string PartNo { get; set; }

            public string PartName { get; set; }

            public string CategoryCode { get; set; }

            public string CategoryDescription { get; set; }

            public string PartType { get; set; }

            public string InputCode { get; set; }

            public InspectionResult Result { get; set; }

            public DateTime InspectedAt { get; set; }

            public decimal ElapsedMilliseconds { get; set; }

            public string ResultMessage { get; set; }

            public List<MeasurementResultDto> Measurements { get; set; }

            public List<CapturedImageDto> Images { get; set; }

            public List<EventLogEntryDto> Events { get; set; }
        }

        private class MeasurementResultDto
        {
            public int MeasurementRegionId { get; set; }

            public string Name { get; set; }

            public decimal NominalValue { get; set; }

            public decimal MeasuredValue { get; set; }

            public decimal ToleranceMin { get; set; }

            public decimal ToleranceMax { get; set; }

            public string Unit { get; set; }

            /// <summary>
            /// 도메인에서는 2026-08-12에 IsPass로 이름을 바꿨지만, 이 DTO는 기존 로컬 이력 JSON 파일을
            /// 그대로 읽어야 하므로 저장 시점의 이름 IsOk를 유지합니다. 이름을 바꾸면 이전 파일의
            /// 측정부 판정이 모두 false로 읽힙니다.
            /// </summary>
            public bool IsOk { get; set; }

            public string Message { get; set; }
        }

        private class CapturedImageDto
        {
            public ImageViewType ViewType { get; set; }

            public string DisplayName { get; set; }

            public string FilePath { get; set; }

            public DateTime CapturedAt { get; set; }
        }

        private class EventLogEntryDto
        {
            public EventSeverity Severity { get; set; }

            public string Source { get; set; }

            public string Message { get; set; }

            public DateTime CreatedAt { get; set; }
        }
    }
}
