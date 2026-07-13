using SABPlus.Radial.Core.Models;
using System;
using System.IO;

namespace SABPlus.Radial.Core.Services
{
    public static class RevitDocumentKindClassifier
    {
        public static RevitDocumentKind Classify(
            bool isFamilyDocument,
            string pathName)
        {
            string extension = string.Empty;
            try
            {
                extension = Path.GetExtension(pathName ?? string.Empty);
            }
            catch
            {
                extension = string.Empty;
            }

            if (string.Equals(extension, ".rft", StringComparison.OrdinalIgnoreCase))
            {
                return RevitDocumentKind.FamilyTemplate;
            }

            if (isFamilyDocument ||
                string.Equals(extension, ".rfa", StringComparison.OrdinalIgnoreCase))
            {
                return RevitDocumentKind.Family;
            }

            if (string.Equals(extension, ".rte", StringComparison.OrdinalIgnoreCase))
            {
                return RevitDocumentKind.ProjectTemplate;
            }

            // RVT files, cloud/server models, and unsaved new projects are project models.
            return RevitDocumentKind.ProjectModel;
        }
    }
}
