using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Game;
using PlanetbaseTrainer.UI;
using UnityEngine;

namespace PlanetbaseTrainer.Diagnostics
{
    /// <summary>
    /// Opt-in end-to-end test, enabled only when the environment variable PBTRAINER_SELFTEST points to an output folder.
    /// It opens the menu on every tab, starts a throw-away new game, enables every cheat, runs every action
    /// (except those writing to the player profile), takes screenshots, then quits. Achievements and milestones are held
    /// back for the whole run. Never active in normal use.
    /// <para>
    /// PBTRAINER_SELFTEST_LANGS (for example "en,fr") repeats the in-game screenshots in each listed language: this is how
    /// the documentation screenshots are produced. The language override is never saved to the configuration.
    /// </para>
    /// </summary>
    internal static class SelfTest
    {
        private static readonly AccessTools.FieldRef<GameManager, GameManager.State> ManagerState =
            Ref.Field<GameManager, GameManager.State>("mState");
        private static readonly System.Reflection.FieldInfo ManagerInstance = Ref.StaticField(typeof(GameManager), "mInstance");

        private static string _outputDir;
        private static string _gameFilesDir;
        private static List<UiLanguage?> _languages;
        private static IEnumerator<float> _script;
        private static float _resumeAt;

        public static void Init()
        {
            string dir = Environment.GetEnvironmentVariable("PBTRAINER_SELFTEST");
            if (string.IsNullOrEmpty(dir))
                return;
            _outputDir = dir;
            _languages = ParseLanguages(Environment.GetEnvironmentVariable("PBTRAINER_SELFTEST_LANGS"));
            Directory.CreateDirectory(dir);
            try
            {
                RedirectGameFiles(Path.Combine(dir, "game-files"));
            }
            catch (Exception ex)
            {
                Log.Error("[SelfTest] cancelled, the game files could not be redirected: " + ex);
                return;
            }
            // A test run must never unlock a Steam achievement nor record a milestone in the player profile.
            TrainerRuntime.TaintForever();
            MilestoneGuard.ForceProtect = true;
            _script = Script();
            Log.Info("[SelfTest] enabled, output: " + dir);
        }

        public static void Update()
        {
            if (_script == null)
                return;
            AudioListener.volume = 0f;
            if (Time.unscaledTime < _resumeAt)
                return;
            try
            {
                if (_script.MoveNext())
                    _resumeAt = Time.unscaledTime + _script.Current;
                else
                    _script = null;
            }
            catch (Exception ex)
            {
                Log.Error("[SelfTest] exception: " + ex);
                _script = null;
                Application.Quit();
            }
        }

        private static GameManager Manager => ManagerInstance?.GetValue(null) as GameManager;

        private static GameState CurrentState => Manager?.getGameState();

