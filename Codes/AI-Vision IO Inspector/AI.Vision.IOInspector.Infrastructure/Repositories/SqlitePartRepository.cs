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

        /// <summary>
        /// 경로 변환에 쓰는 설정입니다. IMAGE_PATH 루트는 실행 중 바뀌지 않으므로 한 번만 읽습니다.
        /// </summary>
        private AI.Vision.IOInspector.Infrastructure.Services.RuntimeImagePathSettings _imagePathSettings;

        private AI.Vision.IOInspector.Infrastructure.Services.RuntimeImagePathSettings ImagePathSettings
        {
            get
            {
                if (_imagePathSettings == null)
                {
                    _imagePathSettings = AI.Vision.IOInspector.Infrastructure.Services.RuntimeImagePathSettings
                        .Load(AppContext.BaseDirectory);
                }

                return _imagePathSettings;
            }
        }

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
                        "SELECT part_no, part_name, category_code, category_description, memo, created_at, updated_at " +
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
                        "SELECT part_no, part_name, category_code, category_description, memo, created_at, updated_at " +
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

        public string GetCategoryDescription(string categoryCode)
        {
            if (string.IsNullOrWhiteSpace(categoryCode))
            {
                return string.Empty;
            }

            using (SqliteConnection connection = _database.OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT category_description FROM PartList_Categories WHERE category_code = $category_code;";
                SqliteDatabase.AddParameter(command, "$category_code", categoryCode.Trim());
                object value = command.ExecuteScalar();
                if (value != null && value != DBNull.Value)
                {
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                }

                command.CommandText =
                    "SELECT category_description FROM PartList_Parts " +
                    "WHERE category_code = $category_code ORDER BY updated_at DESC LIMIT 1;";
                value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return string.Empty;
                }

                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
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

        public void ReplaceAll(IList<Part> parts)
        {
            if (parts == null)
            {
                return;
            }

            using (SqliteConnection connection = _database.OpenConnection())
            {
                Dictionary<string, IList<PartImage>> preservedImages = LoadAllReferenceImagesForReplacement(connection);
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    DeleteAllPartListRows(connection, transaction);

                    foreach (Part part in parts)
                    {
                        AttachPreservedImages(part, preservedImages);
                        UpsertCategory(connection, transaction, part);
                        UpsertPart(connection, transaction, part);
                        SaveMeasurementRegions(connection, transaction, part);
                        SaveReferenceImages(connection, transaction, part);
                    }

                    transaction.Commit();
                }
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

        private Dictionary<string, IList<PartImage>> LoadAllReferenceImagesForReplacement(SqliteConnection connection)
        {
            Dictionary<string, IList<PartImage>> imageMap = new Dictionary<string, IList<PartImage>>(StringComparer.OrdinalIgnoreCase);
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, part_no, view_type, file_path, captured_at, set_no FROM PartList_ReferenceImages ORDER BY part_no, set_no, view_type;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        PartImage image = new PartImage();
                        image.Id = Convert.ToInt32(reader.GetInt64(0));
                        image.PartNo = ReadString(reader, 1);
                        image.ViewType = (ImageViewType)Convert.ToInt32(reader.GetInt64(2));
                        // 상대 형태로 담긴 경로를 이 컴퓨터의 IMAGE_PATH 루트로 되살립니다.
                        // 예전 절대 경로 행은 그대로 통과합니다.
                        image.FilePath = ImagePathSettings.FromStorableImagePath(ReadString(reader, 3));
                        image.CapturedAt = ReadDateTime(reader, 4);
                        image.SetNo = Convert.ToInt32(reader.GetInt64(5));

                        if (!imageMap.ContainsKey(image.PartNo))
                        {
                            imageMap[image.PartNo] = new List<PartImage>();
                        }

                        imageMap[image.PartNo].Add(image);
                    }
                }
            }

            return imageMap;
        }

        private void DeleteAllPartListRows(SqliteConnection connection, SqliteTransaction transaction)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM PartList_Parts;";
                command.ExecuteNonQuery();
            }
        }

        private void AttachPreservedImages(Part part, Dictionary<string, IList<PartImage>> preservedImages)
        {
            if (part == null || part.Images.Count > 0 || string.IsNullOrWhiteSpace(part.PartNo))
            {
                return;
            }

            string partNo = part.PartNo.Trim();
            if (!preservedImages.ContainsKey(partNo))
            {
                return;
            }

            foreach (PartImage preservedImage in preservedImages[partNo])
            {
                PartImage image = new PartImage();
                image.PartNo = partNo;
                image.ViewType = preservedImage.ViewType;
                image.FilePath = preservedImage.FilePath;
                image.CapturedAt = preservedImage.CapturedAt;
                image.SetNo = preservedImage.SetNo;
                part.Images.Add(image);
            }
        }

        private Part ReadPart(SqliteDataReader reader)
        {
            Part part = new Part();
            part.PartNo = ReadString(reader, 0);
            part.PartName = ReadString(reader, 1);
            part.CategoryCode = ReadString(reader, 2);
            part.CategoryDescription = ReadString(reader, 3);
            part.Memo = ReadString(reader, 4);
            part.CreatedAt = ReadDateTime(reader, 5);
            part.UpdatedAt = ReadDateTime(reader, 6);
            return part;
        }

        private void LoadMeasurements(SqliteConnection connection, Dictionary<string, Part> partMap, string partNo)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT part_no, id, index_no, item_type, view_type, nominal_value, tolerance, unit, x1, y1, x2, y2, line_color, tolerance_min, tolerance_max " +
                    "FROM PartList_MeasurementPoints " +
                    "WHERE ($part_no IS NULL OR part_no = $part_no) " +
                    "ORDER BY part_no, index_no;";
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
                        region.IndexNo = Convert.ToInt32(reader.GetInt64(2));
                        region.ItemType = ReadString(reader, 3);

                        // 이름에 카메라가 들어가므로 ViewType을 먼저 채운 뒤에 만듭니다.
                        // 순서가 바뀌면 아직 비어 있는 기본값(Top)으로 이름이 만들어집니다.
                        region.ViewType = (ImageViewType)Convert.ToInt32(reader.GetInt64(4));
                        region.Name = BuildMeasurementPointName(region.IndexNo, region.ItemType, region.ViewType);
                        region.NominalValue = ReadDecimal(reader, 5);
                        decimal tolerance = Math.Abs(ReadDecimal(reader, 6));
                        decimal toleranceMin = ReadDecimal(reader, 13);
                        decimal toleranceMax = ReadDecimal(reader, 14);
                        if (toleranceMin == 0m && toleranceMax == 0m && tolerance != 0m)
                        {
                            toleranceMin = -tolerance;
                            toleranceMax = tolerance;
                        }

                        // 설정자가 절대값으로 정규화하므로 저장된 부호와 무관하게 크기만 들어옵니다.
                        region.ToleranceMin = toleranceMin;
                        region.ToleranceMax = toleranceMax;
                        region.Unit = ReadString(reader, 7);
                        region.X1 = ReadNullableDouble(reader, 8);
                        region.Y1 = ReadNullableDouble(reader, 9);
                        region.X2 = ReadNullableDouble(reader, 10);
                        region.Y2 = ReadNullableDouble(reader, 11);
                        region.LineColor = ReadString(reader, 12);
                        region.Coordinates = BuildCoordinatesText(region);
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
                    "SELECT id, part_no, view_type, file_path, captured_at, set_no " +
                    "FROM PartList_ReferenceImages " +
                    "WHERE ($part_no IS NULL OR part_no = $part_no) ORDER BY part_no, set_no, view_type;";
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
                        // 상대 형태로 담긴 경로를 이 컴퓨터의 IMAGE_PATH 루트로 되살립니다.
                        // 예전 절대 경로 행은 그대로 통과합니다.
                        image.FilePath = ImagePathSettings.FromStorableImagePath(ReadString(reader, 3));
                        image.CapturedAt = ReadDateTime(reader, 4);
                        image.SetNo = Convert.ToInt32(reader.GetInt64(5));
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
                    "ON CONFLICT(category_code) DO NOTHING;";
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
                    "INSERT INTO PartList_Parts (part_no, part_name, category_code, category_description, memo, created_at, updated_at) " +
                    "VALUES ($part_no, $part_name, $category_code, $category_description, $memo, $created_at, $updated_at) " +
                    "ON CONFLICT(part_no) DO UPDATE SET part_name = excluded.part_name, category_code = excluded.category_code, " +
                    "category_description = excluded.category_description, memo = excluded.memo, updated_at = excluded.updated_at;";
                SqliteDatabase.AddParameter(command, "$part_no", part.PartNo.Trim());
                SqliteDatabase.AddParameter(command, "$part_name", NormalizeRequired(part.PartName, "-"));
                SqliteDatabase.AddParameter(command, "$category_code", NormalizeRequired(part.CategoryCode, "EMPTY"));
                SqliteDatabase.AddParameter(command, "$category_description", NormalizeRequired(part.CategoryDescription, "-"));
                SqliteDatabase.AddParameter(command, "$memo", NormalizeRequired(part.Memo, "-"));
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
                command.CommandText = "DELETE FROM PartList_MeasurementPoints WHERE part_no = $part_no;";
                SqliteDatabase.AddParameter(command, "$part_no", partNo);
                command.ExecuteNonQuery();
            }

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
            int fallbackIndex = 1;
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                int indexNo = region.IndexNo > 0 ? region.IndexNo : fallbackIndex;
                // 번호가 크다고 나머지를 버리지 않습니다.
                // 번호는 카메라마다 1부터 세므로 Top 5번 다음에 Thickness 1번이 옵니다.
                // 예전에는 여기서 break 로 빠져나가 Thickness 측정부가 통째로 저장되지 않았습니다.
                if (indexNo > MeasurementPointPolicy.MaxCount)
                {
                    continue;
                }

                InsertMeasurementPoint(connection, transaction, part.PartNo, indexNo, region);
                fallbackIndex++;
            }
        }

        private void InsertMeasurementPoint(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string partNo,
            int indexNo,
            MeasurementRegion region)
        {
            string itemType = ResolveMeasurementItemName(region.Name);
            if (!string.IsNullOrWhiteSpace(region.ItemType))
            {
                itemType = region.ItemType.Trim();
            }

            decimal tolerance = Math.Max(region.ToleranceMin, region.ToleranceMax);
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO PartList_MeasurementPoints " +
                    "(part_no, index_no, item_type, view_type, nominal_value, tolerance, tolerance_min, tolerance_max, unit, x1, y1, x2, y2, line_color) " +
                    "VALUES ($part_no, $index_no, $item_type, $view_type, $nominal_value, $tolerance, $tolerance_min, $tolerance_max, $unit, $x1, $y1, $x2, $y2, $line_color);";
                SqliteDatabase.AddParameter(command, "$part_no", partNo);
                SqliteDatabase.AddParameter(command, "$index_no", indexNo);
                SqliteDatabase.AddParameter(command, "$item_type", NormalizeRequired(itemType, "미설정"));
                // 측정부가 속한 카메라를 그대로 저장합니다.
                // 예전에는 Thickness 하나뿐이라 값을 박아 두었는데, 카메라별로 관리하면서
                // 그대로 두면 Top 측정부까지 Thickness로 저장되어 번호가 충돌합니다.
                // 카메라는 손대지 않고 있는 그대로 담습니다.
                // 나중에 Front/Back에도 측정부가 생기면 그 값과 허용오차, 좌표가
                // 각자의 카메라에 그대로 남아 있어야 합니다.
                SqliteDatabase.AddParameter(command, "$view_type", (int)region.ViewType);
                SqliteDatabase.AddParameter(command, "$nominal_value", region.NominalValue);
                SqliteDatabase.AddParameter(command, "$tolerance", tolerance);
                SqliteDatabase.AddParameter(command, "$tolerance_min", region.SignedToleranceMin);
                SqliteDatabase.AddParameter(command, "$tolerance_max", region.SignedToleranceMax);
                SqliteDatabase.AddParameter(command, "$unit", "mm");
                SqliteDatabase.AddParameter(command, "$x1", region.X1);
                SqliteDatabase.AddParameter(command, "$y1", region.Y1);
                SqliteDatabase.AddParameter(command, "$x2", region.X2);
                SqliteDatabase.AddParameter(command, "$y2", region.Y2);
                SqliteDatabase.AddParameter(command, "$line_color", NormalizeRequired(region.LineColor, MeasurementPointPolicy.GetDefaultColor(indexNo)));
                command.ExecuteNonQuery();
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
                // 저장 관례는 Min 음수 / Max 양수입니다. 사용자가 보는 -/+ 표기와 맞추기 위한 것이며,
                // 판정은 부호가 아니라 크기로 하지만 DB 값도 관례를 지키도록 여기서 정규화합니다.
                SqliteDatabase.AddParameter(command, "$tolerance_min", region.SignedToleranceMin);
                SqliteDatabase.AddParameter(command, "$tolerance_max", region.SignedToleranceMax);
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
                        "INSERT INTO PartList_ReferenceImages (part_no, view_type, file_path, display_path, captured_at, set_no) " +
                        "VALUES ($part_no, $view_type, $file_path, $display_path, $captured_at, $set_no);";
                    SqliteDatabase.AddParameter(command, "$part_no", part.PartNo);
                    SqliteDatabase.AddParameter(command, "$view_type", (int)image.ViewType);
                    // DB 에는 루트 상대 위치로 담습니다. 절대 경로를 담으면 컴퓨터가 바뀔 때 전부 깨집니다.
                    SqliteDatabase.AddParameter(command, "$file_path", NormalizeRequired(ImagePathSettings.ToStorableImagePath(image.FilePath), "-"));
                    SqliteDatabase.AddParameter(command, "$display_path", BuildReferenceDisplayPath(part));
                    SqliteDatabase.AddParameter(command, "$captured_at", image.CapturedAt == DateTime.MinValue ? DateTime.Now.ToString("o", CultureInfo.InvariantCulture) : image.CapturedAt.ToString("o", CultureInfo.InvariantCulture));

                    // 벌 번호가 비어 있으면 1로 둡니다. 예전 자료를 옮길 때 생길 수 있습니다.
                    SqliteDatabase.AddParameter(command, "$set_no", image.SetNo < 1 ? 1 : image.SetNo);
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

        /// <summary>
        /// 화면에 적을 측정부 이름을 만듭니다.
        ///
        /// <para>
        /// 카메라와 번호를 함께 적습니다. 번호는 카메라마다 1부터 세므로
        /// 번호만으로는 어느 카메라의 것인지 알 수 없기 때문입니다.
        ///   예) Top 1 - 폭,  Thk 1 - 두께
        /// </para>
        ///
        /// <para>
        /// 이름에는 하이픈을 쓰지 않습니다. 저장할 때 "이름 - 항목"에서 하이픈으로
        /// 항목을 잘라내므로, 이름 쪽에 하이픈이 있으면 항목이 잘못 잘립니다.
        /// </para>
        /// </summary>
        private string BuildMeasurementPointName(int indexNo, string itemType, ImageViewType viewType)
        {
            return MeasurementPointPolicy.BuildPointName(viewType, indexNo) + " - " +
                   NormalizeRequired(itemType, "미설정");
        }

        private string BuildCoordinatesText(MeasurementRegion region)
        {
            if (!region.X1.HasValue || !region.Y1.HasValue || !region.X2.HasValue || !region.Y2.HasValue)
            {
                return "미지정";
            }

            return region.X1.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   region.Y1.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   region.X2.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   region.Y2.Value.ToString("0.###", CultureInfo.InvariantCulture);
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

        private double? ReadNullableDouble(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetDouble(ordinal);
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
