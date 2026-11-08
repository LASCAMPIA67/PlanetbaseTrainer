using System;
using System.Reflection;
using HarmonyLib;

namespace PlanetbaseTrainer.Core
{
    /// <summary>
    /// Fault-tolerant reflection helpers. A missing member (e.g. after a game update) is logged once and
    /// yields null instead of crashing the whole plugin through a TypeInitializationException.
    /// </summary>
    internal static class Ref
    {
        public static AccessTools.FieldRef<T, TField> Field<T, TField>(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<T, TField>(name);
            }
            catch (Exception ex)
            {
                Log.Error($"Field not found: {typeof(T).Name}.{name} ({ex.Message})");
                return null;
            }
        }

        public static FieldInfo StaticField(Type type, string name)
        {
            var field = AccessTools.Field(type, name);
            if (field == null || !field.IsStatic)
                Log.Error($"Static field not found: {type.Name}.{name}");
            return field;
        }

        public static MethodInfo Method(Type type, string name, Type[] parameters = null)
        {
            var method = AccessTools.Method(type, name, parameters);
            if (method == null)
                Log.Error($"Method not found: {type.Name}.{name}");
            return method;
        }
    }
}
