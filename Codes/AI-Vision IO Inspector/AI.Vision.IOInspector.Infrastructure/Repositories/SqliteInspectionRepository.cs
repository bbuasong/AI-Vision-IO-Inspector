using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using Microsoft.Data.Sqlite;

namespace AI.Vision.IOInspector.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite DataBase.db의 History_* 테이블을 사용해 검사 이력을 저장합니다.
    /// </summary>
    public class SqliteInspectionRepository : IInspectionRepository
    {
        private readonly SqliteDatabase _database;
        private readonly InspectionHistoryRetentionOptions _retentionOptions;

        public SqliteInspectionRepository(SqliteDatabase database)
            : this(database, new InspectionHistoryRetentionOptions())
        {
        }

        public SqliteInspectionRepository(SqliteDatabase database, InspectionHistoryRetentionOptions retentionOptions)
        {
            _database = database;
            _retentionOptions = retentionOptions;
        }

        public IList<Inspection> GetAll()
        {
            Dictionary<int, Inspection> inspectionMap = new Dictionary<int, Inspection>();
            IList<Inspection> inspections = new List<Inspection>();
            using (SqliteConnection connection = _database.OpenConnection())
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT id, part_no, part_name, category_code, category_description, memo, input_code, result, inspected_at, elapsed_ms, result_message, ai_score, ai_score_threshold, has_ai_score " +
                        "FROM History_Inspections ORDER BY inspected_at DESC, id DESC;";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Inspection inspection = ReadInspection(reader);
                            inspections.Add(inspection);
                            inspectionMap[inspection.Id] = inspection;
                        }
                    }
                }

                LoadMeasurements(connection, inspectionMap);
                LoadCapturedImages(connection, inspectionMap);
                LoadEvents(connection, inspectionMap);
            }

            return inspections;
        }

        public void Save(Inspection inspection)
        {
            if (inspection == null)
            {
                return;
            }

            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                SaveInspectionHeader(connection, transaction, inspection);
                DeleteInspectionChildren(connection, transaction, inspection.Id);
                SaveMeasurements(connection, transaction, inspection);
                SaveCapturedImages(connection, transaction, inspection);
                SaveEvents(connection, transaction, inspection);
                transaction.Commit();
            }

            ApplyRetentionPolicy();
        }

        public int GetNextId()
        {
            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT IFNULL(MAX(id), 0) + 1 FROM History_Inspections;";
                object value = command.ExecuteScalar();
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }

        public DateTime? GetOldestInspectedAt()
        {
            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT inspected_at FROM History_Inspections ORDER BY inspected_at ASC, id ASC LIMIT 1;";
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return null;
                }

                DateTime parsed;
                if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), null, DateTimeStyles.RoundtripKind, out parsed))
                {
                    return parsed;
                }

                return null;
            }
        }

        public int DeleteInspectionsBefore(DateTime cutoffExclusive)
        {
            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM History_Inspections WHERE inspected_at < $cutoff;";
                SqliteDatabase.AddParameter(command, "$cutoff", cutoffExclusive.ToString("o", CultureInfo.InvariantCulture));
                return command.ExecuteNonQuery();
            }
        }

        private Inspection ReadInspection(SqliteDataReader reader)
        {
            Inspection inspection = new Inspection();
            inspection.Id = Convert.ToInt32(reader.GetInt64(0));
            inspection.PartNo = ReadString(reader, 1);
            inspection.PartName = ReadString(reader, 2);
            inspection.CategoryCode = ReadString(reader, 3);
            inspection.CategoryDescription = ReadString(reader, 4);
            inspection.Memo = ReadString(reader, 5);
            inspection.InputCode = ReadString(reader, 6);
            inspection.Result = (InspectionResult)Convert.ToInt32(reader.GetInt64(7));
            inspection.InspectedAt = ReadDateTime(reader, 8);
            inspection.ElapsedMilliseconds = ReadDecimal(reader, 9);
            inspection.ResultMessage = ReadString(reader, 10);
            inspection.AiScore = ReadDecimal(reader, 11);
            inspection.AiScoreThreshold = ReadDecimal(reader, 12);
            inspection.HasAiScore = Convert.ToInt32(reader.GetInt64(13)) != 0;
            return inspection;
        }

        private void LoadMeasurements(SqliteConnection connection, Dictionary<int, Inspection> inspectionMap)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT inspection_id, measurement_region_id, name, nominal_value, measured_value, tolerance_min, tolerance_max, unit, is_ok, deviation, message " +
                    "FROM History_Measurements ORDER BY inspection_id, id;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int inspectionId = Convert.ToInt32(reader.GetInt64(0));
                        if (!inspectionMap.ContainsKey(inspectionId))
                        {
                            continue;
                        }

                        MeasurementResult measurement = new MeasurementResult();
                        measurement.MeasurementRegionId = Convert.ToInt32(reader.GetInt64(1));
                        measurement.Name = ReadString(reader, 2);
                        measurement.NominalValue = ReadDecimal(reader, 3);
                        measurement.MeasuredValue = ReadDecimal(reader, 4);
                        measurement.ToleranceMin = ReadDecimal(reader, 5);
                        measurement.ToleranceMax = ReadDecimal(reader, 6);
                        measurement.Unit = ReadString(reader, 7);
                        measurement.IsPass = reader.GetInt64(8) == 1;
                        measurement.Deviation = ReadDecimal(reader, 9);
                        measurement.Message = ReadString(reader, 10);
                        // 판정 없는 참고값은 문구 표식으로 되살립니다.
                        // 이력 표에 열을 더하지 않아 옛 행과 그대로 호환됩니다.
                        measurement.IsJudged = measurement.Message == null || !measurement.Message.Contains("판정 없음");
                        inspectionMap[inspectionId].Measurements.Add(measurement);
                    }
                }
            }
        }

        private void LoadCapturedImages(SqliteConnection connection, Dictionary<int, Inspection> inspectionMap)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT inspection_id, view_type, display_name, file_path, captured_at " +
                    "FROM History_CapturedImages ORDER BY inspection_id, view_type;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int inspectionId = Convert.ToInt32(reader.GetInt64(0));
                        if (!inspectionMap.ContainsKey(inspectionId))
                        {
                            continue;
                        }

                        CapturedImage image = new CapturedImage();
                        image.ViewType = (ImageViewType)Convert.ToInt32(reader.GetInt64(1));
                        image.DisplayName = ReadString(reader, 2);
                        image.FilePath = ReadString(reader, 3);
                        image.CapturedAt = ReadDateTime(reader, 4);
                        inspectionMap[inspectionId].Images.Add(image);
                    }
                }
            }
        }

        private void LoadEvents(SqliteConnection connection, Dictionary<int, Inspection> inspectionMap)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT inspection_id, severity, source, message, created_at FROM History_Events ORDER BY inspection_id, id;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int inspectionId = Convert.ToInt32(reader.GetInt64(0));
                        if (!inspectionMap.ContainsKey(inspectionId))
                        {
                            continue;
                        }

                        EventLogEntry entry = new EventLogEntry();
                        entry.Severity = (EventSeverity)Convert.ToInt32(reader.GetInt64(1));
                        entry.Source = ReadString(reader, 2);
                        entry.Message = ReadString(reader, 3);
                        entry.CreatedAt = ReadDateTime(reader, 4);
                        inspectionMap[inspectionId].Events.Add(entry);
                    }
                }
            }
        }

        private void SaveInspectionHeader(SqliteConnection connection, SqliteTransaction transaction, Inspection inspection)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO History_Inspections (id, part_no, part_name, category_code, category_description, memo, input_code, result, inspected_at, elapsed_ms, result_message, ai_score, ai_score_threshold, has_ai_score) " +
                    "VALUES ($id, $part_no, $part_name, $category_code, $category_description, $memo, $input_code, $result, $inspected_at, $elapsed_ms, $result_message, $ai_score, $ai_score_threshold, $has_ai_score) " +
                    "ON CONFLICT(id) DO UPDATE SET part_no = excluded.part_no, part_name = excluded.part_name, category_code = excluded.category_code, " +
                    "category_description = excluded.category_description, memo = excluded.memo, input_code = excluded.input_code, result = excluded.result, " +
                    "inspected_at = excluded.inspected_at, elapsed_ms = excluded.elapsed_ms, result_message = excluded.result_message, " +
                    "ai_score = excluded.ai_score, ai_score_threshold = excluded.ai_score_threshold, has_ai_score = excluded.has_ai_score;";
                SqliteDatabase.AddParameter(command, "$id", inspection.Id);
                SqliteDatabase.AddParameter(command, "$part_no", inspection.PartNo);
                SqliteDatabase.AddParameter(command, "$part_name", inspection.PartName);
                SqliteDatabase.AddParameter(command, "$category_code", inspection.CategoryCode);
                SqliteDatabase.AddParameter(command, "$category_description", inspection.CategoryDescription);
                SqliteDatabase.AddParameter(command, "$memo", inspection.Memo);
                SqliteDatabase.AddParameter(command, "$input_code", inspection.InputCode);
                SqliteDatabase.AddParameter(command, "$result", (int)inspection.Result);
                SqliteDatabase.AddParameter(command, "$inspected_at", inspection.InspectedAt.ToString("o", CultureInfo.InvariantCulture));
                SqliteDatabase.AddParameter(command, "$elapsed_ms", inspection.ElapsedMilliseconds);
                SqliteDatabase.AddParameter(command, "$result_message", inspection.ResultMessage);
                SqliteDatabase.AddParameter(command, "$ai_score", inspection.AiScore);
                SqliteDatabase.AddParameter(command, "$ai_score_threshold", inspection.AiScoreThreshold);
                SqliteDatabase.AddParameter(command, "$has_ai_score", inspection.HasAiScore ? 1 : 0);
                command.ExecuteNonQuery();
            }
        }

        private void DeleteInspectionChildren(SqliteConnection connection, SqliteTransaction transaction, int inspectionId)
        {
            ExecuteDelete(connection, transaction, "DELETE FROM History_Measurements WHERE inspection_id = $inspection_id;", inspectionId);
            ExecuteDelete(connection, transaction, "DELETE FROM History_CapturedImages WHERE inspection_id = $inspection_id;", inspectionId);
            ExecuteDelete(connection, transaction, "DELETE FROM History_Events WHERE inspection_id = $inspection_id;", inspectionId);
        }

        private void SaveMeasurements(SqliteConnection connection, SqliteTransaction transaction, Inspection inspection)
        {
            foreach (MeasurementResult measurement in inspection.Measurements)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO History_Measurements (inspection_id, measurement_region_id, name, nominal_value, measured_value, tolerance_min, tolerance_max, unit, is_ok, deviation, message) " +
                        "VALUES ($inspection_id, $measurement_region_id, $name, $nominal_value, $measured_value, $tolerance_min, $tolerance_max, $unit, $is_ok, $deviation, $message);";
                    SqliteDatabase.AddParameter(command, "$inspection_id", inspection.Id);
                    SqliteDatabase.AddParameter(command, "$measurement_region_id", measurement.MeasurementRegionId);
                    // name/unit은 NOT NULL 열입니다. 다른 경로로 만들어진 결과가 들어와도
                    // 검사 이력 전체가 롤백되지 않도록 저장 직전에 한 번 더 막습니다.
                    SqliteDatabase.AddParameter(command, "$name", measurement.Name == null ? string.Empty : measurement.Name);
                    SqliteDatabase.AddParameter(command, "$nominal_value", measurement.NominalValue);
                    SqliteDatabase.AddParameter(command, "$measured_value", measurement.MeasuredValue);
                    SqliteDatabase.AddParameter(command, "$tolerance_min", measurement.ToleranceMin);
                    SqliteDatabase.AddParameter(command, "$tolerance_max", measurement.ToleranceMax);
                    SqliteDatabase.AddParameter(command, "$unit", measurement.Unit == null ? string.Empty : measurement.Unit);
                    SqliteDatabase.AddParameter(command, "$is_ok", measurement.IsPass ? 1 : 0);
                    SqliteDatabase.AddParameter(command, "$deviation", measurement.Deviation);
                    SqliteDatabase.AddParameter(command, "$message", measurement.Message);
                    command.ExecuteNonQuery();
                }
            }
        }

        private void SaveCapturedImages(SqliteConnection connection, SqliteTransaction transaction, Inspection inspection)
        {
            foreach (CapturedImage image in inspection.Images)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    // 방향별 판정·점수·치수를 그 방향 행에 함께 담습니다.
                    //
                    // 예전에는 이 값들이 result_message 문자열 안에만 있어 조회도 통계도 낼 수
                    // 없었고, 화면에서는 목록의 메시지 칸에 통째로 나와 다른 칸과 겹쳤습니다.
                    // 값이 없는 방향은 NULL 로 둡니다. 0 으로 채우면 실제 0 과 구분되지 않습니다.
                    AiViewInferenceResult viewResult = FindViewResult(inspection, image.ViewType);

                    command.CommandText =
                        "INSERT INTO History_CapturedImages (inspection_id, view_type, display_name, file_path, captured_at, " +
                        "view_judge, score, dimension_width, dimension_height, dimension_depth) " +
                        "VALUES ($inspection_id, $view_type, $display_name, $file_path, $captured_at, " +
                        "$view_judge, $score, $dimension_width, $dimension_height, $dimension_depth);";
                    SqliteDatabase.AddParameter(command, "$inspection_id", inspection.Id);
                    SqliteDatabase.AddParameter(command, "$view_type", (int)image.ViewType);
                    SqliteDatabase.AddParameter(command, "$display_name", image.DisplayName);
                    SqliteDatabase.AddParameter(command, "$file_path", image.FilePath);
                    SqliteDatabase.AddParameter(command, "$captured_at", image.CapturedAt == DateTime.MinValue ? DateTime.Now.ToString("o", CultureInfo.InvariantCulture) : image.CapturedAt.ToString("o", CultureInfo.InvariantCulture));
                    SqliteDatabase.AddParameter(command, "$view_judge",
                        viewResult == null ? (object)DBNull.Value : (viewResult.IsPass ? "PASS" : "FAIL"));
                    SqliteDatabase.AddParameter(command, "$score",
                        viewResult == null || !viewResult.HasScore ? (object)DBNull.Value : viewResult.Score);
                    SqliteDatabase.AddParameter(command, "$dimension_width",
                        viewResult == null || !viewResult.DimensionWidth.HasValue ? (object)DBNull.Value : viewResult.DimensionWidth.Value);
                    SqliteDatabase.AddParameter(command, "$dimension_height",
                        viewResult == null || !viewResult.DimensionHeight.HasValue ? (object)DBNull.Value : viewResult.DimensionHeight.Value);
                    SqliteDatabase.AddParameter(command, "$dimension_depth",
                        viewResult == null || !viewResult.DimensionDepth.HasValue ? (object)DBNull.Value : viewResult.DimensionDepth.Value);
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 그 방향의 AI 결과를 찾습니다. 없으면 null 입니다.
        /// 카메라 프레임을 못 받은 방향은 결과가 없을 수 있습니다.
        /// </summary>
        private AiViewInferenceResult FindViewResult(Inspection inspection, ImageViewType viewType)
        {
            if (inspection == null || inspection.ViewResults == null)
            {
                return null;
            }

            AiViewInferenceResult found;
            if (inspection.ViewResults.TryGetValue(viewType, out found))
            {
                return found;
            }

            return null;
        }

        private void SaveEvents(SqliteConnection connection, SqliteTransaction transaction, Inspection inspection)
        {
            foreach (EventLogEntry entry in inspection.Events)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO History_Events (inspection_id, severity, source, message, created_at) " +
                        "VALUES ($inspection_id, $severity, $source, $message, $created_at);";
                    SqliteDatabase.AddParameter(command, "$inspection_id", inspection.Id);
                    SqliteDatabase.AddParameter(command, "$severity", (int)entry.Severity);
                    SqliteDatabase.AddParameter(command, "$source", entry.Source);
                    SqliteDatabase.AddParameter(command, "$message", entry.Message);
                    SqliteDatabase.AddParameter(command, "$created_at", entry.CreatedAt == DateTime.MinValue ? DateTime.Now.ToString("o", CultureInfo.InvariantCulture) : entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
                    command.ExecuteNonQuery();
                }
            }
        }

        private void ExecuteDelete(SqliteConnection connection, SqliteTransaction transaction, string sql, int inspectionId)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                SqliteDatabase.AddParameter(command, "$inspection_id", inspectionId);
                command.ExecuteNonQuery();
            }
        }

        private void ApplyRetentionPolicy()
        {
            DeleteExpiredInspections();
            DeleteOldestInspectionsUntilFreeSpaceIsEnough();
        }

        private void DeleteExpiredInspections()
        {
            if (_retentionOptions.RetentionDays <= 0)
            {
                return;
            }

            string cutoff = DateTime.Now.Date.AddDays(-_retentionOptions.RetentionDays).ToString("o", CultureInfo.InvariantCulture);
            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM History_Inspections WHERE inspected_at < $cutoff;";
                SqliteDatabase.AddParameter(command, "$cutoff", cutoff);
                command.ExecuteNonQuery();
            }
        }

        private void DeleteOldestInspectionsUntilFreeSpaceIsEnough()
        {
            if (_retentionOptions.MinimumFreeSpaceBytes <= 0)
            {
                return;
            }

            DriveInfo drive = new DriveInfo(Path.GetPathRoot(_database.DatabasePath));
            while (drive.AvailableFreeSpace < _retentionOptions.MinimumFreeSpaceBytes)
            {
                int oldestInspectionId = GetOldestInspectionId();
                if (oldestInspectionId <= 0)
                {
                    return;
                }

                DeleteInspection(oldestInspectionId);
                drive = new DriveInfo(Path.GetPathRoot(_database.DatabasePath));
            }
        }

        private int GetOldestInspectionId()
        {
            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id FROM History_Inspections ORDER BY inspected_at ASC, id ASC LIMIT 1;";
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }

        private void DeleteInspection(int inspectionId)
        {
            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM History_Inspections WHERE id = $inspection_id;";
                SqliteDatabase.AddParameter(command, "$inspection_id", inspectionId);
                command.ExecuteNonQuery();
            }
        }

        private string ReadString(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return string.Empty;
            }

            return reader.GetString(ordinal);
        }

        private decimal ReadDecimal(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return 0m;
            }

            return Convert.ToDecimal(reader.GetDouble(ordinal), CultureInfo.InvariantCulture);
        }

        private DateTime ReadDateTime(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return DateTime.Now;
            }

            DateTime parsed;
            if (DateTime.TryParse(reader.GetString(ordinal), null, DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed;
            }

            return DateTime.Now;
        }
    }
}
