using System;
using System.Collections.Generic;

namespace SABPlus.Radial.Core.Models
{
    public enum RevitDocumentKind
    {
        None = 0,
        ProjectModel = 1,
        Family = 2,
        ProjectTemplate = 3,
        FamilyTemplate = 4,
        Other = 5
    }

    public static class BridgeRequestTypes
    {
        public const string GetContext = "GetContext";
        public const string ExecuteCommand = "ExecuteCommand";
        public const string VerifyCommand = "VerifyCommand";
        public const string DebugWheelState = "DebugWheelState";
    }

    public sealed class BridgeRequest
    {
        public string RequestId { get; set; }

        public string RequestType { get; set; }

        public CommandDescriptor Command { get; set; }

        public string DebugMessage { get; set; }

        public BridgeRequest()
        {
            RequestId = Guid.NewGuid().ToString("N");
            RequestType = string.Empty;
            DebugMessage = string.Empty;
        }
    }

    public sealed class BridgeResponse
    {
        public string RequestId { get; set; }

        public bool Accepted { get; set; }

        public string Message { get; set; }

        public RevitContextSnapshot Context { get; set; }

        public BridgeResponse()
        {
            RequestId = string.Empty;
            Message = string.Empty;
        }
    }

    public sealed class RevitContextSnapshot
    {
        public int ProcessId { get; set; }

        public string RevitVersion { get; set; }

        public bool HasActiveDocument { get; set; }

        public bool HasActiveView { get; set; }

        public RevitDocumentKind DocumentKind { get; set; }

        public bool IsProjectModelDocument { get; set; }

        public string ProjectKey { get; set; }

        public string ProjectTitle { get; set; }

        public bool IsTransientProjectKey { get; set; }

        public bool IsCommandQueueBusy { get; set; }

        public string LastCommandMessage { get; set; }

        public DateTime CapturedUtc { get; set; }

        public List<string> PostableCommandNames { get; set; }

        public Dictionary<string, string> PostableCommandDisplayNames { get; set; }

        public RevitContextSnapshot()
        {
            RevitVersion = string.Empty;
            ProjectKey = string.Empty;
            ProjectTitle = string.Empty;
            LastCommandMessage = string.Empty;
            CapturedUtc = DateTime.UtcNow;
            PostableCommandNames = new List<string>();
            PostableCommandDisplayNames = new Dictionary<string, string>();
        }
    }
}
