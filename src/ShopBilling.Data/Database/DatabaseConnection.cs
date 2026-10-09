using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using ShopBilling.Core.Services;

namespace ShopBilling.Data.Database
{
    public class DatabaseConnection
    {
        private static string _dbFilePath;
        private static string _connectionString;
        private static readonly object InitLock = new object();

        public static string DatabaseFilePath => _dbFilePath;

        public static void Initialize(string customPath = null)
        {
            lock (InitLock)
            {
                if (!string.IsNullOrWhiteSpace(customPath))
                {
                    _dbFilePath = customPath;
                }
                else
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var dbDir = Path.Combine(localAppData, "ShopBilling", "Data");
                    if (!Directory.Exists(dbDir))
                    {
                        Directory.CreateDirectory(dbDir);
                    }
                    _dbFilePath = Path.Combine(dbDir, "shopbilling.db");
                }

                // SQLite connection string optimized for high performance and reliability on Windows 7
                // - WAL mode allows concurrent readers while writing
                // - Cache Size -64000 allocates 64 MB memory cache for sub-millisecond 50,000 item lookups
                // - Synchronous Normal provides data safety against crashes without sacrificing write performance
                // - Foreign Keys = True ensures strict referential integrity
                _connectionString = $"Data Source={_dbFilePath};Version=3;Foreign Keys=True;Journal Mode=WAL;Synchronous=Normal;Cache Size=-64000;FailIfMissing=False;";

                AppLogger.Info($"Database initialized at: {_dbFilePath}");
            }
        }

        public static SQLiteConnection CreateConnection()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                Initialize();
            }

            var conn = new SQLiteConnection(_connectionString);
            conn.Open();

            // Set pragmas for every connection
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA foreign_keys = ON;
                    PRAGMA journal_mode = WAL;
                    PRAGMA temp_store = MEMORY;
                    PRAGMA mmap_size = 268435456; -- 256MB memory-mapped I/O for 50,000+ item speed
                ";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        public static string GetConnectionString() => _connectionString;
    }
}
