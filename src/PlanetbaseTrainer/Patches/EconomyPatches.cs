using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Game;

namespace PlanetbaseTrainer.Patches
{
    // ------------------------------------------------------------------ Resources & production

    /// <summary>Resources lying outside slowly lose condition and vanish: keep them pristine.</summary>
    [PatchGroup("resources")]
    [HarmonyPatch(typeof(Resource), nameof(Resource.tick))]
    internal static class Resource_tick
    {
        private static bool Prefix(Resource __instance)
        {
            if (!Cheats.NoSpoilage.On)
                return true;
            if (G.ResourceCondition != null)
                G.Fill(G.ResourceCondition(__instance));
            return false;
        }
    }

    [PatchGroup("production")]
    [HarmonyPatch(typeof(ConstructionComponent), "updateProduction")]
    internal static class ConstructionComponent_updateProduction
    {
        private static void Prefix(ref float timeStep)
        {
            if (Cheats.FastProduction.On)
                timeStep *= Cheats.FastProductionMult.Value;
        }
    }

    // ------------------------------------------------------------------ Power / water / oxygen

    /// <summary>
    /// Grid balance = sum of generation (positive) and consumption (negative). Zeroing consumption keeps every
    /// building powered / supplied, even isolated ones, without touching generators or batteries.
    /// </summary>
    [PatchGroup("grid")]
    [HarmonyPatch(typeof(Grid), "getGeneration", typeof(Construction), typeof(GridResource))]
    internal static class Grid_getGeneration
    {
        private static void Postfix(GridResource gridResource, ref float __result)
        {
            if (__result >= 0f)
                return;
            if (gridResource == GridResource.Power ? Cheats.UnlimitedPower.On : Cheats.UnlimitedWater.On)
                __result = 0f;
        }
    }

    [PatchGroup("oxygen")]
    [HarmonyPatch(typeof(Construction), "updateOxygen")]
    internal static class Construction_updateOxygen
    {
        private static void Postfix(Construction __instance)
        {
            if (Cheats.UnlimitedOxygen.On && __instance.isBuilt() && __instance.requiresOxygen())
                G.Fill(__instance.getOxygenIndicator());
        }
    }

    /// <summary>Breathing colonists consume the module's oxygen.</summary>
    [PatchGroup("oxygen")]
    [HarmonyPatch(typeof(Construction), nameof(Construction.decreaseOxygen))]
    internal static class Construction_decreaseOxygen
    {
        private static bool Prefix() => !Cheats.UnlimitedOxygen.On;
    }

    // ------------------------------------------------------------------ Trade

    [PatchGroup("trade")]
    [HarmonyPatch(typeof(GuiMerchantWindow), "updateUi")]
    internal static class GuiMerchantWindow_updateUi
    {
        private static void Postfix(GuiMerchantWindow __instance)
        {
            if (Cheats.FreeTrade.On)
                TradeControl.EnableTradeButton(__instance);
        }
    }

    [PatchGroup("trade")]
    [HarmonyPatch(typeof(GuiMerchantWindow), MethodType.Constructor, typeof(MerchantShip))]
    internal static class GuiMerchantWindow_ctor
    {
        private static void Postfix(GuiMerchantWindow __instance)
        {
            if (Cheats.UnlimitedStock.On)
                TradeControl.BoostStock(__instance);
        }
    }

    /// <summary>
    /// With an empty "needed products" list the ship delivers immediately (game logic), so the colony keeps
    /// everything it had selected to pay with.
    /// </summary>
    [PatchGroup("trade")]
    [HarmonyPatch(typeof(MerchantShip), nameof(MerchantShip.onTradeAgreed))]
    internal static class MerchantShip_onTradeAgreed
    {
        private static void Prefix(MerchantShip __instance, ref ProductAmounts neededProducts)
        {
            if (Cheats.FreeTrade.On)
                TradeControl.WaivePayment(__instance, ref neededProducts);
        }
    }
}
