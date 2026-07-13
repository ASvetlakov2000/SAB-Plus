using SABPlus.Radial.Core.Geometry;
using SABPlus.Radial.Core.Models;
using SABPlus.Radial.Core.Services;
using System;
using System.IO;
using System.Linq;

namespace SABPlus.Radial.Core.SmokeTests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                TestDefaultSettings();
                TestDirectionalGeometry();
                TestCentralZones();
                TestCapsuleLayouts();
                TestEqualVisualClearanceAndFixedSizes();
                TestAssignedCommandLayout();
                TestReturnToStageZone();
                TestDynamicCapsuleWidths();
                TestAppearanceSettings();
                TestPostableCommandLocalizationAndMigration();
                TestRevitDocumentKinds();
                TestWheelActivationPolicy();
                TestJsonAndAtomicBackup();

                Console.WriteLine("SAB+ Radial Core smoke tests: PASSED");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("SAB+ Radial Core smoke tests: FAILED");
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void TestDefaultSettings()
        {
            WheelSettings settings = WheelSettingsFactory.CreateDefault();
            WheelSettingsValidationResult validation = WheelSettingsValidator.Validate(settings);
            Assert(validation.IsValid, "Стандартные настройки должны проходить валидацию.");
            Assert(settings.Profiles.Count == 3, "Должны существовать три стандартные стадии.");
            Assert(settings.StageTrigger.Type == WheelTriggerType.MouseXButton1,
                "Стадии по умолчанию должны открываться XButton1.");
            Assert(settings.CommandTrigger.Type == WheelTriggerType.MouseXButton2,
                "Команды по умолчанию должны открываться XButton2.");
            Assert(settings.Profiles.All(profile => profile.SectorCount == 8), "Стандартные профили должны содержать 8 секторов.");
            Assert(settings.SchemaVersion == 6, "Цвета текста и наведения должны использовать шестую версию схемы.");
        }

        private static void TestDirectionalGeometry()
        {
            WheelGeometrySettings geometry = new WheelGeometrySettings();

            AssertSector(0.0, -220.0, 8, 0, geometry, "верх");
            AssertSector(220.0, 0.0, 8, 2, geometry, "право");
            AssertSector(0.0, 220.0, 8, 4, geometry, "низ");
            AssertSector(-220.0, 0.0, 8, 6, geometry, "лево");

            AssertSector(0.0, -220.0, 3, 0, geometry, "верх для стадий");
            AssertSector(220.0, 0.0, 3, 1, geometry, "право для стадий");
            AssertSector(-180.0, 180.0, 3, 2, geometry, "низ-влево для стадий");
        }

        private static void TestCentralZones()
        {
            WheelGeometrySettings geometry = new WheelGeometrySettings();

            WheelHitResult cancel = WheelGeometryCalculator.HitTest(
                0.0,
                0.0,
                8,
                WheelDisplayLevel.Commands,
                geometry);
            Assert(cancel.Kind == WheelHitKind.Cancel, "Точная центральная зона должна отменять действие.");

            WheelHitResult commandCenter = WheelGeometryCalculator.HitTest(
                30.0,
                0.0,
                8,
                WheelDisplayLevel.Commands,
                geometry);
            Assert(commandCenter.Kind == WheelHitKind.Cancel,
                "При раздельных триггерах центральное кольцо команд должно отменять выбор.");

            WheelHitResult stageCenter = WheelGeometryCalculator.HitTest(
                30.0,
                0.0,
                3,
                WheelDisplayLevel.Stages,
                geometry);
            Assert(stageCenter.Kind == WheelHitKind.None, "Внутри порога стадий не должно выбираться направление.");

            WheelHitResult gap = WheelGeometryCalculator.HitTest(
                80.0,
                0.0,
                8,
                WheelDisplayLevel.Commands,
                geometry);
            Assert(gap.Kind == WheelHitKind.None, "Между центральным возвратом и командами должна быть нейтральная зона.");

            WheelHitResult selectionZone = WheelGeometryCalculator.HitTest(
                105.0,
                0.0,
                8,
                WheelDisplayLevel.Commands,
                geometry);
            Assert(selectionZone.Kind == WheelHitKind.Sector,
                "Секторная зона должна выбирать команду до достижения центра капсулы.");
        }

        private static void TestCapsuleLayouts()
        {
            WheelGeometrySettings geometry = new WheelGeometrySettings();
            var layouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                8,
                WheelCapsuleLevel.Command,
                geometry,
                0.0);

            Assert(layouts.Count == 8, "Для восьми позиций должны создаваться восемь капсул.");
            Assert(Math.Abs(layouts[0].Center.X) < 0.001, "Первая капсула должна находиться сверху.");
            Assert(layouts[0].Center.Y < 0.0, "Первая капсула должна находиться выше центра.");
            Assert(layouts[0].Width <= geometry.CommandCapsuleWidth, "Ширина капсул должна адаптироваться к количеству позиций.");

            double expandedRadius = WheelGeometryCalculator.GetExpandedCommandRadius(8, 8, geometry);
            double regularRadius = WheelGeometryCalculator.GetCapsuleRadius(
                8,
                WheelCapsuleLevel.Command,
                geometry);
            Assert(expandedRadius > regularRadius, "При двух кольцах команды должны отодвигаться от стадий.");

            WheelGeometrySettings spaciousGeometry = new WheelGeometrySettings
            {
                CapsuleGap = 20.0
            };
            double spaciousRadius = WheelGeometryCalculator.GetCapsuleRadius(
                8,
                WheelCapsuleLevel.Command,
                spaciousGeometry);
            Assert(spaciousRadius > regularRadius,
                "Увеличение интервала должно автоматически раздвигать капсулы.");
        }

        private static void TestAssignedCommandLayout()
        {
            WheelProfile profile = WheelSettingsFactory.CreateProfile(
                "Тест",
                "Т",
                "#0F6CBD",
                0,
                8);
            profile.Slots[1].CommandId = "command.one";
            profile.Slots[6].CommandId = "command.two";

            var indexes = WheelGeometryCalculator.GetAssignedCommandSlotIndexes(profile);
            Assert(indexes.Count == 2, "Пустые позиции не должны участвовать в рабочем кольце команд.");
            Assert(indexes[0] == 1 && indexes[1] == 6, "Рабочее кольцо должно сохранять исходные номера слотов.");

            var layouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                indexes.Count,
                WheelCapsuleLevel.Command,
                new WheelGeometrySettings(),
                0.0);
            Assert(layouts.Count == 2, "Две назначенные команды должны образовывать две видимые капсулы.");
            Assert(layouts[0].Center.Y < 0.0 && layouts[1].Center.Y > 0.0,
                "Две команды должны равномерно размещаться сверху и снизу.");
        }

        private static void TestEqualVisualClearanceAndFixedSizes()
        {
            WheelGeometrySettings geometry = new WheelGeometrySettings();
            var layouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                3,
                WheelCapsuleLevel.Command,
                geometry,
                0.0);

            double expectedInnerEdgeRadius = layouts[0].Radius - (layouts[0].Height / 2.0);
            foreach (WheelCapsuleLayout layout in layouts)
            {
                double radians = layout.CenterAngleDegrees * Math.PI / 180.0;
                double radialExtent = (layout.Height / 2.0) +
                                      (((layout.Width - layout.Height) / 2.0) * Math.Abs(Math.Cos(radians)));
                double innerEdgeRadius = layout.Radius - radialExtent;
                Assert(Math.Abs(innerEdgeRadius - expectedInnerEdgeRadius) < 0.001,
                    "Все капсулы должны иметь одинаковый видимый отступ от центра.");
            }

            double originalHeight = layouts[0].Height;
            double originalWidth = layouts[0].Width;
            double originalCenterRadius = geometry.CenterRingOuterRadius;
            geometry.CommandCapsuleRadius += 60.0;
            var movedLayouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                3,
                WheelCapsuleLevel.Command,
                geometry,
                0.0);
            Assert(Math.Abs(movedLayouts[0].Height - originalHeight) < 0.001 &&
                   Math.Abs(movedLayouts[0].Width - originalWidth) < 0.001,
                "Отступ от центра не должен менять размеры капсул.");
            Assert(Math.Abs(geometry.CenterRingOuterRadius - originalCenterRadius) < 0.001,
                "Отступ от центра не должен менять размер центрального круга.");
        }

        private static void TestReturnToStageZone()
        {
            WheelGeometrySettings geometry = new WheelGeometrySettings();
            var stageLayouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                3,
                WheelCapsuleLevel.Stage,
                geometry,
                0.0);
            PointD selectedStageCenter = stageLayouts[0].Center;

            Assert(!WheelGeometryCalculator.IsInsideReturnToStagesZone(
                    selectedStageCenter.X,
                    selectedStageCenter.Y,
                    geometry),
                "Новый центр команд не должен немедленно возвращать кольцо стадий.");
            Assert(WheelGeometryCalculator.IsInsideReturnToStagesZone(0.0, 0.0, geometry),
                "Исходный центр колеса должен возвращать кольцо стадий.");
        }

        private static void TestDynamicCapsuleWidths()
        {
            WheelGeometrySettings geometry = new WheelGeometrySettings();
            double[] widths = { 92.0, 168.0, 124.0 };
            var layouts = WheelGeometryCalculator.CreateCapsuleLayouts(
                widths,
                WheelCapsuleLevel.Command,
                geometry,
                0.0);

            Assert(layouts.Count == widths.Length, "Для каждого текста должна создаваться отдельная капсула.");
            for (int index = 0; index < widths.Length; index++)
            {
                Assert(Math.Abs(layouts[index].Width - widths[index]) < 0.001,
                    "Геометрия должна сохранять индивидуальную ширину капсулы.");
            }

            double windowSize = WheelGeometryCalculator.GetWindowSize(
                geometry,
                new double[0],
                widths);
            Assert(windowSize > 0.0, "Размер окна должен рассчитываться по динамическим капсулам.");
        }

        private static void TestRevitDocumentKinds()
        {
            Assert(
                RevitDocumentKindClassifier.Classify(false, @"C:\Models\Project.rvt") ==
                RevitDocumentKind.ProjectModel,
                "RVT должен определяться как проектная модель.");
            Assert(
                RevitDocumentKindClassifier.Classify(false, string.Empty) ==
                RevitDocumentKind.ProjectModel,
                "Несохранённый проект должен определяться как проектная модель.");
            Assert(
                RevitDocumentKindClassifier.Classify(true, @"C:\Families\Door.rfa") ==
                RevitDocumentKind.Family,
                "RFA не должен разрешать открытие колеса.");
            Assert(
                RevitDocumentKindClassifier.Classify(false, @"C:\Templates\Project.rte") ==
                RevitDocumentKind.ProjectTemplate,
                "RTE не должен разрешать открытие колеса.");
            Assert(
                RevitDocumentKindClassifier.Classify(true, @"C:\Templates\Family.rft") ==
                RevitDocumentKind.FamilyTemplate,
                "RFT не должен разрешать открытие колеса.");
        }

        private static void TestAppearanceSettings()
        {
            WheelSettings settings = WheelSettingsFactory.CreateDefault();
            Assert(settings.Geometry.CapsuleFillOpacity > 0.0 && settings.Geometry.CapsuleFillOpacity <= 1.0,
                "Непрозрачность капсул должна иметь рабочее значение по умолчанию.");
            Assert(settings.Geometry.CenterFillOpacity > 0.0 && settings.Geometry.CenterFillOpacity <= 1.0,
                "Непрозрачность центрального круга должна иметь рабочее значение по умолчанию.");
            Assert(settings.Geometry.CapsuleTextColorHex == "#FFFFFF",
                "Цвет текста капсул должен иметь рабочее значение по умолчанию.");
            Assert(settings.Geometry.CapsuleHoverFillColorHex == "#0F6CBD",
                "Цвет наведения должен иметь рабочее значение по умолчанию.");

            settings.Geometry.CapsuleFillOpacity = 1.1;
            WheelSettingsValidationResult invalid = WheelSettingsValidator.Validate(settings);
            Assert(!invalid.IsValid, "Непрозрачность выше 100% должна отклоняться валидатором.");

            settings = WheelSettingsFactory.CreateDefault();
            settings.Geometry.CapsuleTextColorHex = "white";
            invalid = WheelSettingsValidator.Validate(settings);
            Assert(!invalid.IsValid, "Цвет текста вне формата #RRGGBB должен отклоняться валидатором.");
        }

        private static void TestPostableCommandLocalizationAndMigration()
        {
            WheelSettings settings = WheelSettingsFactory.CreateDefault();
            CommandDescriptor architecturalWall = settings.CommandCatalog.First(
                item => item.RevitPostableCommandName == "ArchitecturalWall");
            Assert(architecturalWall.DisplayName == "Архитектурная стена",
                "API-команда ArchitecturalWall должна показываться с русским названием.");
            Assert(settings.CommandCatalog.All(item => item.RevitPostableCommandName != "Wall"),
                "Устаревшая команда Wall не должна попадать в новый каталог.");
            CommandDescriptor generated = RevitPostableCommandCatalog.CreateDescriptor("EnergyAnalysis");
            Assert(!string.IsNullOrWhiteSpace(generated.DisplayName) &&
                   generated.DisplayName.All(character => !((character >= 'A' && character <= 'Z') ||
                                                             (character >= 'a' && character <= 'z'))),
                "Неизвестная API-команда должна получать русское резервное название.");

            settings.SchemaVersion = 4;
            settings.StageTrigger = null;
            settings.CommandTrigger = null;
            architecturalWall.RevitPostableCommandName = "Wall";
            architecturalWall.DisplayName = "Стена";
            settings.CommandCatalog.Add(new CommandDescriptor
            {
                Id = "postable.auto.ArchitecturalWall",
                Source = WheelCommandSource.RevitPostable,
                DisplayName = "ArchitecturalWall",
                RevitPostableCommandName = "ArchitecturalWall"
            });

            WheelSettingsValidator.Normalize(settings);
            Assert(settings.StageTrigger.Type == WheelTriggerType.MouseXButton1,
                "Миграция должна назначить ближнюю кнопку стадиям.");
            Assert(settings.CommandTrigger.Type == WheelTriggerType.MouseXButton2,
                "Миграция должна сохранить дальнюю кнопку для команд.");
            Assert(settings.CommandCatalog.Count(
                item => item.RevitPostableCommandName == "ArchitecturalWall") == 1,
                "Миграция должна объединить Wall и ArchitecturalWall.");
            Assert(settings.CommandCatalog.First(
                item => item.RevitPostableCommandName == "ArchitecturalWall").DisplayName == "Архитектурная стена",
                "Миграция должна применить русское название команды.");
        }

        private static void TestWheelActivationPolicy()
        {
            RevitContextSnapshot projectContext = new RevitContextSnapshot
            {
                HasActiveDocument = true,
                HasActiveView = true,
                DocumentKind = RevitDocumentKind.ProjectModel,
                IsProjectModelDocument = true
            };
            Assert(WheelActivationPolicy.CanOpenForContext(projectContext),
                "Колесо должно открываться в активной проектной модели.");

            RevitContextSnapshot familyContext = new RevitContextSnapshot
            {
                HasActiveDocument = true,
                HasActiveView = true,
                DocumentKind = RevitDocumentKind.Family,
                IsProjectModelDocument = false
            };
            Assert(!WheelActivationPolicy.CanOpenForContext(familyContext),
                "Колесо не должно открываться в редакторе семейств.");

            projectContext.HasActiveView = false;
            Assert(!WheelActivationPolicy.CanOpenForContext(projectContext),
                "Колесо не должно открываться без активного вида.");
            Assert(!WheelActivationPolicy.CanOpenForContext(null),
                "Колесо не должно открываться без подключённого Revit.");
        }

        private static void TestJsonAndAtomicBackup()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "SABPlus.RadialSmokeTests." + Guid.NewGuid().ToString("N"));

            try
            {
                WheelSettingsRepository repository = new WheelSettingsRepository(directory);
                WheelSettings original = WheelSettingsFactory.CreateDefault();
                repository.Save(original);

                WheelSettings loaded = repository.LoadOrCreateDefault();
                Assert(loaded.Profiles.Count == original.Profiles.Count, "JSON round-trip должен сохранять профили.");

                string originalName = loaded.Profiles[0].Name;
                loaded.Profiles[0].Name = "Изменённый профиль";
                repository.Save(loaded);

                WheelSettings backup = repository.LoadBackup();
                Assert(backup.Profiles[0].Name == originalName, "Резервная копия должна содержать предыдущую версию.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void AssertSector(
            double deltaX,
            double deltaY,
            int sectorCount,
            int expectedIndex,
            WheelGeometrySettings geometry,
            string description)
        {
            WheelHitResult result = WheelGeometryCalculator.HitTest(
                deltaX,
                deltaY,
                sectorCount,
                WheelDisplayLevel.Commands,
                geometry);

            Assert(result.Kind == WheelHitKind.Sector, "Ожидался сектор: " + description);
            Assert(result.SectorIndex == expectedIndex, "Неверный индекс сектора: " + description);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
