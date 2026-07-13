using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SABPlus.Radial.Setup.Services
{
    public sealed class SetupDiagnosticsService
    {
        private readonly object _sync = new object();

        public string LogFilePath { get; }

        public SetupDiagnosticsService()
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAB+",
                "RadialWheel",
                "Logs");
            Directory.CreateDirectory(logDirectory);
            LogFilePath = Path.Combine(logDirectory, "setup.log");
        }

        public void WriteStep(string step, string details)
        {
            string entry = string.Format(
                "{0:yyyy-MM-dd HH:mm:ss.fff} | Thread {1} | {2} | {3}{4}",
                DateTime.Now,
                Thread.CurrentThread.ManagedThreadId,
                step ?? string.Empty,
                details ?? string.Empty,
                Environment.NewLine);

            lock (_sync)
            {
                File.AppendAllText(LogFilePath, entry, new UTF8Encoding(false));
            }
        }

        public void WriteException(string step, Exception exception)
        {
            WriteStep(step, exception?.ToString() ?? "Exception is null.");
        }
    }
}
