using Autodesk.Revit.UI;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;
using System.Diagnostics;

namespace SABPlus.RevitBridge.Services
{
    public sealed class RevitBridgeHost : IDisposable
    {
        private readonly RevitContextSnapshotProvider _contextProvider;
        private readonly RevitCommandExternalEventHandler _commandHandler;
        private readonly ExternalEvent _externalEvent;
        private readonly BridgePipeServer _pipeServer;

        private bool _started;

        public RevitBridgeHost()
        {
            _contextProvider = new RevitContextSnapshotProvider();
            _commandHandler = new RevitCommandExternalEventHandler(_contextProvider);
            _externalEvent = ExternalEvent.Create(_commandHandler);

            string pipeName = PipeNameFactory.GetBridgePipeName(Process.GetCurrentProcess().Id);
            _pipeServer = new BridgePipeServer(pipeName, ProcessRequest);
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _pipeServer.Start();
            _started = true;
        }

        public void UpdateContext(UIApplication uiApplication)
        {
            _contextProvider.Update(uiApplication);

            string requestId;
            string failureMessage;
            if (_commandHandler.TryPrepareDeferredCommand(
                    uiApplication,
                    out requestId,
                    out failureMessage))
            {
                ExternalEventRequest raiseResult = _externalEvent.Raise();
                if (raiseResult != ExternalEventRequest.Accepted)
                {
                    _commandHandler.RestorePreparedCommand(requestId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                _contextProvider.SetLastCommandMessage(failureMessage);
                TaskDialog.Show("SAB+ Радиальное колесо", failureMessage);
            }
        }

        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            _pipeServer.Dispose();
            _externalEvent.Dispose();
            _started = false;
        }

        private BridgeResponse ProcessRequest(BridgeRequest request)
        {
            RevitContextSnapshot context = _contextProvider.GetSnapshot();
            if (request == null)
            {
                return BridgeResponseFactory.Failure(null, "Пустой запрос.", context);
            }

            if (string.Equals(request.RequestType, BridgeRequestTypes.GetContext, StringComparison.Ordinal))
            {
                return BridgeResponseFactory.Success(request, "RevitBridge подключён.", context);
            }

            if (!string.Equals(request.RequestType, BridgeRequestTypes.ExecuteCommand, StringComparison.Ordinal) &&
                !string.Equals(request.RequestType, BridgeRequestTypes.VerifyCommand, StringComparison.Ordinal) &&
                !string.Equals(request.RequestType, BridgeRequestTypes.DebugWheelState, StringComparison.Ordinal))
            {
                return BridgeResponseFactory.Failure(request, "Неизвестный тип запроса.", context);
            }

            string enqueueMessage;
            if (!_commandHandler.TryEnqueue(request, out enqueueMessage))
            {
                return BridgeResponseFactory.Failure(request, enqueueMessage, _contextProvider.GetSnapshot());
            }

            ExternalEventRequest raiseResult = _externalEvent.Raise();
            if (raiseResult != ExternalEventRequest.Accepted)
            {
                _commandHandler.Cancel(request.RequestId);
                return BridgeResponseFactory.Failure(
                    request,
                    "Revit не принял ExternalEvent: " + raiseResult,
                    _contextProvider.GetSnapshot());
            }

            return BridgeResponseFactory.Success(request, enqueueMessage, _contextProvider.GetSnapshot());
        }
    }
}
