using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SABPlus.RevitBridge.Services
{
    public sealed class BridgePipeServer : IDisposable
    {
        private const int MaximumRequestLength = 1024 * 1024;

        private readonly string _pipeName;
        private readonly Func<BridgeRequest, BridgeResponse> _requestHandler;
        private readonly object _sync = new object();

        private CancellationTokenSource _cancellation;
        private Task _serverTask;
        private NamedPipeServerStream _activeServer;

        public BridgePipeServer(string pipeName, Func<BridgeRequest, BridgeResponse> requestHandler)
        {
            _pipeName = string.IsNullOrWhiteSpace(pipeName)
                ? throw new ArgumentException("Имя канала не задано.", nameof(pipeName))
                : pipeName;
            _requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_serverTask != null)
                {
                    return;
                }

                _cancellation = new CancellationTokenSource();
                _serverTask = Task.Run(() => RunServerLoop(_cancellation.Token));
            }
        }

        public void Dispose()
        {
            Task serverTask;
            lock (_sync)
            {
                if (_serverTask == null)
                {
                    return;
                }

                _cancellation.Cancel();
                _activeServer?.Dispose();
                serverTask = _serverTask;
                _serverTask = null;
            }

            try
            {
                serverTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Revit shutdown must continue even when a pipe client disconnected unexpectedly.
            }

            _cancellation.Dispose();
            _cancellation = null;
        }

        private async Task RunServerLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        lock (_sync)
                        {
                            _activeServer = server;
                        }

                        await server.WaitForConnectionAsync().ConfigureAwait(false);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await ProcessConnection(server).ConfigureAwait(false);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    lock (_sync)
                    {
                        _activeServer = null;
                    }
                }
            }
        }

        private async Task ProcessConnection(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, true))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
            {
                writer.AutoFlush = true;
                string line = await reader.ReadLineAsync().ConfigureAwait(false);

                BridgeResponse response;
                if (string.IsNullOrWhiteSpace(line) || line.Length > MaximumRequestLength)
                {
                    response = new BridgeResponse
                    {
                        Accepted = false,
                        Message = "Некорректный запрос локального канала."
                    };
                }
                else
                {
                    try
                    {
                        BridgeRequest request = JsonSerialization.Deserialize<BridgeRequest>(line);
                        response = _requestHandler(request);
                    }
                    catch (Exception exception)
                    {
                        response = new BridgeResponse
                        {
                            Accepted = false,
                            Message = "Ошибка обработки запроса: " + exception.Message
                        };
                    }
                }

                await writer.WriteLineAsync(JsonSerialization.Serialize(response, false)).ConfigureAwait(false);
            }
        }
    }
}
