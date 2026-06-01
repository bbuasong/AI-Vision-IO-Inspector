using System;
using System.IO;
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
                ExecuteNonQuery(connection, "INSERT OR REPLACE INTO SchemaInfo (schema_key, schema_value) VALUES ('schema_version', '1');");

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
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_History_Inspections_InspectedAt ON History_Inspections(inspected_at);");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_History_Inspections_PartNo ON History_Inspections(part_no);");

                NormalizeRuntimeFilePaths(connection);
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
    }
}
