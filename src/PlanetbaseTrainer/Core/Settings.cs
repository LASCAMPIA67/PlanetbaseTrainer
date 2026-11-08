using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace PlanetbaseTrainer.Core
{
    internal enum UiLanguage { Auto, French, English }

    internal enum AccentColor { Cyan, Violet, Emerald, Amber, Rose, Orange }

    internal enum HudPosition { TopCenter, TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>Global trainer settings (persisted in BepInEx/config/com.lascampia.planetbasetrainer.cfg).</summary>
    internal static class Settings
    {
        private const string General = "1. General";
        private const string Interface = "2. Interface";
        private const string Window = "9. Window";

        private static float _dirtySince = -1f;
        private static readonly HashSet<string> FavoriteIds = new HashSet<string>(StringComparer.Ordinal);

        public static ConfigFile Config { get; private set; }

        public static ConfigEntry<KeyboardShortcut> MenuKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> PanicKey { get; private set; }
        public static BoolOption RememberStates { get; private set; }
        public static BoolOption BlockAchievements { get; private set; }
        public static BoolOption ProtectMilestones { get; private set; }
        public static BoolOption PauseWhenOpen { get; private set; }

        public static EnumOption<UiLanguage> Language { get; private set; }
        public static BoolOption ScaleAuto { get; private set; }
        public static FloatOption Scale { get; private set; }
        public static EnumOption<AccentColor> Accent { get; private set; }
        public static BoolOption ShowHud { get; private set; }
        public static EnumOption<HudPosition> HudPos { get; private set; }
        public static BoolOption ShowToasts { get; private set; }

        public static ConfigEntry<float> WinX { get; private set; }
        public static ConfigEntry<float> WinY { get; private set; }
        public static ConfigEntry<float> WinW { get; private set; }
        public static ConfigEntry<float> WinH { get; private set; }
        public static ConfigEntry<int> LastTab { get; private set; }
        private static ConfigEntry<string> _favorites;

        public static void Init(ConfigFile cfg)
        {
            Config = cfg;
            // Saving is debounced (see Tick) so that dragging a slider does not rewrite the file every frame.
            cfg.SaveOnConfigSet = false;

            MenuKey = cfg.Bind(General, "MenuHotkey", new KeyboardShortcut(KeyCode.F9), "Opens / closes the trainer menu.");
            PanicKey = cfg.Bind(General, "DisableAllHotkey", new KeyboardShortcut(KeyCode.Home), "Turns every cheat off at once.");
            RememberStates = new BoolOption(cfg, General, "RememberCheatStates", "set.remember", true,
                "Re-enable the cheats that were active when the game was closed.");
            BlockAchievements = new BoolOption(cfg, General, "BlockAchievementsWhileCheating", "set.achievements", true,
                "Prevents Steam achievements from unlocking while a cheat is active or has been used in the current game.");
            ProtectMilestones = new BoolOption(cfg, General, "ProtectMilestonesWhileCheating", "set.milestones", false,
                "Milestones reached while a cheat is active (or was used in the current game) are not saved to the profile.");
            PauseWhenOpen = new BoolOption(cfg, General, "PauseGameWhileMenuOpen", "set.pause", false,
                "Pauses the simulation while the trainer menu is open.");

            Language = new EnumOption<UiLanguage>(cfg, Interface, "Language", "set.lang", UiLanguage.Auto,
                v => Loc.T("lang." + v), "Trainer language (Auto follows the Windows language).");
            Language.Changed += Loc.Refresh;
            ScaleAuto = new BoolOption(cfg, Interface, "AutoScale", "set.scaleauto", true, "Scale the menu with the screen resolution.");
            Scale = new FloatOption(cfg, Interface, "Scale", "set.scale", 1f, 0.75f, 2f, 0.05f,
                v => Mathf.RoundToInt(v * 100f) + " %", "Manual menu scale (used when AutoScale = false).");
            Accent = new EnumOption<AccentColor>(cfg, Interface, "AccentColor", "set.accent", AccentColor.Cyan,
                v => Loc.T("accent." + v), "Accent color of the menu, HUD and notifications.");
            ShowHud = new BoolOption(cfg, Interface, "ShowActiveCheatsHud", "set.hud", true, "Shows the list of active cheats on screen.");
            HudPos = new EnumOption<HudPosition>(cfg, Interface, "HudPosition", "set.hudpos", HudPosition.TopCenter,
                v => Loc.T("hudpos." + v), "Where the active cheats HUD is drawn.");
            ShowToasts = new BoolOption(cfg, Interface, "ShowNotifications", "set.toasts", true, "Shows on-screen notifications.");

            WinX = cfg.Bind(Window, "X", -1f, "Menu position (pixels). -1 = centered.");
            WinY = cfg.Bind(Window, "Y", -1f, "Menu position (pixels). -1 = centered.");
            WinW = cfg.Bind(Window, "Width", 960f, "Menu width (unscaled units).");
            WinH = cfg.Bind(Window, "Height", 640f, "Menu height (unscaled units).");
            LastTab = cfg.Bind(Window, "LastTab", 1, "Last opened tab.");
            _favorites = cfg.Bind(Window, "Favorites", "GodMode,Needs,FreeBuild,InstantBuild,InfiniteResources,FreeTrade",
                "Comma separated list of favorite cheat ids.");

            foreach (var id in _favorites.Value.Split(','))
            {
                var trimmed = id.Trim();
                if (trimmed.Length > 0)
                    FavoriteIds.Add(trimmed);
            }
        }

        public static bool IsFavorite(string id) => FavoriteIds.Contains(id);

        public static void ToggleFavorite(string id)
        {
            if (!FavoriteIds.Remove(id))
                FavoriteIds.Add(id);
            _favorites.Value = string.Join(",", FavoriteIds);
            MarkDirty();
        }

        public static void MarkDirty()
        {
            if (_dirtySince < 0f)
                _dirtySince = Time.unscaledTime;
        }

        /// <summary>Called every frame: flushes pending changes to disk at most once per second.</summary>
        public static void Tick()
        {
            if (_dirtySince >= 0f && Time.unscaledTime - _dirtySince > 1f)
                SaveNow();
        }

        public static void SaveNow()
        {
            if (Config == null)
                return;
            _dirtySince = -1f;
            try
            {
                Config.Save();
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("config.save", ex);
            }
        }
    }
}
