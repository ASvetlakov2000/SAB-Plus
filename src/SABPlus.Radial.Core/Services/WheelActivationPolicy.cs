using SABPlus.Radial.Core.Models;

namespace SABPlus.Radial.Core.Services
{
    public static class WheelActivationPolicy
    {
        public static bool CanOpenForContext(RevitContextSnapshot context)
        {
            return context != null &&
                   context.HasActiveDocument &&
                   context.HasActiveView &&
                   context.IsProjectModelDocument &&
                   context.DocumentKind == RevitDocumentKind.ProjectModel;
        }
    }
}
