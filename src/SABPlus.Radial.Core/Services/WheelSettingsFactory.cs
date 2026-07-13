using SABPlus.Radial.Core.Models;
using System;
using System.Collections.Generic;

namespace SABPlus.Radial.Core.Services
{
    public static class WheelSettingsFactory
    {
        public static WheelSettings CreateDefault()
        {
            WheelSettings settings = new WheelSettings();

            AddPostableCommand(settings, "ArchitecturalWall");
            AddPostableCommand(settings, "Door");
            AddPostableCommand(settings, "Window");
            AddPostableCommand(settings, "PlaceAComponent");
            AddPostableCommand(settings, "Move");
            AddPostableCommand(settings, "Copy");
            AddPostableCommand(settings, "Align");
            AddPostableCommand(settings, "CreateSimilar");
            AddPostableCommand(settings, "VisibilityOrGraphics");
            AddPostableCommand(settings, "ThinLines");
            AddPostableCommand(settings, "Text");
            AddPostableCommand(settings, "AlignedDimension");

            WheelProfile modeling = CreateProfile("Моделирование", "М", "#0F6CBD", 0, 8);
            Assign(modeling, 0, "ArchitecturalWall");
            Assign(modeling, 1, "Door");
            Assign(modeling, 2, "Window");
            Assign(modeling, 3, "PlaceAComponent");
            Assign(modeling, 4, "Move");
            Assign(modeling, 5, "Copy");
            Assign(modeling, 6, "Align");
            Assign(modeling, 7, "CreateSimilar");

            WheelProfile graphics = CreateProfile("Графика", "Г", "#7C3AED", 1, 8);
            Assign(graphics, 0, "VisibilityOrGraphics");
            Assign(graphics, 1, "ThinLines");

            WheelProfile documentation = CreateProfile("Оформление", "О", "#D97706", 2, 8);
            Assign(documentation, 0, "Text");
            Assign(documentation, 1, "AlignedDimension");

            settings.Profiles.Add(modeling);
            settings.Profiles.Add(graphics);
            settings.Profiles.Add(documentation);
            return settings;
        }

        public static WheelProfile CreateProfile(
            string name,
            string abbreviation,
            string colorHex,
            int order,
            int sectorCount)
        {
            WheelProfile profile = new WheelProfile
            {
                Id = Guid.NewGuid().ToString("D"),
                Name = name,
                Abbreviation = abbreviation,
                ColorHex = colorHex,
                Order = order,
                SectorCount = sectorCount,
                Slots = new List<WheelSlot>()
            };

            for (int index = 0; index < sectorCount; index++)
            {
                profile.Slots.Add(new WheelSlot { Index = index });
            }

            return profile;
        }

        private static void AddPostableCommand(WheelSettings settings, string apiName)
        {
            settings.CommandCatalog.Add(RevitPostableCommandCatalog.CreateDescriptor(apiName));
        }

        private static void Assign(WheelProfile profile, int index, string apiName)
        {
            CommandDescriptor command = RevitPostableCommandCatalog.CreateDescriptor(apiName);
            WheelSlot slot = profile.Slots[index];
            slot.CommandId = command.Id;
            slot.DisplayName = command.DisplayName;
            slot.ShortLabel = command.DisplayName;
            slot.ToolTip = command.Description;
            slot.ShowText = true;
        }
    }
}
