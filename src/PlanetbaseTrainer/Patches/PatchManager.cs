using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PlanetbaseTrainer.Core;

namespace PlanetbaseTrainer.Patches
{
    /// <summary>Tags a Harmony patch class with the feature group it belongs to.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class PatchGroupAttribute : Attribute
    {
        public PatchGroupAttribute(string group)
        {
            Group = group;
        }

        public string Group { get; }
    }

    /// <summary>
    /// Applies every patch class individually. If a game update renames a method, only the features depending on
    /// that patch are disabled (and shown as unavailable in the menu) instead of the whole trainer failing to load.
    /// </summary>
    internal static class PatchManager
    {
        private static readonly Dictionary<string, bool> GroupStatus = new Dictionary<string, bool>(StringComparer.Ordinal);

        public static readonly List<string> Failures = new List<string>();

        public static int AppliedCount { get; private set; }

        public static int TotalCount { get; private set; }

        public static void ApplyAll(Harmony harmony)
        {
            foreach (var type in GetPatchClasses())
            {
                var group = type.GetCustomAttribute<PatchGroupAttribute>().Group;
                TotalCount++;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    AppliedCount++;
                    if (!GroupStatus.ContainsKey(group))
                        GroupStatus[group] = true;
                }
                catch (Exception ex)
                {
                    GroupStatus[group] = false;
                    Failures.Add(group + " / " + type.Name);
                    Log.Error($"Patch '{type.Name}' ({group}) not applied: {ex.GetBaseException().Message}");
                }
            }
        }

        /// <summary>True when every listed group was patched successfully (an empty list is always OK).</summary>
        public static bool AreOk(string[] groups)
        {
            if (groups == null)
                return true;
            for (int i = 0; i < groups.Length; i++)
            {
                if (!GroupStatus.TryGetValue(groups[i], out bool ok) || !ok)
                    return false;
            }
            return true;
        }

        private static IEnumerable<Type> GetPatchClasses()
        {
            Type[] types;
            try
            {
                types = typeof(PatchManager).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }
            return types
                .Where(t => t.GetCustomAttribute<PatchGroupAttribute>() != null)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
        }
    }
}
