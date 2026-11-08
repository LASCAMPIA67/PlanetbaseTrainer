using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Game;

// Harmony patches for colonists, visitors and bots.
// Naming convention: <TargetType>_<TargetMethod>. Parameter names must match the game's (Harmony injects by name).
namespace PlanetbaseTrainer.Patches
{
    /// <summary>Single choke point for stat losses (storms, radiation, diseases, impacts...).</summary>
    [PatchGroup("characters")]
    [HarmonyPatch(typeof(Character), nameof(Character.decayIndicator))]
    internal static class Character_decayIndicator
    {
        private static bool Prefix(Character __instance, CharacterIndicator status, ref bool __result)
        {
            if (!Protection.IsProtected(__instance, status))
                return true;
            __result = false;
            return false;
        }
    }

    /// <summary>Hunger, thirst, sleep, oxygen and morale decay happen inside Human.tick: refill before and after.</summary>
    [PatchGroup("characters")]
    [HarmonyPatch(typeof(Human), nameof(Human.tick))]
    internal static class Human_tick
    {
        private static void Prefix(Human __instance) => Protection.OnHumanTick(__instance);

        private static void Postfix(Human __instance) => Protection.OnHumanTick(__instance);
    }

    [PatchGroup("characters")]
    [HarmonyPatch(typeof(Human), "setKo")]
    internal static class Human_setKo
    {
        private static bool Prefix(Human __instance) => !(Cheats.GodMode.On && G.IsFriendlyHuman(__instance));
    }

    [PatchGroup("characters")]
    [HarmonyPatch(typeof(Human), "setDead")]
    internal static class Human_setDead
    {
        private static bool Prefix(Human __instance) => !(Cheats.GodMode.On && G.IsFriendlyHuman(__instance));
    }

    [PatchGroup("characters")]
    [HarmonyPatch(typeof(Human), nameof(Human.setCondition))]
    internal static class Human_setCondition
    {
        private static bool Prefix(Human __instance) => !(Cheats.NoDiseases.On && G.IsFriendlyHuman(__instance));
    }

    [PatchGroup("characters")]
    [HarmonyPatch(typeof(Human), nameof(Human.getMaxSpeed))]
    internal static class Human_getMaxSpeed
    {
        private static void Postfix(Human __instance, ref float __result)
        {
            if (Cheats.MoveSpeed.On && G.IsFriendlyHuman(__instance))
                __result *= Cheats.MoveSpeedMult.Value;
        }
    }

    /// <summary>Work speed drives building, production, repairs, mining and the matching animations.</summary>
    [PatchGroup("characters")]
    [HarmonyPatch(typeof(Character), nameof(Character.getWorkSpeed))]
    internal static class Character_getWorkSpeed
    {
        private static void Postfix(Character __instance, ref float __result)
        {
            if (Cheats.WorkSpeed.On && (__instance is Bot || G.IsFriendlyHuman(__instance)))
                __result *= Cheats.WorkSpeedMult.Value;
        }
    }

    [PatchGroup("bots")]
    [HarmonyPatch(typeof(Bot), nameof(Bot.tick))]
    internal static class Bot_tick
    {
        private static void Prefix(Bot __instance) => Protection.OnBotTick(__instance);

        private static void Postfix(Bot __instance) => Protection.OnBotTick(__instance);
    }

    [PatchGroup("bots")]
    [HarmonyPatch(typeof(Bot), "setKo")]
    internal static class Bot_setKo
    {
        private static bool Prefix() => !Cheats.InvincibleBots.On;
    }

    [PatchGroup("bots")]
    [HarmonyPatch(typeof(Bot), "setDead")]
    internal static class Bot_setDead
    {
        private static bool Prefix() => !Cheats.InvincibleBots.On;
    }

    [PatchGroup("bots")]
    [HarmonyPatch(typeof(Bot), nameof(Bot.getMaxSpeed))]
    internal static class Bot_getMaxSpeed
    {
        private static void Postfix(ref float __result)
        {
            if (Cheats.MoveSpeed.On)
                __result *= Cheats.MoveSpeedMult.Value;
        }
    }
}
