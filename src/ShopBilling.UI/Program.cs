using System;
using System.IO;
using System.Windows.Forms;
using ShopBilling.Core.Services;
using ShopBilling.Data.Database;
using ShopBilling.UI.Forms;

namespace ShopBilling.UI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Global exception handlers for mission-critical retail billing reliability
            Application.ThreadException += (s, e) =>
            {
                AppLogger.Error("Unhandled UI Thread Exception", e.Exception);
                MessageBox.Show($"Application Error:\n{e.Exception.Message}\n\nDetails logged to application log.",
                    "ShopBilling Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                AppLogger.Error("Fatal AppDomain Exception", ex);
                MessageBox.Show($"Fatal Error:\n{ex?.Message}", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            };

            try
            {
                // Initialize SQLite database and run versioned migrations
                AppLogger.Info("Initializing ShopBilling application shell...");
                DatabaseConnection.Initialize();
                DatabaseMigrator.RunMigrations();
                AppLogger.Info("Migrations completed successfully. Launching MainForm.");

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                AppLogger.Error("Application startup crash", ex);
                MessageBox.Show($"Application could not start:\n{ex.Message}\n\nPlease verify .NET Framework 4.8 and SQLite prerequisites.",
                    "Startup Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
