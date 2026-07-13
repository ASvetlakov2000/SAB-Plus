using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SABPlus.Radial.Core.Models
{
    public enum WheelCommandSource
    {
        RevitPostable = 0,
        RevitCommandId = 1,
        SabCommand = 2,
        LocalUtility = 3
    }

    public enum WheelTriggerType
    {
        MouseXButton2 = 0,
        MouseXButton1 = 1,
        Keyboard = 2
    }

    [Flags]
    public enum WheelKeyboardModifiers
    {
        None = 0,
        Control = 1,
        Shift = 2,
        Alt = 4,
        Windows = 8
    }

    public enum WheelDisplayLevel
    {
        Stages = 0,
        Commands = 1
    }

    public enum WheelTriggerAction
    {
        Stages = 0,
        Commands = 1
    }

    public sealed class WheelTriggerSettings
    {
        public WheelTriggerType Type { get; set; }

        public int VirtualKey { get; set; }

        public WheelKeyboardModifiers Modifiers { get; set; }

        public WheelTriggerSettings()
        {
            Type = WheelTriggerType.MouseXButton2;
            VirtualKey = 0;
            Modifiers = WheelKeyboardModifiers.None;
        }
    }

    public sealed class WheelGeometrySettings
    {
        // Block responsible for the central cancel ring and invisible radial selection zones.
        public double CancelRadius { get; set; }

        public double CenterRingOuterRadius { get; set; }

        public double StageActivationRadius { get; set; }

        public double CommandActivationRadius { get; set; }

        public double ReturnToStagesRadius { get; set; }

        // Block responsible for capsule placement and visual dimensions.
        public double StageCapsuleRadius { get; set; }

        public double CommandCapsuleRadius { get; set; }

        public double StageCapsuleWidth { get; set; }

        public double CommandCapsuleWidth { get; set; }

        public double CapsuleHeight { get; set; }

        public double CapsuleCornerRadius { get; set; }

        public double CapsuleIconSize { get; set; }

        public double CapsuleGap { get; set; }

        // Block responsible for user-configurable wheel colors and background transparency.
        public string CapsuleFillColorHex { get; set; }

        public string CapsuleBorderColorHex { get; set; }

        public string CapsuleTextColorHex { get; set; }

        public string CapsuleHoverFillColorHex { get; set; }

        public string CenterFillColorHex { get; set; }

        public string CenterBorderColorHex { get; set; }

        public double CapsuleFillOpacity { get; set; }

        public double CenterFillOpacity { get; set; }

        public int StageHoverDelayMilliseconds { get; set; }

        public double WindowPadding { get; set; }

        public WheelGeometrySettings()
        {
            CancelRadius = 24.0;
            CenterRingOuterRadius = 68.0;
            StageActivationRadius = 78.0;
            CommandActivationRadius = 150.0;
            ReturnToStagesRadius = 92.0;
            StageCapsuleRadius = 152.0;
            CommandCapsuleRadius = 174.0;
            StageCapsuleWidth = 168.0;
            CommandCapsuleWidth = 172.0;
            CapsuleHeight = 46.0;
            CapsuleCornerRadius = 23.0;
            CapsuleIconSize = 30.0;
            CapsuleGap = 2.0;
            CapsuleFillColorHex = "#1F232B";
            CapsuleBorderColorHex = "#5C6370";
            CapsuleTextColorHex = "#FFFFFF";
            CapsuleHoverFillColorHex = "#0F6CBD";
            CenterFillColorHex = "#181C23";
            CenterBorderColorHex = "#0F6CBD";
            CapsuleFillOpacity = 0.96;
            CenterFillOpacity = 0.97;
            StageHoverDelayMilliseconds = 180;
            WindowPadding = 24.0;
        }
    }

    public sealed class CommandDescriptor
    {
        public string Id { get; set; }

        public WheelCommandSource Source { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string RevitPostableCommandName { get; set; }

        public string RevitCommandId { get; set; }

        public string UtilityPath { get; set; }

        public string UtilityArguments { get; set; }

        public string IconPath { get; set; }

        [JsonIgnore]
        public string SourceDisplayName
        {
            get
            {
                switch (Source)
                {
                    case WheelCommandSource.RevitPostable:
                        return "Revit";
                    case WheelCommandSource.RevitCommandId:
                        return "Сторонний add-in";
                    case WheelCommandSource.SabCommand:
                        return "SAB+";
                    case WheelCommandSource.LocalUtility:
                        return "Утилита";
                    default:
                        return Source.ToString();
                }
            }
        }

        public CommandDescriptor()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            RevitPostableCommandName = string.Empty;
            RevitCommandId = string.Empty;
            UtilityPath = string.Empty;
            UtilityArguments = string.Empty;
            IconPath = string.Empty;
        }
    }

    public sealed class WheelSlot
    {
        public int Index { get; set; }

        public string CommandId { get; set; }

        public string DisplayName { get; set; }

        public string ShortLabel { get; set; }

        public string ToolTip { get; set; }

        public string IconPath { get; set; }

        public bool ShowText { get; set; }

        public WheelSlot()
        {
            CommandId = string.Empty;
            DisplayName = string.Empty;
            ShortLabel = string.Empty;
            ToolTip = string.Empty;
            IconPath = string.Empty;
            ShowText = true;
        }
    }

    public sealed class WheelProfile
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public int Order { get; set; }

        public string ColorHex { get; set; }

        public int SectorCount { get; set; }

        public List<WheelSlot> Slots { get; set; }

        [JsonIgnore]
        public int AssignedCommandCount
        {
            get
            {
                if (Slots == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (WheelSlot slot in Slots)
                {
                    if (slot != null && !string.IsNullOrWhiteSpace(slot.CommandId))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public WheelProfile()
        {
            Id = Guid.NewGuid().ToString("D");
            Name = "Новая стадия";
            Abbreviation = "НС";
            ColorHex = "#0F6CBD";
            SectorCount = 8;
            Slots = new List<WheelSlot>();
        }
    }

    public sealed class WheelSettings
    {
        public const int CurrentSchemaVersion = 6;

        public int SchemaVersion { get; set; }

        public WheelTriggerSettings Trigger { get; set; }

        public WheelTriggerSettings StageTrigger { get; set; }

        public WheelTriggerSettings CommandTrigger { get; set; }

        public WheelGeometrySettings Geometry { get; set; }

        public List<WheelProfile> Profiles { get; set; }

        public List<CommandDescriptor> CommandCatalog { get; set; }

        public WheelSettings()
        {
            SchemaVersion = CurrentSchemaVersion;
            Trigger = new WheelTriggerSettings();
            StageTrigger = new WheelTriggerSettings
            {
                Type = WheelTriggerType.MouseXButton1
            };
            CommandTrigger = new WheelTriggerSettings
            {
                Type = WheelTriggerType.MouseXButton2
            };
            Geometry = new WheelGeometrySettings();
            Profiles = new List<WheelProfile>();
            CommandCatalog = new List<CommandDescriptor>();
        }
    }

    public sealed class ProjectWheelState
    {
        public string ProjectKey { get; set; }

        public string ActiveProfileId { get; set; }

        public DateTime LastUpdatedUtc { get; set; }

        public ProjectWheelState()
        {
            ProjectKey = string.Empty;
            ActiveProfileId = string.Empty;
            LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    public sealed class ProjectWheelStateCollection
    {
        public int SchemaVersion { get; set; }

        public List<ProjectWheelState> Projects { get; set; }

        public ProjectWheelStateCollection()
        {
            SchemaVersion = 1;
            Projects = new List<ProjectWheelState>();
        }
    }
}
