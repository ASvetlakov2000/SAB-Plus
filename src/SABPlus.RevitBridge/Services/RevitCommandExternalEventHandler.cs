using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;

namespace SABPlus.RevitBridge.Services
{
    public sealed class RevitCommandExternalEventHandler : IExternalEventHandler
    {
        private readonly object _sync = new object();
        private readonly RevitContextSnapshotProvider _contextProvider;
        private BridgeRequest _pendingRequest;
        private BridgeRequest _deferredRequest;
        private DateTime _deferredDeadlineUtc;
        private bool _isExecuting;

        public RevitCommandExternalEventHandler(RevitContextSnapshotProvider contextProvider)
        {
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        }

        public bool TryEnqueue(BridgeRequest request, out string message)
        {
            bool isDebugRequest = request != null &&
                                  string.Equals(
                                      request.RequestType,
                                      BridgeRequestTypes.DebugWheelState,
                                      StringComparison.Ordinal);
            if (request == null || (!isDebugRequest && request.Command == null))
            {
                message = "Команда не передана.";
                return false;
            }

            lock (_sync)
            {
                if (_pendingRequest != null || _deferredRequest != null || _isExecuting)
                {
                    message = "Предыдущая команда ещё ожидает выполнения в Revit.";
                    return false;
                }

                _pendingRequest = JsonSerialization.DeepClone(request);
                _contextProvider.SetCommandQueueBusy(true);
                message = "Команда поставлена в очередь Revit.";
                return true;
            }
        }

        public void Cancel(string requestId)
        {
            lock (_sync)
            {
                if (_pendingRequest != null &&
                    string.Equals(_pendingRequest.RequestId, requestId, StringComparison.Ordinal))
                {
                    _pendingRequest = null;
                    _contextProvider.SetCommandQueueBusy(false);
                }

                if (_deferredRequest != null &&
                    string.Equals(_deferredRequest.RequestId, requestId, StringComparison.Ordinal))
                {
                    _deferredRequest = null;
                    _contextProvider.SetCommandQueueBusy(false);
                }
            }
        }

        public bool TryPrepareDeferredCommand(
            UIApplication app,
            out string requestId,
            out string failureMessage)
        {
            requestId = string.Empty;
            failureMessage = string.Empty;
            BridgeRequest deferred;
            lock (_sync)
            {
                deferred = _deferredRequest;
            }

            if (deferred == null)
            {
                return false;
            }

            if (DateTime.UtcNow > _deferredDeadlineUtc)
            {
                lock (_sync)
                {
                    if (_deferredRequest != null &&
                        string.Equals(_deferredRequest.RequestId, deferred.RequestId, StringComparison.Ordinal))
                    {
                        _deferredRequest = null;
                    }
                }

                _contextProvider.SetCommandQueueBusy(false);
                failureMessage = "Revit не завершил предыдущую команду за отведённое время.";
                return false;
            }

            string validationMessage;
            RevitCommandId commandId = ResolveCommandId(deferred.Command, out validationMessage);
            if (commandId == null)
            {
                lock (_sync)
                {
                    _deferredRequest = null;
                }

                _contextProvider.SetCommandQueueBusy(false);
                failureMessage = validationMessage;
                return false;
            }

            if (app == null || !app.CanPostCommand(commandId))
            {
                return false;
            }

            lock (_sync)
            {
                if (_deferredRequest == null ||
                    !string.Equals(_deferredRequest.RequestId, deferred.RequestId, StringComparison.Ordinal))
                {
                    return false;
                }

                _pendingRequest = _deferredRequest;
                _deferredRequest = null;
                requestId = _pendingRequest.RequestId;
                return true;
            }
        }

        public void RestorePreparedCommand(string requestId)
        {
            lock (_sync)
            {
                if (_pendingRequest == null ||
                    !string.Equals(_pendingRequest.RequestId, requestId, StringComparison.Ordinal))
                {
                    return;
                }

                _deferredRequest = _pendingRequest;
                _pendingRequest = null;
            }
        }

