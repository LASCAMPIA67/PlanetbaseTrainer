using System;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Game;
using PlanetbaseTrainer.UI;
using UnityEngine;

namespace PlanetbaseTrainer.Features
{
    /// <summary>Per-frame logic: hotkeys, periodic cheat work and game-session tracking.</summary>
    internal static class TrainerRuntime
    {
        private const float InstantBuildPeriod = 0.2f;
        private const float RefillPeriod = 1f;
        private const float ConfirmWindow = 3f;

        private static bool _wasInGame;
        private static bool _sessionTainted;
        private static bool _welcomed;
        private static float _nextInstantBuild;
        private static float _nextRefill;
        private static float _storageWarningCooldown;
        private static ActionCheat _pendingConfirm;
        private static float _pendingConfirmUntil;
        private static bool _bulkChange;

        /// <summary>True when achievements must be held back (a cheat is active, or one was used in this game).</summary>
        public static bool IsTainted => _sessionTainted || _alwaysTainted || Cheats.ActiveCount() > 0;

        private static bool _alwaysTainted;

        /// <summary>Holds achievements back for the whole process lifetime (used by the self-test).</summary>
        public static void TaintForever() => _alwaysTainted = true;

        public static void Update()
        {
            Settings.Tick();
            TrainerUI.Update();

            bool inGame = G.InGame;
            if (inGame != _wasInGame)
            {
                _wasInGame = inGame;
                OnSessionChanged(inGame);
            }

            if (!TrainerUI.BlocksHotkeys && Application.isFocused)
                HandleHotkeys();

            if (inGame)
                Tick();
        }

        private static void OnSessionChanged(bool inGame)
        {
            if (!inGame)
                return;
            _sessionTainted = false;
            TimeControl.ResetFrozen();
            // Re-apply the side effects of cheats that were already on when the save was loaded.
            if (Cheats.FreeBuild.On) GameActions.ClearPendingCosts();
            if (Cheats.UnlimitedPower.On || Cheats.UnlimitedWater.On) Planetbase.Construction.resetGrid();
            if (Cheats.GameSpeed.On) TimeControl.NotifyTimeScaleChanged();
            if (Cheats.NoDiseases.On) GameActions.CureEveryone();
            if (Cheats.ActiveCount() > 0) _sessionTainted = true;
            if (!_welcomed)
            {
                _welcomed = true;
                Toasts.Info(Loc.F("toast.welcome", HotkeyUtil.Format(Settings.MenuKey.Value)), 5f);
            }
        }

        private static void Tick()
        {
            float now = Time.unscaledTime;
            try
            {
                if (Cheats.InstantBuild.On && now >= _nextInstantBuild)
                {
                    _nextInstantBuild = now + InstantBuildPeriod;
                    GameActions.CompleteConstructions();
                }
                if (Cheats.InfiniteResources.On && now >= _nextRefill && !G.IsPaused)
                {
                    _nextRefill = now + RefillPeriod;
                    if (!GameActions.Refill() && now >= _storageWarningCooldown)
                    {
                        _storageWarningCooldown = now + 120f;
                        Toasts.Warn(Loc.T("toast.storagefull"));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("runtime.tick", ex);
            }
        }

        private static void HandleHotkeys()
        {
            if (HotkeyUtil.IsDown(Settings.PanicKey.Value))
            {
                DisableAll();
                return;
            }
            var all = Cheats.All;
            for (int i = 0; i < all.Count; i++)
            {
                var cheat = all[i];
                if (!HotkeyUtil.IsDown(cheat.Hotkey.Value))
                    continue;
                if (!cheat.IsSupported)
                {
                    Toasts.Warn(Loc.F("toast.unsupported", cheat.Name));
                    continue;
                }
                if (cheat is ToggleCheat toggle)
                    toggle.Set(!toggle.On, fromHotkey: true);
                else if (cheat is ActionCheat action)
                    Run(action, fromHotkey: true);
            }
        }

        /// <summary>Runs an action, handling the "press twice to confirm" rule and reporting the result.</summary>
        public static void Run(ActionCheat action, bool fromHotkey)
        {
            if (!G.InGame)
            {
                Toasts.Warn(Loc.F("toast.notingame", action.Name));
                return;
            }
            if (action.Confirm)
            {
                float now = Time.unscaledTime;
                if (_pendingConfirm != action || now > _pendingConfirmUntil)
                {
                    _pendingConfirm = action;
                    _pendingConfirmUntil = now + ConfirmWindow;
                    if (fromHotkey)
                        Toasts.Warn(Loc.F("toast.confirm", action.Name));
                    return;
                }
                _pendingConfirm = null;
            }

            _sessionTainted = true;
            ActionResult result;
            try
            {
                result = action.Execute();
            }
            catch (Exception ex)
            {
                Log.Error($"Action {action.Id} failed: {ex}");
                result = ActionResult.Fail(Loc.F("res.failed", ex.Message));
            }
            if (string.IsNullOrEmpty(result.Message))
                return;
            if (result.Success)
                Toasts.Success(result.Message);
            else
                Toasts.Warn(result.Message);
        }

        /// <summary>True while an action waits for its confirmation click.</summary>
        public static bool IsAwaitingConfirm(ActionCheat action) =>
            _pendingConfirm == action && Time.unscaledTime <= _pendingConfirmUntil;

        public static void OnToggled(ToggleCheat toggle, bool fromHotkey)
        {
            if (toggle.On && G.InGame)
                _sessionTainted = true;
            if (_bulkChange)
                return;
            if (fromHotkey || !TrainerUI.Visible)
            {
                if (toggle.On)
                    Toasts.Success(Loc.F("toast.on", toggle.Name));
                else
                    Toasts.Muted(Loc.F("toast.off", toggle.Name));
            }
        }

        public static void DisableAll()
        {
            bool any = false;
            _bulkChange = true;
            try
            {
                foreach (var toggle in Cheats.Toggles)
                {
                    if (!toggle.On)
                        continue;
                    any = true;
                    toggle.Set(false);
                }
            }
            finally
            {
                _bulkChange = false;
            }
            if (any || !TrainerUI.Visible)
                Toasts.Muted(Loc.T("toast.alloff"));
        }
    }
}
