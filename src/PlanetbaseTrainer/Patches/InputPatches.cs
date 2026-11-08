using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.UI;

namespace PlanetbaseTrainer.Patches
{
    // Keeps the game from reacting to input aimed at the trainer menu:
    //  - clicks / placement / selection behind the menu,
    //  - mouse-wheel zoom while scrolling the menu,
    //  - keyboard shortcuts and camera keys while typing in the search box or binding a hotkey.

    /// <summary>GameStateGame.update handles shortcuts, Escape and world clicks (simulation runs in fixedUpdate).</summary>
    [PatchGroup("input")]
    [HarmonyPatch(typeof(GameStateGame), nameof(GameStateGame.update))]
    internal static class GameStateGame_update
    {
        private static bool Prefix() => !TrainerUI.WantsKeyboard;
    }

    /// <summary>Selection, module/equipment placement and cancel clicks.</summary>
    [PatchGroup("input")]
    [HarmonyPatch(typeof(GameStateGame), "updateUi")]
    internal static class GameStateGame_updateUi
    {
        private static bool Prefix() => !TrainerUI.IsMouseOverUI;
    }

    /// <summary>The game's own UI uses this for its press-and-hold buttons.</summary>
    [PatchGroup("input")]
    [HarmonyPatch(typeof(Util), nameof(Util.isMouseOnRect))]
    internal static class Util_isMouseOnRect
    {
        private static void Postfix(ref bool __result)
        {
            if (__result && TrainerUI.IsMouseOverUI)
                __result = false;
        }
    }

    [PatchGroup("input")]
    [HarmonyPatch(typeof(CameraManager), nameof(CameraManager.update))]
    internal static class CameraManager_update
    {
        private static readonly AccessTools.FieldRef<CameraManager, float> ZoomAxis = Ref.Field<CameraManager, float>("mZoomAxis");

        private static void Postfix(CameraManager __instance)
        {
            if (ZoomAxis != null && TrainerUI.IsMouseOverUI)
                ZoomAxis(__instance) = 0f;
        }
    }

    /// <summary>Camera movement / rotation / zoom keys.</summary>
    [PatchGroup("input")]
    [HarmonyPatch(typeof(KeyBindingManager), nameof(KeyBindingManager.getCompositeAxis))]
    internal static class KeyBindingManager_getCompositeAxis
    {
        private static bool Prefix(ref float __result)
        {
            if (!TrainerUI.WantsKeyboard)
                return true;
            __result = 0f;
            return false;
        }
    }
}
