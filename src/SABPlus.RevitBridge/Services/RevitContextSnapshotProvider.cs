using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SABPlus.RevitBridge.Services
{
    public sealed class RevitContextSnapshotProvider
    {
        private readonly object _sync = new object();
        private RevitContextSnapshot _snapshot;

        public RevitContextSnapshotProvider()
        {
            _snapshot = new RevitContextSnapshot
            {
                ProcessId = Process.GetCurrentProcess().Id,
                PostableCommandNames = new System.Collections.Generic.List<string>(
                    Enum.GetNames(typeof(PostableCommand)))
            };
        }

        public void Update(UIApplication uiApplication)
        {
            if (uiApplication == null)
            {
                return;
            }

            RevitContextSnapshot updated;
            lock (_sync)
            {
                updated = JsonSerialization.DeepClone(_snapshot);
            }

            updated.ProcessId = Process.GetCurrentProcess().Id;
            updated.RevitVersion = uiApplication.Application?.VersionNumber ?? string.Empty;
            updated.CapturedUtc = DateTime.UtcNow;

            UIDocument uiDocument = uiApplication.ActiveUIDocument;
            Document document = uiDocument?.Document;
            if (document == null)
            {
                updated.HasActiveDocument = false;
                updated.HasActiveView = false;
                updated.DocumentKind = RevitDocumentKind.None;
                updated.IsProjectModelDocument = false;
                updated.ProjectKey = string.Empty;
                updated.ProjectTitle = string.Empty;
                updated.IsTransientProjectKey = false;
            }
            else
            {
                updated.HasActiveDocument = true;
                updated.HasActiveView = uiDocument.ActiveView != null;
                updated.DocumentKind = GetDocumentKind(document);
                updated.IsProjectModelDocument =
                    updated.DocumentKind == RevitDocumentKind.ProjectModel;
                updated.ProjectTitle = document.Title ?? string.Empty;

                string identity = GetDocumentIdentity(document);
                updated.IsTransientProjectKey = string.IsNullOrWhiteSpace(identity);
                if (updated.IsTransientProjectKey)
                {
                    identity = "unsaved|" + updated.ProcessId + "|" + updated.ProjectTitle;
                }

                updated.ProjectKey = HashProjectIdentity(identity);
            }

            lock (_sync)
            {
                _snapshot = updated;
            }
        }

        public RevitContextSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return JsonSerialization.DeepClone(_snapshot);
            }
        }

        public void SetCommandQueueBusy(bool isBusy)
        {
            lock (_sync)
            {
                _snapshot.IsCommandQueueBusy = isBusy;
                _snapshot.CapturedUtc = DateTime.UtcNow;
            }
        }

        public void SetLastCommandMessage(string message)
        {
            lock (_sync)
            {
                _snapshot.LastCommandMessage = message ?? string.Empty;
                _snapshot.CapturedUtc = DateTime.UtcNow;
            }
        }

        private static string GetDocumentIdentity(Document document)
        {
            try
            {
                if (document.IsWorkshared)
                {
                    ModelPath centralPath = document.GetWorksharingCentralModelPath();
                    if (centralPath != null)
                    {
                        string visibleCentralPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);
                        if (!string.IsNullOrWhiteSpace(visibleCentralPath))
                        {
                            return NormalizeIdentity(visibleCentralPath);
                        }
                    }
                }
            }
            catch
            {
                // Fall back to the document path below.
            }

            if (!string.IsNullOrWhiteSpace(document.PathName))
            {
                return NormalizeIdentity(document.PathName);
            }

            return string.Empty;
        }

        private static RevitDocumentKind GetDocumentKind(Document document)
        {
            if (document == null)
            {
                return RevitDocumentKind.None;
            }

            try
            {
                return RevitDocumentKindClassifier.Classify(
                    document.IsFamilyDocument,
                    document.PathName);
            }
            catch
            {
                return RevitDocumentKind.Other;
            }
        }

        private static string NormalizeIdentity(string value)
        {
            string normalized = value.Trim();
            try
            {
                if (Path.IsPathRooted(normalized))
                {
                    normalized = Path.GetFullPath(normalized);
                }
            }
            catch
            {
                // Cloud and server model paths are not regular file-system paths.
            }

            return normalized.Replace('/', '\\').ToUpperInvariant();
        }

        private static string HashProjectIdentity(string identity)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
