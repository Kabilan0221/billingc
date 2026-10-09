using System;
using System.Data.SQLite;
using System.IO;
using ShopBilling.Core.Services;

namespace ShopBilling.Data.Database
{
    public class DatabaseBackupService
    {
        public static string CreateBackup(string destinationFolder = null)
        {
            if (string.IsNullOrEmpty(destinationFolder))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                destinationFolder = Path.Combine(appData, "ShopBilling", "Backups");
            }

            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            string backupFileName = $"ShopBilling_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            string backupFilePath = Path.Combine(destinationFolder, backupFileName);

            using (var sourceConn = DatabaseConnection.CreateConnection())
            using (var destConn = new SQLiteConnection($"Data Source={backupFilePath};Version=3;"))
            {
                destConn.Open();
                // SQLite Online Backup API creates a safe, consistent snapshot even with active readers/writers
                sourceConn.BackupDatabase(destConn, "main", "main", -1, null, 0);
            }

            AppLogger.Info($"Database backup successfully created: {backupFilePath}");
            return backupFilePath;
        }

        public static bool RestoreBackup(string backupFilePath)
        {
            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException("Backup file does not exist.", backupFilePath);
            }

            // Verify backup file integrity before restoring
            string testConnString = $"Data Source={backupFilePath};Version=3;FailIfMissing=True;";
            using (var testConn = new SQLiteConnection(testConnString))
            {
                testConn.Open();
                using (var cmd = testConn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA quick_check;";
                    var result = cmd.ExecuteScalar()?.ToString();
                    if (result != "ok")
                    {
                        throw new InvalidOperationException($"Corrupt backup file: {result}");
                    }
                }
            }

            // Perform live restore into current active database
            using (var destConn = DatabaseConnection.CreateConnection())
            using (var sourceConn = new SQLiteConnection(testConnString))
            {
                sourceConn.Open();
                sourceConn.BackupDatabase(destConn, "main", "main", -1, null, 0);
            }

            AppLogger.Info($"Database restored successfully from: {backupFilePath}");
            return true;
        }
    }
}
