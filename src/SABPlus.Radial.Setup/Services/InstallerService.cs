using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace SABPlus.Radial.Setup.Services
{
    public sealed class InstallerService
    {
        private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SABPlus.RadialWheel";
        private const string AddinFileName = "SABPlus.RevitBridge.addin";
        private const string OverlayProcessName = "SABPlus.Radial.Overlay";

        private static readonly string[] PayloadFiles =
        {
            "SABPlus.Radial.Overlay.exe",
            "SABPlus.Radial.Overlay.exe.config",
            "SABPlus.RevitBridge.dll",
            "SABPlus.Radial.Core.dll",
            "Newtonsoft.Json.dll"
        };

        public string InstallDirectory { get; }

        public bool IsInstalled => File.Exists(Path.Combine(InstallDirectory, "SABPlus.Radial.Overlay.exe"));

        public InstallerService()
        {
            InstallDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAB+",
                "RadialWheel",
                "App");
        }

        public void Install(bool installRevit2023, bool installRevit2024)
        {
            string parentDirectory = Path.GetDirectoryName(InstallDirectory);
            string stagingDirectory = Path.Combine(parentDirectory, "App.installing");
            string backupDirectory = Path.Combine(parentDirectory, "App.previous");
            Directory.CreateDirectory(parentDirectory);
            StopInstalledOverlay();

            DeleteDirectoryIfExists(stagingDirectory);
            DeleteDirectoryIfExists(backupDirectory);
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                ExtractPayload(stagingDirectory);
                File.Copy(
                    Process.GetCurrentProcess().MainModule.FileName,
                    Path.Combine(stagingDirectory, "SABPlus.Radial.Setup.exe"),
                    true);

                if (Directory.Exists(InstallDirectory))
                {
                    Directory.Move(InstallDirectory, backupDirectory);
                }

                Directory.Move(stagingDirectory, InstallDirectory);
                if (installRevit2023)
                {
                    WriteAddinManifest("2023");
                }
                else
                {
                    DeleteAddinManifest("2023");
                }

                if (installRevit2024)
                {
                    WriteAddinManifest("2024");
                }
                else
                {
                    DeleteAddinManifest("2024");
                }

                WriteUninstallRegistration();
                DeleteDirectoryIfExists(backupDirectory);
            }
            catch
            {
                DeleteDirectoryIfExists(stagingDirectory);
                if (Directory.Exists(InstallDirectory))
                {
                    DeleteDirectoryIfExists(InstallDirectory);
                }

                if (Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, InstallDirectory);
                }

                throw;
            }
        }

        public void LaunchOverlay()
        {
            string overlayPath = Path.Combine(InstallDirectory, "SABPlus.Radial.Overlay.exe");
            if (!File.Exists(overlayPath))
            {
                throw new FileNotFoundException("Не найден установленный Overlay.", overlayPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = overlayPath,
                WorkingDirectory = InstallDirectory,
                UseShellExecute = true
            });
        }

        public void BeginSelfUninstall()
        {
            string currentExecutable = Process.GetCurrentProcess().MainModule.FileName;
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "SABPlus.Radial.Setup");
            Directory.CreateDirectory(temporaryDirectory);
            string temporaryExecutable = Path.Combine(temporaryDirectory, "SABPlus.Radial.Setup." + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(currentExecutable, temporaryExecutable, true);

            Process.Start(new ProcessStartInfo
            {
                FileName = temporaryExecutable,
                Arguments = "--remove-installation \"" + InstallDirectory + "\" " + Process.GetCurrentProcess().Id,
                WorkingDirectory = temporaryDirectory,
                UseShellExecute = true
            });
        }

        public static void RemoveInstallation(string installDirectory, int installerProcessId)
        {
            string expectedInstallDirectory = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAB+",
                "RadialWheel",
                "App"));
            string requestedInstallDirectory = Path.GetFullPath(installDirectory ?? string.Empty);
            if (!string.Equals(
                    requestedInstallDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    expectedInstallDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Отказано в удалении неожиданного пути: " + requestedInstallDirectory);
            }

            if (installerProcessId > 0)
            {
                try
                {
                    using (Process process = Process.GetProcessById(installerProcessId))
                    {
                        process.WaitForExit(10000);
                    }
                }
                catch
                {
                    // The original installer may have already exited.
                }
            }

            StopOverlayAtPath(requestedInstallDirectory);
            DeleteDirectoryIfExists(requestedInstallDirectory);
            DeleteAddinManifestStatic("2023");
            DeleteAddinManifestStatic("2024");
            Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, false);
        }

        private void ExtractPayload(string targetDirectory)
        {
            Assembly assembly = typeof(InstallerService).Assembly;
            foreach (string fileName in PayloadFiles)
            {
                string resourceName = "SABPlus.Payload." + fileName;
                using (Stream input = assembly.GetManifestResourceStream(resourceName))
                {
                    if (input == null)
                    {
                        throw new InvalidDataException("В установщике отсутствует ресурс: " + resourceName);
                    }

                    using (FileStream output = new FileStream(
                        Path.Combine(targetDirectory, fileName),
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        input.CopyTo(output);
                        output.Flush(true);
                    }
                }
            }
        }

        private void WriteAddinManifest(string revitVersion)
        {
            string directory = GetAddinDirectory(revitVersion);
            Directory.CreateDirectory(directory);
            string targetPath = Path.Combine(directory, AddinFileName);
            string temporaryPath = targetPath + ".tmp";
            string bridgePath = Path.Combine(InstallDirectory, "SABPlus.RevitBridge.dll");
            string xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>" + Environment.NewLine +
                "<RevitAddIns>" + Environment.NewLine +
                "  <AddIn Type=\"Application\">" + Environment.NewLine +
                "    <Name>SAB+ Radial Bridge</Name>" + Environment.NewLine +
                "    <Assembly>" + EscapeXml(bridgePath) + "</Assembly>" + Environment.NewLine +
                "    <AddInId>761F15C3-4516-43CA-AFA7-8A8D16B1C60A</AddInId>" + Environment.NewLine +
                "    <FullClassName>SABPlus.RevitBridge.App</FullClassName>" + Environment.NewLine +
                "    <VendorId>SABP</VendorId>" + Environment.NewLine +
                "    <VendorDescription>SAB+ Revit Productivity Tools</VendorDescription>" + Environment.NewLine +
                "  </AddIn>" + Environment.NewLine +
                "</RevitAddIns>" + Environment.NewLine;
            File.WriteAllText(temporaryPath, xml, new UTF8Encoding(false));
            if (File.Exists(targetPath))
            {
                File.Replace(temporaryPath, targetPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
        }

        private void DeleteAddinManifest(string revitVersion)
        {
            DeleteAddinManifestStatic(revitVersion);
        }

        private void WriteUninstallRegistration()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath))
            {
                string setupPath = Path.Combine(InstallDirectory, "SABPlus.Radial.Setup.exe");
                key.SetValue("DisplayName", "SAB+ Радиальное колесо");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "SAB+");
                key.SetValue("InstallLocation", InstallDirectory);
                key.SetValue("UninstallString", "\"" + setupPath + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private void StopInstalledOverlay()
        {
            StopOverlayAtPath(InstallDirectory);
        }

        private static void StopOverlayAtPath(string installDirectory)
        {
            string expectedPath = Path.Combine(installDirectory, "SABPlus.Radial.Overlay.exe");
            foreach (Process process in Process.GetProcessesByName(OverlayProcessName))
            {
                using (process)
                {
                    try
                    {
                        string actualPath = process.MainModule.FileName;
                        if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch
                    {
                        // Copying will produce a clear error if a matching process still locks a file.
                    }
                }
            }
        }

        private static void DeleteAddinManifestStatic(string revitVersion)
        {
            string path = Path.Combine(GetAddinDirectory(revitVersion), AddinFileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetAddinDirectory(string revitVersion)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk",
                "Revit",
                "Addins",
                revitVersion);
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            Directory.Delete(path, true);
        }

        private static string EscapeXml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
