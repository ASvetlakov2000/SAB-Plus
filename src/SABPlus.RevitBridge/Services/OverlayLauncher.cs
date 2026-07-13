using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace SABPlus.RevitBridge.Services
{
    public sealed class OverlayLaunchResult
    {
        public string OverlayPath { get; }

        public int StartedProcessId { get; }

        public OverlayLaunchResult(string overlayPath, int startedProcessId)
        {
            OverlayPath = overlayPath ?? string.Empty;
            StartedProcessId = startedProcessId;
        }
    }

    public static class OverlayLauncher
    {
        private const string OverlayFileName = "SABPlus.Radial.Overlay.exe";

        public static OverlayLaunchResult OpenSettings()
        {
            string overlayPath = FindOverlayPath();
            if (string.IsNullOrWhiteSpace(overlayPath))
            {
                throw new FileNotFoundException(
                    "Не найден " + OverlayFileName + ". Переустановите SAB+ или укажите переменную SABPLUS_RADIAL_OVERLAY_PATH.");
            }

            int revitProcessId = Process.GetCurrentProcess().Id;
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = overlayPath,
                Arguments = "--settings --revit-process " + revitProcessId,
                WorkingDirectory = Path.GetDirectoryName(overlayPath),
                UseShellExecute = true
            };

            Process process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Windows не вернула запущенный процесс редактора колеса.");
            }

            return new OverlayLaunchResult(overlayPath, process.Id);
        }

        public static string FindOverlayPath()
        {
            List<string> candidates = new List<string>();
            string configuredPath = Environment.GetEnvironmentVariable("SABPLUS_RADIAL_OVERLAY_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                candidates.Add(Environment.ExpandEnvironmentVariables(configuredPath));
            }

            // Standard per-user installation created by SABPlus.Radial.Setup.
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAB+",
                "RadialWheel",
                "App",
                OverlayFileName));

            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                candidates.Add(Path.Combine(assemblyDirectory, OverlayFileName));

                // Development layout: sibling projects under the same src directory.
                candidates.Add(Path.GetFullPath(Path.Combine(
                    assemblyDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "SABPlus.Radial.Overlay",
                    "bin",
                    "Debug",
                    "net48",
                    OverlayFileName)));

                candidates.Add(Path.GetFullPath(Path.Combine(
                    assemblyDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "SABPlus.Radial.Overlay",
                    "bin",
                    "Release",
                    "net48",
                    OverlayFileName)));
            }

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return string.Empty;
        }
    }
}
