using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Game;

namespace PlanetbaseTrainer.Patches
{
    // ------------------------------------------------------------------ Environment & time

    [PatchGroup("environment")]
    [HarmonyPatch(typeof(EnvironmentManager), "updateWind")]
    internal static class EnvironmentManager_updateWind
    {
        private static void Postfix(EnvironmentManager __instance)
        {
            if (!Cheats.MaxWind.On)
                return;
            var planet = PlanetManager.getCurrentPlanet();
            // Airless planets have no wind at all: leave them alone.
            if (planet != null && planet.getAtmosphereDensity() > Planet.Quantity.None)
                __instance.getWindIndicator()?.setValue(1f);
        }
    }

    [PatchGroup("daytime")]
    [HarmonyPatch(typeof(EnvironmentManager), nameof(EnvironmentManager.update), typeof(float), typeof(bool))]
    internal static class EnvironmentManager_update
    {
        private static void Prefix(EnvironmentManager __instance) => TimeControl.Apply(__instance);

        private static void Postfix(EnvironmentManager __instance) => TimeControl.Apply(__instance);
    }

    /// <summary>The simulation time step is multiplied by this value every fixed update.</summary>
    [PatchGroup("time")]
    [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.getTimeScale))]
    internal static class TimeManager_getTimeScale
    {
        private static void Postfix(ref float __result)
        {
            if (Cheats.GameSpeed.On && __result > 0f)
                __result *= Cheats.GameSpeedMult.Value;
        }
    }

    // ------------------------------------------------------------------ Disasters

    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(Sandstorm), nameof(Sandstorm.update))]
    internal static class Sandstorm_update
    {
        private static bool Prefix(Sandstorm __instance) => DisasterControl.GateUpdate(__instance);
    }

    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(Blizzard), nameof(Blizzard.update))]
    internal static class Blizzard_update
    {
        private static bool Prefix(Blizzard __instance) => DisasterControl.GateUpdate(__instance);
    }

    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(SolarFlare), nameof(SolarFlare.update))]
    internal static class SolarFlare_update
    {
        private static bool Prefix(SolarFlare __instance) => DisasterControl.GateUpdate(__instance);
    }

    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(Sandstorm), nameof(Sandstorm.trigger))]
    internal static class Sandstorm_trigger
    {
        private static bool Prefix(Sandstorm __instance) => !DisasterControl.IsBlocked(__instance);
    }

    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(Blizzard), nameof(Blizzard.trigger))]
    internal static class Blizzard_trigger
    {
        private static bool Prefix(Blizzard __instance) => !DisasterControl.IsBlocked(__instance);
    }

    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(SolarFlare), nameof(SolarFlare.trigger))]
    internal static class SolarFlare_trigger
    {
        private static bool Prefix(SolarFlare __instance) => !DisasterControl.IsBlocked(__instance);
    }

    /// <summary>No new meteor is spawned (manual "drop a meteor" calls spawnMeteor directly and still works).</summary>
    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(MeteorManager), nameof(MeteorManager.update))]
    internal static class MeteorManager_update
    {
        private static bool Prefix() => !(Cheats.NoDisasters.On && Cheats.NoMeteors.Value);
    }

    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(ThunderstormManager), nameof(ThunderstormManager.spawnThunder))]
    internal static class ThunderstormManager_spawnThunder
    {
        private static bool Prefix() => !(Cheats.NoDisasters.On && Cheats.NoLightning.Value);
    }

    /// <summary>Re-uses the developers' debug switch "100% meteor interception chance".</summary>
    [PatchGroup("disasters")]
    [HarmonyPatch(typeof(DebugManager), nameof(DebugManager.is100PercentInterceptionChance))]
    internal static class DebugManager_is100PercentInterceptionChance
    {
        private static void Postfix(ref bool __result)
        {
            if (Cheats.MeteorShield.On)
                __result = true;
        }
    }

    // ------------------------------------------------------------------ Security & colony

    [PatchGroup("intruders")]
    [HarmonyPatch(typeof(LandingShip), nameof(LandingShip.setIntruders))]
    internal static class LandingShip_setIntruders
    {
        private static bool Prefix() => !Cheats.NoIntruders.On;
    }

    [PatchGroup("colony")]
    [HarmonyPatch(typeof(Colony), "calculatePrestige")]
    internal static class Colony_calculatePrestige
    {
        private static void Postfix(Colony __instance)
        {
            if (!Cheats.MaxPrestige.On)
                return;
            var prestige = __instance.getPrestigeIndicator();
            prestige?.setValue(prestige.getMax());
        }
    }

    /// <summary>Optional protection of the profile progression (see MilestoneGuard).</summary>
    [PatchGroup("milestones")]
    [HarmonyPatch(typeof(MilestoneManager), nameof(MilestoneManager.onMilestoneCompleted))]
    internal static class MilestoneManager_onMilestoneCompleted
    {
        private static bool Prefix() => !MilestoneGuard.ShouldBlock;
    }

    // ------------------------------------------------------------------ Steam achievements

    /// <summary>Achievements are re-evaluated continuously, so holding them back is fully reversible.</summary>
    [PatchGroup("achievements")]
    [HarmonyPatch(typeof(ServerInterfaceManager), "checkAchievement")]
    internal static class ServerInterfaceManager_checkAchievement
    {
        private static bool Prefix() => !(Settings.BlockAchievements.Value && TrainerRuntime.IsTainted);
    }
}
