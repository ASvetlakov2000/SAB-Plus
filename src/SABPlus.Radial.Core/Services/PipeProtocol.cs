using SABPlus.Radial.Core.Models;
using System;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace SABPlus.Radial.Core.Services
{
    public static class PipeNameFactory
    {
        public static string GetBridgePipeName(int revitProcessId)
        {
            return "SABPlus.RadialBridge." + GetCurrentUserToken() + "." + revitProcessId;
        }

        public static string GetOverlayInstancePipeName()
        {
            return "SABPlus.RadialOverlay." + GetCurrentUserToken();
        }

        public static string GetOverlayMutexName()
        {
            return "Local\\SABPlus.RadialOverlay." + GetCurrentUserToken();
        }

        private static string GetCurrentUserToken()
        {
            string identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
                StringBuilder builder = new StringBuilder(16);
                for (int index = 0; index < 8; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }

    public static class BridgeResponseFactory
    {
        public static BridgeResponse Success(BridgeRequest request, string message, RevitContextSnapshot context)
        {
            return new BridgeResponse
            {
                RequestId = request?.RequestId ?? string.Empty,
                Accepted = true,
                Message = message ?? string.Empty,
                Context = context
            };
        }

        public static BridgeResponse Failure(BridgeRequest request, string message, RevitContextSnapshot context)
        {
            return new BridgeResponse
            {
                RequestId = request?.RequestId ?? string.Empty,
                Accepted = false,
                Message = message ?? string.Empty,
                Context = context
            };
        }
    }
}