        public void Execute(UIApplication app)
        {
            BridgeRequest request;
            bool keepQueueBusy = false;
            lock (_sync)
            {
                request = _pendingRequest;
                _pendingRequest = null;
                _isExecuting = request != null;
            }

            if (request == null)
            {
                _contextProvider.SetCommandQueueBusy(false);
                return;
            }

            try
            {
                if (string.Equals(
                        request.RequestType,
                        BridgeRequestTypes.DebugWheelState,
                        StringComparison.Ordinal))
                {
                    string debugMessage = string.IsNullOrWhiteSpace(request.DebugMessage)
                        ? "Отладочные данные радиального колеса отсутствуют."
                        : request.DebugMessage;
                    _contextProvider.SetLastCommandMessage(debugMessage);
                    TaskDialog.Show("SAB+ — Отладка радиального колеса", debugMessage);
                    return;
                }

                string validationMessage;
                RevitCommandId commandId = ResolveCommandId(request.Command, out validationMessage);
                if (commandId == null)
                {
                    ShowUnavailable(validationMessage);
                    return;
                }

                if (app == null || app.ActiveUIDocument == null || app.ActiveUIDocument.Document == null)
                {
                    ShowUnavailable("В Revit нет активного документа.");
                    return;
                }

                if (app.ActiveUIDocument.ActiveView == null)
                {
                    ShowUnavailable("В Revit нет активного вида.");
                    return;
                }

                if (string.Equals(request.RequestType, BridgeRequestTypes.VerifyCommand, StringComparison.Ordinal))
                {
                    if (!app.CanPostCommand(commandId))
                    {
                        ShowUnavailable("Команда недоступна в текущем контексте Revit: " + request.Command.DisplayName);
                        return;
                    }

                    _contextProvider.SetLastCommandMessage("Команда доступна: " + request.Command.DisplayName);
                    return;
                }

                // Revit permits one PostCommand per API context. When an interactive command
                // is active, cancel it now and post the wheel command from the next ExternalEvent.
                RevitCommandId cancelCommandId = RevitCommandId.LookupCommandId("ID_CANCEL");
                if (cancelCommandId != null &&
                    commandId.Id != cancelCommandId.Id &&
                    app.CanPostCommand(cancelCommandId))
                {
                    app.PostCommand(cancelCommandId);
                    lock (_sync)
                    {
                        _deferredRequest = request;
                        _deferredDeadlineUtc = DateTime.UtcNow.AddSeconds(8.0);
                    }

                    keepQueueBusy = true;
                    _contextProvider.SetLastCommandMessage(
                        "Предыдущая команда Revit отменяется. Ожидает запуска: " + request.Command.DisplayName);
                    return;
                }

                if (!app.CanPostCommand(commandId))
                {
                    ShowUnavailable("Команда недоступна в текущем контексте Revit: " + request.Command.DisplayName);
                    return;
                }

                app.PostCommand(commandId);
                _contextProvider.SetLastCommandMessage("Запущена команда: " + request.Command.DisplayName);
            }
            catch (Exception exception)
            {
                _contextProvider.SetLastCommandMessage("Ошибка команды: " + exception.Message);
                TaskDialog.Show(
                    "SAB+ Радиальное колесо",
                    "Ошибка выполнения команды.\n\n" + exception);
            }
            finally
            {
                lock (_sync)
                {
                    _isExecuting = false;
                }

                if (!keepQueueBusy)
                {
                    _contextProvider.SetCommandQueueBusy(false);
                }
            }
        }

        public string GetName()
        {
            return "SABPlus.RadialWheel.CommandExternalEvent";
        }

        private static RevitCommandId ResolveCommandId(
            CommandDescriptor command,
            out string validationMessage)
        {
            validationMessage = string.Empty;

            if (command.Source == WheelCommandSource.RevitPostable)
            {
                PostableCommand postableCommand;
                if (!Enum.TryParse(command.RevitPostableCommandName, true, out postableCommand))
                {
                    validationMessage = "Неизвестная системная команда Revit: " + command.RevitPostableCommandName;
                    return null;
                }

                return RevitCommandId.LookupPostableCommandId(postableCommand);
            }

            if (command.Source == WheelCommandSource.RevitCommandId ||
                command.Source == WheelCommandSource.SabCommand)
            {
                if (string.IsNullOrWhiteSpace(command.RevitCommandId))
                {
                    validationMessage = "У команды отсутствует Revit Command ID.";
                    return null;
                }

                RevitCommandId commandId = RevitCommandId.LookupCommandId(command.RevitCommandId);
                if (commandId == null)
                {
                    validationMessage = "Revit Command ID не зарегистрирован: " + command.RevitCommandId;
                }

                return commandId;
            }

            validationMessage = "Локальная утилита должна запускаться процессом RadialOverlay.";
            return null;
        }

        private void ShowUnavailable(string message)
        {
            _contextProvider.SetLastCommandMessage(message);
            TaskDialog.Show("SAB+ Радиальное колесо", message);
        }
    }
}
