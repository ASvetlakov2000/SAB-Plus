using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class BridgeClientService
    {
        public BridgeResponse Send(int revitProcessId, BridgeRequest request, int timeoutMilliseconds)
        {
            if (revitProcessId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revitProcessId));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string pipeName = PipeNameFactory.GetBridgePipeName(revitProcessId);
            using (NamedPipeClientStream client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.None))
            {
                client.Connect(timeoutMilliseconds);

                using (StreamReader reader = new StreamReader(client, new UTF8Encoding(false), false, 4096, true))
                using (StreamWriter writer = new StreamWriter(client, new UTF8Encoding(false), 4096, true))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(JsonSerialization.Serialize(request, false));
                    string responseLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(responseLine))
                    {
                        throw new IOException("RevitBridge вернул пустой ответ.");
                    }

                    return JsonSerialization.Deserialize<BridgeResponse>(responseLine);
                }
            }
        }

        public Task<BridgeResponse> SendAsync(int revitProcessId, BridgeRequest request, int timeoutMilliseconds)
        {
            return Task.Run(() => Send(revitProcessId, request, timeoutMilliseconds));
        }

        public RevitContextSnapshot TryGetContext(int revitProcessId, int timeoutMilliseconds)
        {
            try
            {
                BridgeResponse response = Send(
                    revitProcessId,
                    new BridgeRequest { RequestType = BridgeRequestTypes.GetContext },
                    timeoutMilliseconds);

                return response.Accepted ? response.Context : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
