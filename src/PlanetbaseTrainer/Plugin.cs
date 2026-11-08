using System;
using BepInEx;
using HarmonyLib;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Patches;
using PlanetbaseTrainer.UI;
using UnityEngine;

namespace PlanetbaseTrainer
{
    /// <summary>
    /// Entry point. Bootstraps settings, cheats and Harmony patches, then forwards Update/OnGUI to the trainer.
    /// </summary>
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Planetbase.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;
        private bool _ready;
        private bool _quitting;

        private void Awake()
        {
            Log.Init(Logger);
            ProtectFromGameCleanup();
            // The trainer draws with explicit rects only: skipping the IMGUI layout pass halves OnGUI calls.
            useGUILayout = false;

            try
            {
                // The self-test plays with a throw-away configuration (null in normal use).
                var config = Diagnostics.SelfTest.CreateConfig() ?? Config;
                Settings.Init(config);
                Loc.Init();
                Cheats.Init(config);
                TrainerUI.Init();

                _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
                PatchManager.ApplyAll(_harmony);
                Cheats.RestoreStates();
                Settings.SaveNow();
                _ready = true;
                Diagnostics.SelfTest.Init();

                Log.Info($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} ready: Unity {Application.unityVersion}, " +
                         $"{Cheats.All.Count} cheats, patches {PatchManager.AppliedCount}/{PatchManager.TotalCount}" +
                         (PatchManager.Failures.Count > 0 ? " (failed: " + string.Join(", ", PatchManager.Failures) + ")" : string.Empty));
            }
            catch (Exception ex)
            {
                Log.Error("Initialization failed: " + ex);
            }
        }

        /// <summary>
        /// Planetbase's GameManager constructor destroys every root GameObject that is not tagged "RootObject"
        /// (this would silently kill the BepInEx manager object and this plugin with it).
        /// </summary>
        private void ProtectFromGameCleanup()
        {
            try
            {
                gameObject.tag = "RootObject";
            }
            catch (UnityException ex)
            {
                Log.Warn("Tag 'RootObject' unavailable: " + ex.Message);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (!_ready)
                return;
            try
            {
                TrainerRuntime.Update();
                Diagnostics.SelfTest.Update();
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("plugin.update", ex);
            }
        }

        private void OnGUI()
        {
            if (_ready)
                TrainerUI.OnGUI();
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            if (_ready)
                Settings.SaveNow();
            // Known BepInEx 5 crash at exit on 32-bit Unity 2022.3 (see MonoExitCrashFix).
            int repaired = MonoExitCrashFix.Apply();
            if (repaired > 0)
                Log.Info($"Mono exit crash workaround: {repaired} assembly(ies) repaired (BepInEx #398 / MonoMod #329)");
            Log.Flush();
        }

        private void OnDestroy()
        {
            // Should only ever happen on exit; anything else means the protection above was defeated.
            if (!_quitting)
                Log.Warn("The trainer GameObject was destroyed: the menu and hotkeys will stop responding.");
        }
    }
}
