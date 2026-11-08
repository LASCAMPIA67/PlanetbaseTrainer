using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Game;
using UnityEngine;

namespace PlanetbaseTrainer.Patches
{
    /// <summary>Free construction: drop the material requirements right after the player places something.</summary>
    [PatchGroup("construction")]
    [HarmonyPatch(typeof(Buildable), nameof(Buildable.onUserPlaced))]
    internal static class Buildable_onUserPlaced
    {
        private static void Postfix(Buildable __instance)
        {
            if (Cheats.FreeBuild.On)
                __instance.getPendingConstructionCosts()?.clear();
        }
    }

    internal static class BuildSpeed
    {
        /// <summary>Instant: finish on the first build step. Fast: multiply the build step.</summary>
        public static void Apply(ref float timeStep)
        {
            if (Cheats.InstantBuild.On)
                timeStep = Mathf.Max(timeStep, 10000f);
            else if (Cheats.FastBuild.On)
                timeStep *= Cheats.FastBuildMult.Value;
        }
    }

    // Construction.updateBuild and ConstructionComponent.updateBuild both call base.updateBuild with the
    // already-scaled time step, so only the overrides are patched (no double multiplication).
    [PatchGroup("construction")]
    [HarmonyPatch(typeof(Construction), nameof(Construction.updateBuild))]
    internal static class Construction_updateBuild
    {
        private static void Prefix(ref float timeStep) => BuildSpeed.Apply(ref timeStep);
    }

    [PatchGroup("construction")]
    [HarmonyPatch(typeof(ConstructionComponent), nameof(ConstructionComponent.updateBuild))]
    internal static class ConstructionComponent_updateBuild
    {
        private static void Prefix(ref float timeStep) => BuildSpeed.Apply(ref timeStep);
    }

    // Meteor / lightning impacts: Module and Connection override onImpact and call base first,
    // so the overrides themselves must be skipped (otherwise their glass-shattering effects would still play).
    [PatchGroup("structures")]
    [HarmonyPatch(typeof(Construction), nameof(Construction.onImpact))]
    internal static class Construction_onImpact
    {
        private static bool Prefix() => !Cheats.Indestructible.On;
    }

    [PatchGroup("structures")]
    [HarmonyPatch(typeof(Module), nameof(Module.onImpact))]
    internal static class Module_onImpact
    {
        private static bool Prefix() => !Cheats.Indestructible.On;
    }

    [PatchGroup("structures")]
    [HarmonyPatch(typeof(Connection), nameof(Connection.onImpact))]
    internal static class Connection_onImpact
    {
        private static bool Prefix() => !Cheats.Indestructible.On;
    }

    /// <summary>No wear on modules (condition decays inside Module.tick).</summary>
    [PatchGroup("structures")]
    [HarmonyPatch(typeof(Module), nameof(Module.tick))]
    internal static class Module_tick
    {
        private static void Postfix(Module __instance)
        {
            if (Cheats.NoWear.On && __instance.isBuilt())
                GameActions.RepairConstruction(__instance);
        }
    }

    /// <summary>No wear on equipment. Filled before the original runs so the game also clears the "repair" sign.</summary>
    [PatchGroup("structures")]
    [HarmonyPatch(typeof(ConstructionComponent), "updateCondition")]
    internal static class ConstructionComponent_updateCondition
    {
        private static void Prefix(ConstructionComponent __instance)
        {
            if (Cheats.NoWear.On && G.ComponentCondition != null)
                G.Fill(G.ComponentCondition(__instance));
        }
    }
}