        private static IEnumerator<float> Script()
        {
            // ---- 1. Reach the title screen (skipping the logo).
            float deadline = Time.unscaledTime + 90f;
            while (!(CurrentState is GameStateTitle))
            {
                if (CurrentState is GameStateLogo && ManagerState != null && ManagerState(Manager) == GameManager.State.Updating)
                    Manager.setGameStateTitle();
                if (Time.unscaledTime > deadline)
                {
                    Log.Error("[SelfTest] title screen never reached");
                    Application.Quit();
                    yield break;
                }
                yield return 0.5f;
            }
            SetLanguage(_languages[0]);
            yield return 3f;

            // ---- 2. The menu on the title screen.
            foreach (Tab tab in Enum.GetValues(typeof(Tab)))
            {
                TrainerUI.Show(tab);
                yield return 0.7f;
                Shot("1_title_" + tab);
                yield return 0.3f;
            }
            TrainerUI.Show(Tab.Colonists, SearchTerm());
            yield return 0.7f;
            Shot("1_title_search");
            yield return 0.3f;
            TrainerUI.SetVisible(false);

            // ---- 3. A throw-away new game.
            Manager.setGameStateGameNew(424242, 0, false, null);
            deadline = Time.unscaledTime + 180f;
            while (!G.InGame)
            {
                // Skip the "press any key" loading screen.
                if (Manager != null && ManagerState != null && ManagerState(Manager) == GameManager.State.Waiting && CurrentState is GameStateGame)
                {
                    CurrentState.onStartUpdating();
                    ManagerState(Manager) = GameManager.State.Updating;
                }
                if (Time.unscaledTime > deadline)
                {
                    Log.Error("[SelfTest] the game never started");
                    Application.Quit();
                    yield break;
                }
                yield return 0.5f;
            }
            Log.Info("[SelfTest] game started");
            yield return 10f;
            Shot("2_game_start");

            // ---- 4. Every toggle on.
            foreach (var toggle in Cheats.Toggles)
            {
                toggle.Set(true);
                Log.Info($"[SelfTest] cheat {toggle.Id}: {(toggle.On ? "ON" : "UNAVAILABLE")}");
            }
            yield return 2f;

            // ---- 5. Every action, except those that write to the player's profile
            //         (owning every technology completes the "Technocracy" milestone, which the game saves to the profile).
            foreach (var cheat in Cheats.All)
            {
                if (!(cheat is ActionCheat action) || action == Cheats.CompleteMilestones || action == Cheats.UnlockTechs)
                    continue;
                try
                {
                    var result = action.Execute();
                    Log.Info($"[SelfTest] action {action.Id}: {(result.Success ? "OK" : "refused")} - {result.Message}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[SelfTest] action {action.Id} threw: {ex}");
                }
                yield return 0.4f;
            }

            // ---- 6. Let the simulation run with everything on, then capture the HUD and every tab in each language.
            yield return 20f;
            foreach (var language in _languages)
            {
                SetLanguage(language);
                string suffix = Suffix(language);
                TrainerUI.SetVisible(false);
                yield return 0.5f;

                // A few on-screen confirmations, as when cheats are toggled with their hotkeys.
                Toasts.Info(Loc.F("toast.welcome", HotkeyUtil.Format(Settings.MenuKey.Value)));
                Toasts.Success(Loc.F("toast.on", Cheats.GodMode.Name));
                Toasts.Success(Loc.F("toast.on", Cheats.InfiniteResources.Name));
                yield return 1f;
                Shot("3_game_hud" + suffix);
                // The capture happens at the end of the frame: keep the menu hidden until then.
                yield return 0.3f;

                foreach (Tab tab in Enum.GetValues(typeof(Tab)))
                {
                    TrainerUI.Show(tab);
                    yield return 0.7f;
                    Shot("4_game_" + tab + suffix);
                    yield return 0.3f;
                }
                TrainerUI.Show(Tab.Colonists, SearchTerm());
                yield return 0.7f;
                Shot("4_game_search" + suffix);
                yield return 0.3f;
                TrainerUI.SetVisible(false);
            }
            SetLanguage(null);

            // ---- 7. Everything off again.
            TrainerRuntime.DisableAll();
            yield return 3f;
            Shot("5_game_off");
            yield return 1.5f;
            Log.Info($"[SelfTest] done, errors logged: {Log.ErrorCount}");
            Settings.SaveNow();
            Application.Quit();
        }

        /// <summary>
        /// The game keeps its profile, settings and saves under <c>Util.getFilesFolder()</c> (Documents\Planetbase).
        /// The test plays with a throw-away profile inside its output folder instead, so the player's own files are never
        /// read or written. Applied from the plugin's Awake, before any game code has run.
        /// </summary>
        private static void RedirectGameFiles(string folder)
        {
            _gameFilesDir = folder;
            var target = AccessTools.Method(typeof(Util), nameof(Util.getFilesFolder));
            new Harmony(MyPluginInfo.PLUGIN_GUID + ".selftest").Patch(target,
                prefix: new HarmonyMethod(typeof(SelfTest), nameof(GetFilesFolderPrefix)));
            Log.Info("[SelfTest] game files redirected to " + folder);
        }

        private static bool GetFilesFolderPrefix(ref string __result)
        {
            __result = _gameFilesDir;
            return false;
        }

        private static List<UiLanguage?> ParseLanguages(string value)
        {
            var languages = new List<UiLanguage?>();
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var part in value.Split(','))
                {
                    switch (part.Trim().ToLowerInvariant())
                    {
                        case "en":
                            languages.Add(UiLanguage.English);
                            break;
                        case "fr":
                            languages.Add(UiLanguage.French);
                            break;
                        default:
                            Log.Warn("[SelfTest] unknown language ignored: " + part);
                            break;
                    }
                }
            }
            if (languages.Count == 0)
                languages.Add(null); // Current language only.
            return languages;
        }

        private static void SetLanguage(UiLanguage? language)
        {
            if (Loc.Override == language)
                return;
            Loc.Override = language;
            Loc.Refresh();
        }

        private static string Suffix(UiLanguage? language) =>
            language == null ? string.Empty : language == UiLanguage.French ? "_fr" : "_en";

        /// <summary>A search that matches the "Unlimited power" cheat in the current language.</summary>
        private static string SearchTerm() => Loc.IsFrench ? "energie" : "power";

        private static void Shot(string name)
        {
            string path = Path.Combine(_outputDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Log.Info("[SelfTest] capture " + name);
        }
    }
}
