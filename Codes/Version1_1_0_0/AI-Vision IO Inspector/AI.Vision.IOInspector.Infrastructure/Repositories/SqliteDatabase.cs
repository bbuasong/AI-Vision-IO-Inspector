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
                    "part_type TEXT NOT NULL, " +
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
                    "UNIQUE(part_no, index_no));");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS PartList_ReferenceImages (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "part_no TEXT NOT NULL, " +
                    "view_type INTEGER NOT NULL, " +
                    "file_path TEXT NOT NULL, " +
                    "display_path TEXT NOT NULL, " +
                    "captured_at TEXT NOT NULL, " +
                    "FOREIGN KEY(part_no) REFERENCES PartList_Parts(part_no) ON DELETE CASCADE, " +
                    "UNIQUE(part_no, view_type));");

                ExecuteNonQuery(connection,
                    "CREATE TABLE IF NOT EXISTS History_Inspections (" +
                    "id INTEGER PRIMARY KEY, " +
                    "part_no TEXT, " +
                    "part_name TEXT, " +
                    "category_code TEXT, " +
                    "category_description TEXT, " +
                    "part_type TEXT, " +
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
                MigrateLegacyMeasurementPoints(connection);
                ExecuteNonQuery(connection, "INSERT OR REPLACE INTO SchemaInfo (schema_key, schema_value) VALUES ('schema_version', '2');");
                NormalizeRuntimeFilePaths(connection);
            }
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
                        point.ToleranceMin = -Math.Abs(toleranceMin);
                        point.ToleranceMax = Math.Abs(toleranceMax);
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
            using (SqliteCommand command = connection.CreateCommand())
            {
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
