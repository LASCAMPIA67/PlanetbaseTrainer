using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Patches;

namespace PlanetbaseTrainer.Features
{
    /// <summary>Menu tabs, in display order.</summary>
    internal enum Tab
    {
        Favorites,
        Colonists,
        Construction,
        Resources,
        Base,
        Trade,
        Threats,
        Time,
        Progression,
        Settings,
    }

    /// <summary>Common data of every cheat: identity, location in the menu, hotkey, options and required patches.</summary>
    internal abstract class Cheat
    {
        public readonly string Id;
        public readonly Tab Tab;
        public readonly string SectionKey;
        public readonly List<Option> Options = new List<Option>();

        protected Cheat(string id, Tab tab, string sectionKey)
        {
            Id = id;
            Tab = tab;
            SectionKey = sectionKey;
        }

        public ConfigEntry<KeyboardShortcut> Hotkey { get; private set; }

        public KeyboardShortcut DefaultHotkey { get; private set; }

        /// <summary>Patch groups this cheat relies on; if one failed to apply, the cheat is shown as unavailable.</summary>
        public string[] PatchGroups { get; set; } = new string[0];

        /// <summary>Destructive / irreversible actions get a red tag in the menu.</summary>
        public bool Danger { get; set; }

        public string Name => Loc.T("c." + Id);

        public string Description => Loc.T("c." + Id + ".d");

        public bool IsSupported => PatchManager.AreOk(PatchGroups);

        public void BindHotkey(ConfigFile cfg, KeyboardShortcut defaultHotkey)
        {
            DefaultHotkey = defaultHotkey;
            Hotkey = cfg.Bind("Hotkeys", Id, defaultHotkey, "Hotkey: " + Id);
        }
    }

    /// <summary>A cheat that stays on until switched off.</summary>
    internal sealed class ToggleCheat : Cheat
    {
        private ConfigEntry<bool> _saved;

        public ToggleCheat(string id, Tab tab, string sectionKey) : base(id, tab, sectionKey)
        {
        }

        /// <summary>Read by the Harmony patches: a plain field read, safe for hot paths.</summary>
        public bool On { get; private set; }

        /// <summary>Side effects to run when the state changes (e.g. recompute the power grid).</summary>
        public Action<bool> OnChanged { get; set; }

        public void BindState(ConfigFile cfg)
        {
            _saved = cfg.Bind("States", Id, false, "Last state of the cheat (restored if RememberCheatStates = true).");
        }

        /// <summary>Restores the saved state at startup, without notifications.</summary>
        public void Restore(bool remember)
        {
            On = remember && _saved.Value && IsSupported;
        }

        public void Set(bool on, bool fromHotkey = false)
        {
            if (On == on)
                return;
            if (on && !IsSupported)
                return;
            On = on;
            _saved.Value = on;
            Settings.MarkDirty();
            try
            {
                OnChanged?.Invoke(on);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("toggle." + Id, ex);
            }
            TrainerRuntime.OnToggled(this, fromHotkey);
        }
    }

    /// <summary>A one-shot command. Returns a short, localized result message (or null).</summary>
    internal sealed class ActionCheat : Cheat
    {
        public ActionCheat(string id, Tab tab, string sectionKey) : base(id, tab, sectionKey)
        {
        }

        public Func<ActionResult> Execute { get; set; }

        /// <summary>Requires a second click / key press within a few seconds.</summary>
        public bool Confirm { get; set; }
    }

    internal readonly struct ActionResult
    {
        public readonly string Message;
        public readonly bool Success;

        private ActionResult(string message, bool success)
        {
            Message = message;
            Success = success;
        }

        public static ActionResult Ok(string message) => new ActionResult(message, true);

        public static ActionResult Fail(string message) => new ActionResult(message, false);
    }
}
