using System;
using System.Collections.Generic;
using System.Globalization;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using Microsoft.Data.Sqlite;

namespace AI.Vision.IOInspector.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite DataBase.db의 PartList_* 테이블을 사용해 부품 기준정보를 관리합니다.
    /// </summary>
    public class SqlitePartRepository : IPartRepository
    {
        private readonly SqliteDatabase _database;

        public SqlitePartRepository(SqliteDatabase database)
        {
            _database = database;
        }

        public IList<Part> GetAll()
        {
            Dictionary<string, Part> partMap = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);
            IList<Part> parts = new List<Part>();
            using (SqliteConnection connection = _database.OpenConnection())
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT part_no, part_name, category_code, category_description, part_type, created_at, updated_at " +
                        "FROM PartList_Parts ORDER BY part_no;";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Part part = ReadPart(reader);
                            parts.Add(part);
                            partMap[part.PartNo] = part;
                        }
                    }
                }

                LoadMeasurements(connection, partMap, null);
                LoadImages(connection, partMap, null);
            }

            return parts;
        }

        public Part GetByPartNo(string partNo)
        {
            if (string.IsNullOrWhiteSpace(partNo))
            {
                return null;
            }

            Dictionary<string, Part> partMap = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);
            Part part = null;
            using (SqliteConnection connection = _database.OpenConnection())
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT part_no, part_name, category_code, category_description, part_type, created_at, updated_at " +
                        "FROM PartList_Parts WHERE part_no = $part_no;";
                    SqliteDatabase.AddParameter(command, "$part_no", partNo.Trim());
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            part = ReadPart(reader);
                            partMap[part.PartNo] = part;
                        }
                    }
                }

                if (part != null)
                {
                    LoadMeasurements(connection, partMap, part.PartNo);
                    LoadImages(connection, partMap, part.PartNo);
                }
            }

            return part;
        }

        public void Save(Part part)
        {
            if (part == null)
            {
                return;
            }

            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                UpsertCategory(connection, transaction, part);
                UpsertPart(connection, transaction, part);
                DeletePartChildren(connection, transaction, part.PartNo);
                SaveMeasurementRegions(connection, transaction, part);
                SaveReferenceImages(connection, transaction, part);
                transaction.Commit();
            }
        }

        public void Delete(string partNo)
        {
            if (string.IsNullOrWhiteSpace(partNo))
            {
                return;
            }

            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM PartList_Parts WHERE part_no = $part_no;";
                SqliteDatabase.AddParameter(command, "$part_no", partNo.Trim());
                command.ExecuteNonQuery();
            }
        }

        private Part ReadPart(SqliteDataReader reader)
        {
            Part part = new Part();
            part.PartNo = ReadString(reader, 0);
            part.PartName = ReadString(reader, 1);
            part.CategoryCode = ReadString(reader, 2);
            part.CategoryDescription = ReadString(reader, 3);
            part.PartType = ReadString(reader, 4);
            part.CreatedAt = ReadDateTime(reader, 5);
            part.UpdatedAt = ReadDateTime(reader, 6);
            return part;
        }

        private void LoadMeasurements(SqliteConnection connection, Dictionary<string, Part> partMap, string partNo)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT s.part_no, i.id, s.set_name, i.item_name, i.view_type, i.nominal_value, i.tolerance_min, i.tolerance_max, i.unit, i.coordinates " +
                    "FROM PartList_MeasurementSets s " +
                    "INNER JOIN PartList_MeasurementItems i ON i.set_id = s.id " +
                    "WHERE ($part_no IS NULL OR s.part_no = $part_no) AND i.is_used = 1 " +
                    "ORDER BY s.part_no, s.set_index, i.item_order;";
                SqliteDatabase.AddParameter(command, "$part_no", partNo);
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string currentPartNo = ReadString(reader, 0);
                        if (!partMap.ContainsKey(currentPartNo))
                        {
                            continue;
                        }

                        MeasurementRegion region = new MeasurementRegion();
                        region.PartNo = currentPartNo;
                        region.Id = Convert.ToInt32(reader.GetInt64(1));
                        region.Name = ReadString(reader, 2) + " - " + ReadString(reader, 3);
                        region.ViewType = (ImageViewType)Convert.ToInt32(reader.GetInt64(4));
                        region.NominalValue = ReadDecimal(reader, 5);
                        region.ToleranceMin = ReadDecimal(reader, 6);
                        region.ToleranceMax = ReadDecimal(reader, 7);
                        region.Unit = ReadString(reader, 8);
                        region.Coordinates = ReadString(reader, 9);
                        partMap[currentPartNo].MeasurementRegions.Add(region);
                    }
                }
            }
        }

        private void LoadImages(SqliteConnection connection, Dictionary<string, Part> partMap, string partNo)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT id, part_no, view_type, file_path, captured_at " +
                    "FROM PartList_ReferenceImages " +
                    "WHERE ($part_no IS NULL OR part_no = $part_no) ORDER BY part_no, view_type;";
                SqliteDatabase.AddParameter(command, "$part_no", partNo);
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string currentPartNo = ReadString(reader, 1);
                        if (!partMap.ContainsKey(currentPartNo))
                        {
                            continue;
                        }

                        PartImage image = new PartImage();
                        image.Id = Convert.ToInt32(reader.GetInt64(0));
                        image.PartNo = currentPartNo;
                        image.ViewType = (ImageViewType)Convert.ToInt32(reader.GetInt64(2));
                        image.FilePath = ReadString(reader, 3);
                        image.CapturedAt = ReadDateTime(reader, 4);
                        partMap[currentPartNo].Images.Add(image);
                    }
                }
            }
        }

        private void UpsertCategory(SqliteConnection connection, SqliteTransaction transaction, Part part)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO PartList_Categories (category_code, category_description) VALUES ($category_code, $category_description) " +
                    "ON CONFLICT(category_code) DO UPDATE SET category_description = excluded.category_description;";
                SqliteDatabase.AddParameter(command, "$category_code", NormalizeRequired(part.CategoryCode, "EMPTY"));
                SqliteDatabase.AddParameter(command, "$category_description", NormalizeRequired(part.CategoryDescription, "-"));
                command.ExecuteNonQuery();
            }
        }

        private void UpsertPart(SqliteConnection connection, SqliteTransaction transaction, Part part)
        {
            string now = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO PartList_Parts (part_no, part_name, category_code, category_description, part_type, created_at, updated_at) " +
                    "VALUES ($part_no, $part_name, $category_code, $category_description, $part_type, $created_at, $updated_at) " +
                    "ON CONFLICT(part_no) DO UPDATE SET part_name = excluded.part_name, category_code = excluded.category_code, " +
                    "category_description = excluded.category_description, part_type = excluded.part_type, updated_at = excluded.updated_at;";
                SqliteDatabase.AddParameter(command, "$part_no", part.PartNo.Trim());
                SqliteDatabase.AddParameter(command, "$part_name", NormalizeRequired(part.PartName, "-"));
                SqliteDatabase.AddParameter(command, "$category_code", NormalizeRequired(part.CategoryCode, "EMPTY"));
                SqliteDatabase.AddParameter(command, "$category_description", NormalizeRequired(part.CategoryDescription, "-"));
                SqliteDatabase.AddParameter(command, "$part_type", NormalizeRequired(part.PartType, "-"));
                SqliteDatabase.AddParameter(command, "$created_at", part.CreatedAt == DateTime.MinValue ? now : part.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
                SqliteDatabase.AddParameter(command, "$updated_at", now);
                command.ExecuteNonQuery();
            }
        }

        private void DeletePartChildren(SqliteConnection connection, SqliteTransaction transaction, string partNo)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM PartList_MeasurementSets WHERE part_no = $part_no;";
                SqliteDatabase.AddParameter(command, "$part_no", partNo);
                command.ExecuteNonQuery();
            }

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM PartList_ReferenceImages WHERE part_no = $part_no;";
                SqliteDatabase.AddParameter(command, "$part_no", partNo);
                command.ExecuteNonQuery();
            }
        }

        private void SaveMeasurementRegions(SqliteConnection connection, SqliteTransaction transaction, Part part)
        {
            Dictionary<int, long> setIdByIndex = new Dictionary<int, long>();
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                int setIndex = ResolveMeasurementSetIndex(region.Name);
                string setName = BuildMeasurementSetName(setIndex);
                if (!setIdByIndex.ContainsKey(setIndex))
                {
                    setIdByIndex[setIndex] = InsertMeasurementSet(connection, transaction, part.PartNo, setIndex, setName);
                }

                InsertMeasurementItem(connection, transaction, setIdByIndex[setIndex], region);
            }
        }

        private long InsertMeasurementSet(SqliteConnection connection, SqliteTransaction transaction, string partNo, int setIndex, string setName)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO PartList_MeasurementSets (part_no, set_index, set_name) VALUES ($part_no, $set_index, $set_name);";
                SqliteDatabase.AddParameter(command, "$part_no", partNo);
                SqliteDatabase.AddParameter(command, "$set_index", setIndex);
                SqliteDatabase.AddParameter(command, "$set_name", setName);
                command.ExecuteNonQuery();
            }

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT last_insert_rowid();";
                object value = command.ExecuteScalar();
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
        }

        private void InsertMeasurementItem(SqliteConnection connection, SqliteTransaction transaction, long setId, MeasurementRegion region)
        {
            string itemName = ResolveMeasurementItemName(region.Name);
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO PartList_MeasurementItems (set_id, item_name, item_order, view_type, nominal_value, tolerance_min, tolerance_max, unit, is_used, coordinates) " +
                    "VALUES ($set_id, $item_name, $item_order, $view_type, $nominal_value, $tolerance_min, $tolerance_max, $unit, 1, $coordinates);";
                SqliteDatabase.AddParameter(command, "$set_id", setId);
                SqliteDatabase.AddParameter(command, "$item_name", itemName);
                SqliteDatabase.AddParameter(command, "$item_order", ResolveMeasurementItemOrder(itemName));
                SqliteDatabase.AddParameter(command, "$view_type", (int)region.ViewType);
                SqliteDatabase.AddParameter(command, "$nominal_value", region.NominalValue);
                SqliteDatabase.AddParameter(command, "$tolerance_min", region.ToleranceMin);
                SqliteDatabase.AddParameter(command, "$tolerance_max", region.ToleranceMax);
                SqliteDatabase.AddParameter(command, "$unit", NormalizeRequired(region.Unit, "mm"));
                SqliteDatabase.AddParameter(command, "$coordinates", NormalizeRequired(region.Coordinates, "미정"));
                command.ExecuteNonQuery();
            }
        }

        private void SaveReferenceImages(SqliteConnection connection, SqliteTransaction transaction, Part part)
        {
            foreach (PartImage image in part.Images)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "INSERT INTO PartList_ReferenceImages (part_no, view_type, file_path, display_path, captured_at) " +
                        "VALUES ($part_no, $view_type, $file_path, $display_path, $captured_at);";
                    SqliteDatabase.AddParameter(command, "$part_no", part.PartNo);
                    SqliteDatabase.AddParameter(command, "$view_type", (int)image.ViewType);
                    SqliteDatabase.AddParameter(command, "$file_path", NormalizeRequired(image.FilePath, "-"));
                    SqliteDatabase.AddParameter(command, "$display_path", BuildReferenceDisplayPath(part));
                    SqliteDatabase.AddParameter(command, "$captured_at", image.CapturedAt == DateTime.MinValue ? DateTime.Now.ToString("o", CultureInfo.InvariantCulture) : image.CapturedAt.ToString("o", CultureInfo.InvariantCulture));
                    command.ExecuteNonQuery();
                }
            }
        }

        private int ResolveMeasurementSetIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 1;
            }

            string prefix = name.Split('-')[0].Trim();
            string numberText = prefix.Replace("측정부", string.Empty).Trim();
            int setIndex;
            if (int.TryParse(numberText, out setIndex) && setIndex > 0)
            {
                return setIndex;
            }

            return 1;
        }

        private string BuildMeasurementSetName(int setIndex)
        {
            if (setIndex <= 1)
            {
                return "측정부";
            }

            return "측정부" + setIndex.ToString(CultureInfo.InvariantCulture);
        }

        private string ResolveMeasurementItemName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "길이";
            }

            int separatorIndex = name.IndexOf('-');
            if (separatorIndex >= 0 && separatorIndex < name.Length - 1)
            {
                return name.Substring(separatorIndex + 1).Trim();
            }

            if (name.Contains("너비"))
            {
                return "너비";
            }

            if (name.Contains("높이"))
            {
                return "높이";
            }

            if (name.Contains("두께"))
            {
                return "두께";
            }

            return "길이";
        }

        private int ResolveMeasurementItemOrder(string itemName)
        {
            if (itemName == "길이")
            {
                return 1;
            }

            if (itemName == "너비")
            {
                return 2;
            }

            if (itemName == "높이")
            {
                return 3;
            }

            if (itemName == "두께")
            {
                return 4;
            }

            return 99;
        }

        private string BuildReferenceDisplayPath(Part part)
        {
            return "REFERENCE:\\\\" + NormalizeRequired(part.CategoryCode, "EMPTY") + "\\" + part.PartNo;
        }

        private string NormalizeRequired(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return value.Trim();
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
