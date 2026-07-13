using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using SABPlus.RevitBridge.Commands;
using SABPlus.RevitBridge.Services;
using System;
using System.Linq;

namespace SABPlus.RevitBridge
{
    public sealed class App : IExternalApplication
    {
        private const string RibbonTabName = "SAB+";
        private const string RibbonPanelName = "Быстрый доступ";

        private RevitBridgeHost _bridgeHost;

        public Result OnStartup(UIControlledApplication application)
        {
            if (application == null)
            {
                return Result.Failed;
            }

            try
            {
                CreateRibbon(application);

                _bridgeHost = new RevitBridgeHost();
                _bridgeHost.Start();
                application.Idling += Application_Idling;

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                TaskDialog.Show(
                    "SAB+ Radial Bridge",
                    "Не удалось запустить радиальное колесо.\n\n" + exception);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (application != null)
                {
                    application.Idling -= Application_Idling;
                }

                if (_bridgeHost != null)
                {
                    _bridgeHost.Dispose();
                    _bridgeHost = null;
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                TaskDialog.Show(
                    "SAB+ Radial Bridge",
                    "Ошибка завершения радиального колеса.\n\n" + exception);
                return Result.Failed;
            }
        }

        private static void CreateRibbon(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(RibbonTabName);
            }
            catch
            {
                // Ribbon tab already exists.
            }

            RibbonPanel panel = application
                .GetRibbonPanels(RibbonTabName)
                .FirstOrDefault(item => string.Equals(item.Name, RibbonPanelName, StringComparison.Ordinal));

            if (panel == null)
            {
                panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
            }

            string assemblyPath = typeof(App).Assembly.Location;
            PushButtonData buttonData = new PushButtonData(
                "SABPlus_RadialWheelSettings",
                "Настройка\nколеса",
                assemblyPath,
                typeof(OpenWheelSettingsCommand).FullName)
            {
                ToolTip = "Открыть редактор профилей и команд радиального колеса.",
                LongDescription = "Настройка стадий, позиций команд, подписей и триггера радиального колеса SAB+."
            };

            PushButton button = panel.AddItem(buttonData) as PushButton;
            if (button != null)
            {
                button.Image = RibbonIconFactory.CreateWheelIcon(16);
                button.LargeImage = RibbonIconFactory.CreateWheelIcon(32);
            }
        }

        private void Application_Idling(object sender, IdlingEventArgs e)
        {
            UIApplication uiApplication = sender as UIApplication;
            if (uiApplication == null || _bridgeHost == null)
            {
                return;
            }

            try
            {
                _bridgeHost.UpdateContext(uiApplication);
            }
            catch
            {
                // Idling must never be interrupted by context refresh failures.
            }
        }
    }
}
