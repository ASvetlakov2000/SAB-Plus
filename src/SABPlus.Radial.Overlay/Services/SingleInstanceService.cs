using SABPlus.Radial.Core.Services;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class SingleInstanceCommandServer : IDisposable
    {
        private readonly Action<string[]> _handler;
        private readonly object _sync = new object();

        private CancellationTokenSource _cancellation;
        private Task _serverTask;
        private NamedPipeServerStream _activeServer;

        public SingleInstanceCommandServer(Action<string[]> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
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
                _serverTask = Task.Run(() => RunLoop(_cancellation.Token));
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
                serverTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Application shutdown must continue.
            }

            _cancellation.Dispose();
            _cancellation = null;
        }

        public static bool TrySend(string[] arguments)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(
                    ".",
                    PipeNameFactory.GetOverlayInstancePipeName(),
                    PipeDirection.Out))
                {
                    client.Connect(500);
                    using (StreamWriter writer = new StreamWriter(client, new UTF8Encoding(false)))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine(JsonSerialization.Serialize(arguments ?? new string[0], false));
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task RunLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(
                        PipeNameFactory.GetOverlayInstancePipeName(),
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        lock (_sync)
                        {
                            _activeServer = server;
                        }

                        await server.WaitForConnectionAsync().ConfigureAwait(false);
                        using (StreamReader reader = new StreamReader(server, new UTF8Encoding(false), false, 4096, true))
                        {
                            string line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                string[] arguments = JsonSerialization.Deserialize<string[]>(line);
                                _handler(arguments);
                            }
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
                        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
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
    }
}
