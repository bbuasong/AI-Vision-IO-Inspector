using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure;
using Microsoft.Data.Sqlite;

namespace AI.Vision.IOInspector.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite DB 파일과 스키마 생성을 담당합니다.
    /// 기준정보 적재는 별도 1회성 작업으로 처리하고, 앱 실행 중에는 외부 CSV 파일을 참조하지 않습니다.
    /// </summary>
    public class SqliteDatabase
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        public SqliteDatabase(string applicationRootPath)
        {
            string dataRootPath = ResolveDataRootPath(applicationRootPath);
            string databaseFolderPath = Path.Combine(dataRootPath, "DB");
            Directory.CreateDirectory(databaseFolderPath);

            _databasePath = Path.Combine(databaseFolderPath, "DataBase.db");
            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder();
            builder.DataSource = _databasePath;
            _connectionString = builder.ToString();

            EnsureSchema();
        }

        public string DatabasePath
        {
            get { return _databasePath; }
        }

        public SqliteConnection OpenConnection()
        {
            SqliteConnection connection = new SqliteConnection(_connectionString);
            connection.Open();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON;";
                command.ExecuteNonQuery();
            }

            return connection;
        }

        private string ResolveDataRootPath(string applicationRootPath)
        {
            return ProjectDataRootResolver.Resolve(applicationRootPath);
        }

        private void EnsureSchema()
        {
            using (SqliteConnection connection = OpenConnection())
            {
                ExecuteNonQuery(connection, "PRAGMA journal_mode = WAL;");
                ExecuteNonQuery(connection, "CREATE TABLE IF NOT EXISTS SchemaInfo (schema_key TEXT PRIMARY KEY, schema_value TEXT NOT NULL);");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS PartList_Categories (" +
                    "category_code TEXT PRIMARY KEY, " +
                    "category_description TEXT NOT NULL);");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS PartList_Parts (" +
                    "part_no TEXT PRIMARY KEY, " +
                    "part_name TEXT NOT NULL, " +
                    "category_code TEXT NOT NULL, " +
                    "category_description TEXT NOT NULL, " +
                    "memo TEXT NOT NULL, " +
                    "created_at TEXT NOT NULL, " +
                    "updated_at TEXT NOT NULL, " +
                    "FOREIGN KEY(category_code) REFERENCES PartList_Categories(category_code));");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS PartList_MeasurementSets (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "part_no TEXT NOT NULL, " +
                    "set_index INTEGER NOT NULL, " +
                    "set_name TEXT NOT NULL, " +
                    "FOREIGN KEY(part_no) REFERENCES PartList_Parts(part_no) ON DELETE CASCADE, " +
                    "UNIQUE(part_no, set_index));");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS PartList_MeasurementItems (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "set_id INTEGER NOT NULL, " +
                    "item_name TEXT NOT NULL, " +
                    "item_order INTEGER NOT NULL, " +
                    "view_type INTEGER NOT NULL, " +
                    "nominal_value REAL NOT NULL, " +
                    "tolerance_min REAL NOT NULL, " +
                    "tolerance_max REAL NOT NULL, " +
                    "unit TEXT NOT NULL, " +
                    "is_used INTEGER NOT NULL, " +
                    "coordinates TEXT NOT NULL, " +
                    "FOREIGN KEY(set_id) REFERENCES PartList_MeasurementSets(id) ON DELETE CASCADE, " +
                    "UNIQUE(set_id, item_name));");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS PartList_MeasurementPoints (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "part_no TEXT NOT NULL, " +
                    "index_no INTEGER NOT NULL, " +
                    "item_type TEXT NOT NULL, " +
                    "view_type INTEGER NOT NULL, " +
                    "nominal_value REAL NOT NULL, " +
                    "tolerance REAL NOT NULL, " +
                    "tolerance_min REAL NOT NULL DEFAULT 0, " +
                    "tolerance_max REAL NOT NULL DEFAULT 0, " +
                    "unit TEXT NOT NULL, " +
                    "x1 REAL, " +
                    "y1 REAL, " +
                    "x2 REAL, " +
                    "y2 REAL, " +
                    "line_color TEXT NOT NULL, " +
                    "FOREIGN KEY(part_no) REFERENCES PartList_Parts(part_no) ON DELETE CASCADE, " +
                    "UNIQUE(part_no, view_type, index_no));");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS PartList_ReferenceImages (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "part_no TEXT NOT NULL, " +
                    "view_type INTEGER NOT NULL, " +
                    "file_path TEXT NOT NULL, " +
                    "display_path TEXT NOT NULL, " +
                    "captured_at TEXT NOT NULL, " +
                    "set_no INTEGER NOT NULL DEFAULT 1, " +
                    "FOREIGN KEY(part_no) REFERENCES PartList_Parts(part_no) ON DELETE CASCADE);");

                // 같은 부품의 같은 방향을 시각으로 구분해 여러 벌 보관합니다.
                // 조회는 부품별로 하고 최근 벌을 먼저 보므로 이 순서로 색인을 둡니다.
                ExecuteNonQuery(connection,
                    "CREATE INDEX IF NOT EXISTS IX_PartList_ReferenceImages_PartNo " +
                    "ON PartList_ReferenceImages(part_no, captured_at DESC);");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS History_Inspections (" +
                    "id INTEGER PRIMARY KEY, " +
                    "part_no TEXT, " +
                    "part_name TEXT, " +
                    "category_code TEXT, " +
                    "category_description TEXT, " +
                    "memo TEXT, " +
                    "input_code TEXT, " +
                    "result INTEGER NOT NULL, " +
                    "inspected_at TEXT NOT NULL, " +
                    "elapsed_ms REAL NOT NULL, " +
                    "result_message TEXT);");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS History_Measurements (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "inspection_id INTEGER NOT NULL, " +
                    "measurement_region_id INTEGER NOT NULL, " +
                    "name TEXT NOT NULL, " +
                    "nominal_value REAL NOT NULL, " +
                    "measured_value REAL NOT NULL, " +
                    "tolerance_min REAL NOT NULL, " +
                    "tolerance_max REAL NOT NULL, " +
                    "unit TEXT NOT NULL, " +
                    "is_ok INTEGER NOT NULL, " +
                    "deviation REAL NOT NULL DEFAULT 0, " +
                    "message TEXT, " +
                    "FOREIGN KEY(inspection_id) REFERENCES History_Inspections(id) ON DELETE CASCADE);");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS History_CapturedImages (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "inspection_id INTEGER NOT NULL, " +
                    "view_type INTEGER NOT NULL, " +
                    "display_name TEXT, " +
                    "file_path TEXT, " +
                    "captured_at TEXT NOT NULL, " +
                    "FOREIGN KEY(inspection_id) REFERENCES History_Inspections(id) ON DELETE CASCADE);");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS History_Events (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "inspection_id INTEGER NOT NULL, " +
                    "severity INTEGER NOT NULL, " +
                    "source TEXT, " +
                    "message TEXT, " +
                    "created_at TEXT NOT NULL, " +
                    "FOREIGN KEY(inspection_id) REFERENCES History_Inspections(id) ON DELETE CASCADE);");

                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_PartList_Parts_CategoryCode ON PartList_Parts(category_code);");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_PartList_MeasurementSets_PartNo ON PartList_MeasurementSets(part_no);");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_PartList_MeasurementPoints_PartNo ON PartList_MeasurementPoints(part_no);");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_History_Inspections_InspectedAt ON History_Inspections(inspected_at);");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_History_Inspections_PartNo ON History_Inspections(part_no);");

                EnsureMeasurementPointToleranceColumns(connection);
                EnsureMeasurementDeviationColumn(connection);
                EnsureReferenceImagesAllowMultipleSets(connection);
                EnsureReferenceImageSetNoColumn(connection);
                EnsurePartTypeRenamedToMemo(connection);
                EnsureMeasurementPointUniqueByViewType(connection);
                MigrateLegacyMeasurementPoints(connection);
                ExecuteNonQuery(connection, "INSERT OR REPLACE INTO SchemaInfo (schema_key, schema_value) VALUES ('schema_version', '2');");
                NormalizeRuntimeFilePaths(connection);
            }
        }

        /// <summary>
        /// 측정부의 유일 조건에 카메라를 더합니다.
        ///
        /// <para>
        /// 예전에는 부품 안에서 번호가 하나뿐이라 (품번, 번호)로 충분했습니다.
        /// 이제 측정부를 카메라마다 따로 관리하고 번호도 각각 1부터 세므로,
        /// Top 1번과 Thickness 1번이 함께 있습니다. 카메라를 빼면 그 둘이 충돌합니다.
        /// </para>
        ///
        /// <para>
        /// SQLite는 제약만 바꾸는 명령이 없어 표를 다시 만들어 옮깁니다.
        /// 이미 카메라가 들어간 조건이면 아무것도 하지 않습니다.
        /// </para>
        /// </summary>
        private void EnsureMeasurementPointUniqueByViewType(SqliteConnection connection)
        {
            if (!TableExists(connection, "PartList_MeasurementPoints"))
            {
                return;
            }

            if (TableDefinitionContains(connection, "PartList_MeasurementPoints", "UNIQUE(part_no, view_type, index_no)"))
            {
                return;
            }

            if (!TableDefinitionContains(connection, "PartList_MeasurementPoints", "UNIQUE(part_no, index_no)"))
            {
                return;
            }

            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    ExecuteNonQuery(connection, transaction,
                        "CREATE TABLE PartList_MeasurementPoints_New (" +
                        "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "part_no TEXT NOT NULL, " +
                        "index_no INTEGER NOT NULL, " +
                        "item_type TEXT NOT NULL, " +
                        "view_type INTEGER NOT NULL, " +
                        "nominal_value REAL NOT NULL, " +
                        "tolerance REAL NOT NULL, " +
                        "tolerance_min REAL NOT NULL DEFAULT 0, " +
                        "tolerance_max REAL NOT NULL DEFAULT 0, " +
                        "unit TEXT NOT NULL, " +
                        "x1 REAL, " +
                        "y1 REAL, " +
                        "x2 REAL, " +
                        "y2 REAL, " +
                        "line_color TEXT NOT NULL, " +
                        "FOREIGN KEY(part_no) REFERENCES PartList_Parts(part_no) ON DELETE CASCADE, " +
                        "UNIQUE(part_no, view_type, index_no));");

                    ExecuteNonQuery(connection, transaction,
                        "INSERT INTO PartList_MeasurementPoints_New " +
                        "(id, part_no, index_no, item_type, view_type, nominal_value, tolerance, " +
                        " tolerance_min, tolerance_max, unit, x1, y1, x2, y2, line_color) " +
                        "SELECT id, part_no, index_no, item_type, view_type, nominal_value, tolerance, " +
                        " tolerance_min, tolerance_max, unit, x1, y1, x2, y2, line_color " +
                        "FROM PartList_MeasurementPoints;");

                    ExecuteNonQuery(connection, transaction, "DROP TABLE PartList_MeasurementPoints;");
                    ExecuteNonQuery(connection, transaction,
                        "ALTER TABLE PartList_MeasurementPoints_New RENAME TO PartList_MeasurementPoints;");
                    ExecuteNonQuery(connection, transaction,
                        "CREATE INDEX IF NOT EXISTS IX_PartList_MeasurementPoints_PartNo " +
                        "ON PartList_MeasurementPoints(part_no);");

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// 부품의 '구분'을 '메모'로 부르기로 하면서 열 이름도 memo로 맞춥니다.
        ///
        /// <para>
        /// 이름만 바꾸는 것이라 값은 그대로 옮겨집니다. 이미 memo인 DB에서는 아무것도 하지 않으므로
        /// 여러 번 기동해도 안전합니다.
        /// </para>
        ///
        /// <para>
        /// 두 표에 같은 열이 있습니다.
        ///   PartList_Parts        부품 기준정보
        ///   History_Inspections   검사 이력(검사 시점의 값을 함께 남깁니다)
        /// </para>
        /// </summary>
        private void EnsurePartTypeRenamedToMemo(SqliteConnection connection)
        {
            RenameColumnIfNeeded(connection, "PartList_Parts", "part_type", "memo");
            RenameColumnIfNeeded(connection, "History_Inspections", "part_type", "memo");
        }

        private void RenameColumnIfNeeded(
            SqliteConnection connection,
            string tableName,
            string oldColumnName,
            string newColumnName)
        {
            if (!TableExists(connection, tableName))
            {
                return;
            }

            if (ColumnExists(connection, tableName, newColumnName))
            {
                return;
            }

            if (!ColumnExists(connection, tableName, oldColumnName))
            {
                return;
            }

            ExecuteNonQuery(connection,
                "ALTER TABLE " + tableName + " RENAME COLUMN " + oldColumnName + " TO " + newColumnName + ";");
        }

        /// <summary>
        /// 기준 이미지에 벌 번호 열을 붙입니다.
        ///
        /// <para>
        /// 이미 쓰고 있던 이미지에는 번호가 없으므로, 부품별로 저장 시각이 이른 것부터
        /// 1, 2, 3... 을 매깁니다. 같은 시각에 저장된 것들은 한 벌이므로 같은 번호를 받습니다.
        /// 저장 시각이 곧 벌의 구분이라 이 방식으로 예전 자료도 벌로 묶입니다.
        /// </para>
        /// </summary>
        private void EnsureReferenceImageSetNoColumn(SqliteConnection connection)
        {
            if (!TableExists(connection, "PartList_ReferenceImages"))
            {
                return;
            }

            if (ColumnExists(connection, "PartList_ReferenceImages", "set_no"))
            {
                return;
            }

            EnsureColumnExists(connection, "PartList_ReferenceImages", "set_no", "INTEGER NOT NULL DEFAULT 1");

            // 부품별로 저장 시각이 이른 순서대로 번호를 매깁니다.
            // 같은 시각(한 벌)은 같은 번호가 되도록 DISTINCT 시각을 셉니다.
            ExecuteNonQuery(connection,
                "UPDATE PartList_ReferenceImages " +
                "SET set_no = (" +
                "  SELECT COUNT(DISTINCT inner_images.captured_at) " +
                "  FROM PartList_ReferenceImages AS inner_images " +
                "  WHERE inner_images.part_no = PartList_ReferenceImages.part_no " +
                "    AND inner_images.captured_at <= PartList_ReferenceImages.captured_at);");
        }

        /// <summary>
        /// 기준 이미지를 여러 벌 보관할 수 있도록 UNIQUE(part_no, view_type) 제약을 걷어냅니다.
        ///
        /// <para>
        /// 예전에는 부품+방향마다 한 장만 두는 것을 DB가 강제했습니다. 이제 저장할 때마다
        /// 그 시각의 이미지가 한 벌로 쌓이므로 같은 조합이 여러 번 들어갑니다.
        /// SQLite는 제약만 떼어내는 명령이 없어 표를 다시 만들어 옮깁니다.
        /// </para>
        ///
        /// <para>
        /// 이미 제약이 없으면 아무것도 하지 않습니다. 기존 행은 그대로 옮겨집니다.
        /// </para>
        /// </summary>
        private void EnsureReferenceImagesAllowMultipleSets(SqliteConnection connection)
        {
            if (!TableExists(connection, "PartList_ReferenceImages"))
            {
                return;
            }

            if (!TableDefinitionContains(connection, "PartList_ReferenceImages", "UNIQUE(part_no, view_type)"))
            {
                return;
            }

            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    ExecuteNonQuery(connection, transaction,
                        "CREATE TABLE PartList_ReferenceImages_New (" +
                        "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "part_no TEXT NOT NULL, " +
                        "view_type INTEGER NOT NULL, " +
                        "file_path TEXT NOT NULL, " +
                        "display_path TEXT NOT NULL, " +
                        "captured_at TEXT NOT NULL, " +
                        "set_no INTEGER NOT NULL DEFAULT 1, " +
                        "FOREIGN KEY(part_no) REFERENCES PartList_Parts(part_no) ON DELETE CASCADE);");

                    // 예전 표에는 set_no가 없을 수 있습니다. 그때는 1로 채우고,
                    // 뒤이어 도는 EnsureReferenceImageSetNoColumn이 시각 순서대로 다시 매깁니다.
                    string setNoSource = ColumnExists(connection, "PartList_ReferenceImages", "set_no")
                        ? "set_no"
                        : "1";

                    ExecuteNonQuery(connection, transaction,
                        "INSERT INTO PartList_ReferenceImages_New " +
                        "(id, part_no, view_type, file_path, display_path, captured_at, set_no) " +
                        "SELECT id, part_no, view_type, file_path, display_path, captured_at, " + setNoSource + " " +
                        "FROM PartList_ReferenceImages;");

                    ExecuteNonQuery(connection, transaction, "DROP TABLE PartList_ReferenceImages;");
                    ExecuteNonQuery(connection, transaction,
                        "ALTER TABLE PartList_ReferenceImages_New RENAME TO PartList_ReferenceImages;");
                    ExecuteNonQuery(connection, transaction,
                        "CREATE INDEX IF NOT EXISTS IX_PartList_ReferenceImages_PartNo " +
                        "ON PartList_ReferenceImages(part_no, captured_at DESC);");

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private bool TableExists(SqliteConnection connection, string tableName)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
                AddParameter(command, "$name", tableName);
                return Convert.ToInt64(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// 표를 만들 때 쓴 SQL에 지정한 문구가 있는지 봅니다. 제약 존재 여부를 확인하는 데 씁니다.
        /// 공백이 다를 수 있으므로 공백을 지우고 비교합니다.
        /// </summary>
        private bool TableDefinitionContains(SqliteConnection connection, string tableName, string fragment)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;";
                AddParameter(command, "$name", tableName);

                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return false;
                }

                string definition = Convert.ToString(value).Replace(" ", string.Empty);
                string target = fragment.Replace(" ", string.Empty);
                return definition.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>
        /// 측정부가 허용 범위를 얼마나 벗어났는지 기록하는 열입니다.
        /// 이미 쓰고 있는 DB에도 붙여야 하므로 없을 때만 추가합니다.
        /// 기존 행은 0으로 남으며, 그 값은 "벗어남 없음"이 아니라 "기록 전"을 뜻합니다.
        /// </summary>
        private void EnsureMeasurementDeviationColumn(SqliteConnection connection)
        {
            EnsureColumnExists(connection, "History_Measurements", "deviation", "REAL NOT NULL DEFAULT 0");
        }

        private void EnsureMeasurementPointToleranceColumns(SqliteConnection connection)
        {
            EnsureColumnExists(connection, "PartList_MeasurementPoints", "tolerance_min", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(connection, "PartList_MeasurementPoints", "tolerance_max", "REAL NOT NULL DEFAULT 0");
            ExecuteNonQuery(connection,
                "UPDATE PartList_MeasurementPoints " +
                "SET tolerance_min = -ABS(tolerance), tolerance_max = ABS(tolerance) " +
                "WHERE tolerance <> 0 AND tolerance_min = 0 AND tolerance_max = 0;");
        }

        private void MigrateLegacyMeasurementPoints(SqliteConnection connection)
        {
            if (ReadCount(connection, "PartList_MeasurementPoints") > 0 ||
                ReadCount(connection, "PartList_MeasurementItems") == 0)
            {
                return;
            }

            IList<LegacyMeasurementPoint> legacyPoints = new List<LegacyMeasurementPoint>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT s.part_no, i.item_name, i.view_type, i.nominal_value, i.tolerance_min, i.tolerance_max, i.unit, i.coordinates " +
                    "FROM PartList_MeasurementSets s " +
                    "INNER JOIN PartList_MeasurementItems i ON i.set_id = s.id " +
                    "WHERE i.is_used = 1 " +
                    "ORDER BY s.part_no, s.set_index, i.item_order, i.id;";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        LegacyMeasurementPoint point = new LegacyMeasurementPoint();
                        point.PartNo = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        point.ItemType = reader.IsDBNull(1) ? "미설정" : reader.GetString(1);
                        point.ViewType = reader.IsDBNull(2) ? 5 : Convert.ToInt32(reader.GetInt64(2));
                        point.NominalValue = reader.IsDBNull(3) ? 0d : reader.GetDouble(3);
                        double toleranceMin = reader.IsDBNull(4) ? 0d : reader.GetDouble(4);
                        double toleranceMax = reader.IsDBNull(5) ? 0d : reader.GetDouble(5);
                        point.ToleranceMin = toleranceMin;
                        point.ToleranceMax = toleranceMax;
                        point.Tolerance = Math.Max(Math.Abs(toleranceMin), Math.Abs(toleranceMax));
                        point.Unit = reader.IsDBNull(6) ? "mm" : reader.GetString(6);
                        point.Coordinates = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                        legacyPoints.Add(point);
                    }
                }
            }

            Dictionary<string, int> indexByPartNo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                foreach (LegacyMeasurementPoint point in legacyPoints)
                {
                    int currentIndex = indexByPartNo.ContainsKey(point.PartNo) ? indexByPartNo[point.PartNo] + 1 : 1;
                    indexByPartNo[point.PartNo] = currentIndex;
                    if (currentIndex > MeasurementPointPolicy.MaxCount)
                    {
                        continue;
                    }

                    double? x1;
                    double? y1;
                    double? x2;
                    double? y2;
                    ParseCoordinates(point.Coordinates, out x1, out y1, out x2, out y2);

                    using (SqliteCommand insertCommand = connection.CreateCommand())
                    {
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText =
                            "INSERT OR IGNORE INTO PartList_MeasurementPoints " +
                            "(part_no, index_no, item_type, view_type, nominal_value, tolerance, tolerance_min, tolerance_max, unit, x1, y1, x2, y2, line_color) " +
                            "VALUES ($part_no, $index_no, $item_type, $view_type, $nominal_value, $tolerance, $tolerance_min, $tolerance_max, $unit, $x1, $y1, $x2, $y2, $line_color);";
                        AddParameter(insertCommand, "$part_no", point.PartNo);
                        AddParameter(insertCommand, "$index_no", currentIndex);
                        AddParameter(insertCommand, "$item_type", string.IsNullOrWhiteSpace(point.ItemType) ? "미설정" : point.ItemType);
                        AddParameter(insertCommand, "$view_type", 5);
                        AddParameter(insertCommand, "$nominal_value", point.NominalValue);
                        AddParameter(insertCommand, "$tolerance", point.Tolerance);
                        AddParameter(insertCommand, "$tolerance_min", point.ToleranceMin);
                        AddParameter(insertCommand, "$tolerance_max", point.ToleranceMax);
                        AddParameter(insertCommand, "$unit", string.IsNullOrWhiteSpace(point.Unit) ? "mm" : point.Unit);
                        AddParameter(insertCommand, "$x1", x1);
                        AddParameter(insertCommand, "$y1", y1);
                        AddParameter(insertCommand, "$x2", x2);
                        AddParameter(insertCommand, "$y2", y2);
                        AddParameter(insertCommand, "$line_color", MeasurementPointPolicy.GetDefaultColor(currentIndex));
                        insertCommand.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
        }

        private long ReadCount(SqliteConnection connection, string tableName)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM " + tableName + ";";
                object value = command.ExecuteScalar();
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
        }

        private void EnsureColumnExists(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            if (ColumnExists(connection, tableName, columnName))
            {
                return;
            }

            ExecuteNonQuery(connection, "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDefinition + ";");
        }

        private bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(" + tableName + ");";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string currentColumnName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        if (string.Equals(currentColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void ParseCoordinates(
            string coordinates,
            out double? x1,
            out double? y1,
            out double? x2,
            out double? y2)
        {
            x1 = null;
            y1 = null;
            x2 = null;
            y2 = null;
            if (string.IsNullOrWhiteSpace(coordinates))
            {
                return;
            }

            string[] values = coordinates.Split(',');
            if (values.Length != 4)
            {
                return;
            }

            double parsedX1;
            double parsedY1;
            double parsedX2;
            double parsedY2;
            if (double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedX1) &&
                double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedY1) &&
                double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedX2) &&
                double.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out parsedY2))
            {
                x1 = parsedX1;
                y1 = parsedY1;
                x2 = parsedX2;
                y2 = parsedY2;
            }
        }

        private void NormalizeRuntimeFilePaths(SqliteConnection connection)
        {
            string databaseFolderPath = Path.GetDirectoryName(_databasePath);
            if (string.IsNullOrWhiteSpace(databaseFolderPath))
            {
                return;
            }

            string currentImageRootPath = Path.Combine(databaseFolderPath, "Image");
            string oldBuildOutputImageRootPath = Path.Combine(AppContext.BaseDirectory, "DB", "Image");
            ReplaceStoredPathRoot(connection, "PartList_ReferenceImages", "file_path", oldBuildOutputImageRootPath, currentImageRootPath);
        }

        private void ReplaceStoredPathRoot(SqliteConnection connection, string tableName, string columnName, string oldRootPath, string newRootPath)
        {
            if (string.IsNullOrWhiteSpace(oldRootPath) || string.IsNullOrWhiteSpace(newRootPath))
            {
                return;
            }

            string normalizedOldRootPath = Path.GetFullPath(oldRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedNewRootPath = Path.GetFullPath(newRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(normalizedOldRootPath, normalizedNewRootPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "UPDATE " + tableName + " " +
                    "SET " + columnName + " = REPLACE(" + columnName + ", $old_root, $new_root) " +
                    "WHERE " + columnName + " LIKE $old_root_like;";
                AddParameter(command, "$old_root", normalizedOldRootPath);
                AddParameter(command, "$new_root", normalizedNewRootPath);
                AddParameter(command, "$old_root_like", normalizedOldRootPath + "%");
                command.ExecuteNonQuery();
            }
        }

        private void ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            ExecuteNonQuery(connection, null, sql);
        }

        /// <summary>
        /// 트랜잭션 안에서 실행합니다. 표를 다시 만드는 이전 작업처럼 중간에 실패하면
        /// 되돌려야 하는 경우에 씁니다. transaction이 null이면 단독 실행과 같습니다.
        /// </summary>
        private void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public static void AddParameter(SqliteCommand command, string name, object value)
        {
            if (value == null)
            {
                command.Parameters.AddWithValue(name, DBNull.Value);
            }
            else
            {
                command.Parameters.AddWithValue(name, value);
            }
        }

        private class LegacyMeasurementPoint
        {
            public string PartNo { get; set; }

            public string ItemType { get; set; }

            public int ViewType { get; set; }

            public double NominalValue { get; set; }

            public double Tolerance { get; set; }

            public double ToleranceMin { get; set; }

            public double ToleranceMax { get; set; }

            public string Unit { get; set; }

            public string Coordinates { get; set; }
        }
    }
}
