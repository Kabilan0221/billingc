using System;
using System.IO;

namespace ShopBilling.Core.Services
{
    public static class AppLogger
    {
        private static readonly object LockObj = new object();
        private static string _logDirectory;

        static AppLogger()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _logDirectory = Path.Combine(appData, "ShopBilling", "Logs");
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
                _logDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static void SetCustomDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _logDirectory = path;
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception ex = null)
        {
            var msg = ex != null ? $"{message} | Exception: {ex.Message} \nStack: {ex.StackTrace}" : message;
            Write("ERROR", msg);
        }

        private static void Write(string level, string message)
        {
            lock (LockObj)
            {
                try
                {
                    var fileName = Path.Combine(_logDirectory, $"ShopBilling_{DateTime.Now:yyyyMMdd}.log");
                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(fileName, line);
                }
                catch
                {
                    // Fail-safe: Avoid crashing application on log write failures
                }
            }
        }
    }
}
