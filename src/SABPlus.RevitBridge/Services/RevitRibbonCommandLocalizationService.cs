using Autodesk.Revit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SABPlus.RevitBridge.Services
{
    public sealed class RevitRibbonCommandLocalizationResult
    {
        public Dictionary<string, string> DisplayNames { get; }

        public int RibbonItemCount { get; set; }

        public int MatchedCommandCount { get; set; }

        public RevitRibbonCommandLocalizationResult()
        {
            DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static class RevitRibbonCommandLocalizationService
    {
        private const int MaximumTraversalDepth = 12;

        public static RevitRibbonCommandLocalizationResult ReadRussianRibbonNames()
        {
            RevitRibbonCommandLocalizationResult result = new RevitRibbonCommandLocalizationResult();

            try
            {
                Type componentManagerType = Type.GetType(
                    "Autodesk.Windows.ComponentManager, AdWindows",
                    false);
                if (componentManagerType == null)
                {
                    return result;
                }

                PropertyInfo ribbonProperty = componentManagerType.GetProperty(
                    "Ribbon",
                    BindingFlags.Public | BindingFlags.Static);
                object ribbon = ribbonProperty?.GetValue(null, null);
                if (ribbon == null)
                {
                    return result;
                }

                Dictionary<string, string> labelsByRibbonId =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                TraverseRibbon(ribbon, 0, visited, labelsByRibbonId, result);

                foreach (string commandName in Enum.GetNames(typeof(PostableCommand)))
                {
                    PostableCommand postableCommand;
                    if (!Enum.TryParse(commandName, true, out postableCommand))
                    {
                        continue;
                    }

                    RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(postableCommand);
                    string ribbonLabel = FindRibbonLabel(labelsByRibbonId, commandId?.Name);
                    if (string.IsNullOrWhiteSpace(ribbonLabel))
                    {
                        continue;
                    }

                    result.DisplayNames[commandName] = ribbonLabel;
                    result.MatchedCommandCount++;
                }
            }
            catch
            {
                // The AdWindows object model is internal and differs between Revit releases.
                // A failure here must not prevent the bridge from starting; the core catalog
                // provides stable Russian fallback names for every PostableCommand.
            }

            return result;
        }

        private static void TraverseRibbon(
            object item,
            int depth,
            HashSet<object> visited,
            Dictionary<string, string> labelsByRibbonId,
            RevitRibbonCommandLocalizationResult result)
        {
            if (item == null || depth > MaximumTraversalDepth || visited.Contains(item))
            {
                return;
            }

            visited.Add(item);
            string itemId = ReadStringProperty(item, "Id") ?? ReadStringProperty(item, "Name");
            string label = ReadStringProperty(item, "Text") ??
                           ReadStringProperty(item, "AutomationName") ??
                           ReadStringProperty(item, "Title");
            if (!string.IsNullOrWhiteSpace(itemId) &&
                !string.IsNullOrWhiteSpace(label) &&
                !LooksLikeInternalIdentifier(label))
            {
                string normalizedId = NormalizeIdentifier(itemId);
                if (!labelsByRibbonId.ContainsKey(normalizedId))
                {
                    labelsByRibbonId.Add(normalizedId, CleanLabel(label));
                    result.RibbonItemCount++;
                }
            }

            TraverseProperty(item, "Source", depth, visited, labelsByRibbonId, result);
            TraverseCollectionProperty(item, "Tabs", depth, visited, labelsByRibbonId, result);
            TraverseCollectionProperty(item, "Panels", depth, visited, labelsByRibbonId, result);
            TraverseCollectionProperty(item, "Items", depth, visited, labelsByRibbonId, result);
        }

        private static void TraverseProperty(
            object item,
            string propertyName,
            int depth,
            HashSet<object> visited,
            Dictionary<string, string> labelsByRibbonId,
            RevitRibbonCommandLocalizationResult result)
        {
            object value = ReadProperty(item, propertyName);
            if (value != null)
            {
                TraverseRibbon(value, depth + 1, visited, labelsByRibbonId, result);
            }
        }

        private static void TraverseCollectionProperty(
            object item,
            string propertyName,
            int depth,
            HashSet<object> visited,
            Dictionary<string, string> labelsByRibbonId,
            RevitRibbonCommandLocalizationResult result)
        {
            IEnumerable values = ReadProperty(item, propertyName) as IEnumerable;
            if (values == null)
            {
                return;
            }

            foreach (object value in values)
            {
                TraverseRibbon(value, depth + 1, visited, labelsByRibbonId, result);
            }
        }

        private static object ReadProperty(object instance, string propertyName)
        {
            try
            {
                PropertyInfo property = instance.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance);
                return property?.GetValue(instance, null);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadStringProperty(object instance, string propertyName)
        {
            return ReadProperty(instance, propertyName) as string;
        }

        private static string FindRibbonLabel(
            Dictionary<string, string> labelsByRibbonId,
            string commandIdName)
        {
            if (string.IsNullOrWhiteSpace(commandIdName))
            {
                return string.Empty;
            }

            string normalizedCommandId = NormalizeIdentifier(commandIdName);
            string exactLabel;
            if (labelsByRibbonId.TryGetValue(normalizedCommandId, out exactLabel))
            {
                return exactLabel;
            }

            // Revit sometimes prefixes the same command identifier for split buttons.
            foreach (KeyValuePair<string, string> pair in labelsByRibbonId)
            {
                if (pair.Key.Length >= 8 &&
                    (pair.Key.EndsWith(normalizedCommandId, StringComparison.OrdinalIgnoreCase) ||
                     normalizedCommandId.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    return pair.Value;
                }
            }

            return string.Empty;
        }

        private static string NormalizeIdentifier(string value)
        {
            char[] characters = value.ToUpperInvariant().ToCharArray();
            List<char> result = new List<char>(characters.Length);
            foreach (char character in characters)
            {
                if (char.IsLetterOrDigit(character))
                {
                    result.Add(character);
                }
            }

            return new string(result.ToArray());
        }

        private static string CleanLabel(string value)
        {
            return value.Replace("_", string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static bool LooksLikeInternalIdentifier(string value)
        {
            string trimmed = value.Trim();
            return trimmed.StartsWith("ID_", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("CustomCtrl_", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
