using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using SABPlus.Radial.Overlay.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SABPlus.Radial.Overlay.Services
{
    public sealed class RevitBridgeDiscoveryService : IDisposable
    {
        private readonly BridgeClientService _bridgeClient;
        private readonly object _sync = new object();
        private readonly Dictionary<int, RevitContextSnapshot> _contexts;

        private Timer _timer;
        private int _refreshing;

        public event EventHandler ContextsChanged;

        public RevitBridgeDiscoveryService(BridgeClientService bridgeClient)
        {
            _bridgeClient = bridgeClient ?? throw new ArgumentNullException(nameof(bridgeClient));
            _contexts = new Dictionary<int, RevitContextSnapshot>();
        }

        public void Start()
        {
            if (_timer != null)
            {
                return;
            }

            _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        public IReadOnlyList<RevitContextSnapshot> GetContexts()
        {
            lock (_sync)
            {
                return _contexts.Values
                    .Select(item => JsonSerialization.DeepClone(item))
                    .OrderBy(item => item.ProcessId)
                    .ToList();
            }
        }

        public RevitContextSnapshot GetContext(int processId)
        {
            lock (_sync)
            {
                RevitContextSnapshot context;
                return _contexts.TryGetValue(processId, out context)
                    ? JsonSerialization.DeepClone(context)
                    : null;
            }
        }

        public RevitContextSnapshot GetForegroundRevitContext()
        {
            IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return null;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(foregroundWindow, out processId);
            if (processId == 0)
            {
                return null;
            }

            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    if (!string.Equals(process.ProcessName, "Revit", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }

            // The global hook must validate the document at the exact press moment.
            // Do not rely on the two-second discovery cache when the user changed documents.
            RevitContextSnapshot liveContext = _bridgeClient.TryGetContext((int)processId, 180);
            if (liveContext == null)
            {
                return null;
            }

            lock (_sync)
            {
                _contexts[(int)processId] = JsonSerialization.DeepClone(liveContext);
            }

            return liveContext;
        }

        public void Dispose()
        {
            Timer timer = Interlocked.Exchange(ref _timer, null);
            timer?.Dispose();
        }

        private void Refresh(object state)
        {
            if (Interlocked.Exchange(ref _refreshing, 1) != 0)
            {
                return;
            }

            try
            {
                Process[] processes = Process.GetProcessesByName("Revit");
                Task<RevitContextSnapshot>[] tasks = processes
                    .Select(process => Task.Run(() =>
                    {
                        int processId = process.Id;
                        process.Dispose();
                        return _bridgeClient.TryGetContext(processId, 180);
                    }))
                    .ToArray();

                Task.WaitAll(tasks, TimeSpan.FromSeconds(1));
                Dictionary<int, RevitContextSnapshot> updated = new Dictionary<int, RevitContextSnapshot>();

                foreach (Task<RevitContextSnapshot> task in tasks)
                {
                    if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
                    {
                        updated[task.Result.ProcessId] = task.Result;
                    }
                }

                lock (_sync)
                {
                    _contexts.Clear();
                    foreach (KeyValuePair<int, RevitContextSnapshot> item in updated)
                    {
                        _contexts[item.Key] = item.Value;
                    }
                }

                ContextsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // A missing or closing Revit process is expected during discovery.
            }
            finally
            {
                Interlocked.Exchange(ref _refreshing, 0);
            }
        }
    }
}
