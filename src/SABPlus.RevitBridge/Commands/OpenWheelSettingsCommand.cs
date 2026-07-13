using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SABPlus.RevitBridge.Services;
using System;

namespace SABPlus.RevitBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class OpenWheelSettingsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                if (commandData == null || commandData.Application == null)
                {
                    message = "Не удалось получить UIApplication.";
                    TaskDialog.Show("SAB+ Радиальное колесо", message);
                    return Result.Failed;
                }

                // The editor is independent from the active model and must also open from Revit's home screen.
                OverlayLaunchResult launchResult = OverlayLauncher.OpenSettings();
#if DEBUG
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;
                string documentState = uiDocument?.Document == null
                    ? "документ не открыт (это допустимо для редактора)"
                    : "документ: " + uiDocument.Document.Title;
                TaskDialog.Show(
                    "SAB+ — отладка запуска",
                    "Шаг 1. UIApplication получен.\n" +
                    "Шаг 2. Состояние Revit: " + documentState + ".\n" +
                    "Шаг 3. Overlay найден: " + launchResult.OverlayPath + ".\n" +
                    "Шаг 4. Процесс запуска создан, PID " + launchResult.StartedProcessId + ".\n" +
                    "Шаг 5. Передана команда открытия окна настроек.");
#endif
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show(
                    "SAB+ Радиальное колесо",
                    "Не удалось открыть редактор колеса.\n\n" + exception);
                return Result.Failed;
            }
        }
    }
}
