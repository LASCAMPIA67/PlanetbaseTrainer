using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Game;
using UnityEngine;

namespace PlanetbaseTrainer.Features
{
    /// <summary>Registry of every cheat. Static fields are read directly by the Harmony patches.</summary>
    internal static class Cheats
    {
        private const string OptionsSection = "Options";

        public static readonly List<Cheat> All = new List<Cheat>();
        public static readonly List<ToggleCheat> Toggles = new List<ToggleCheat>();

        // ---- Colonists
        public static ToggleCheat GodMode, Needs, NoDiseases, MoveSpeed, WorkSpeed, InvincibleBots;
        public static BoolOption NeedNutrition, NeedHydration, NeedOxygen, NeedSleep, NeedMorale;
        public static FloatOption MoveSpeedMult, WorkSpeedMult;
        public static ActionCheat HealAll, SpawnColonists, RepairBots, SpawnBots;
        public static ChoiceOption ColonistType, ColonistCount, BotType, BotCount;

        // ---- Construction
        public static ToggleCheat FreeBuild, InstantBuild, FastBuild, Indestructible, NoWear;
        public static FloatOption FastBuildMult;
        public static ActionCheat CompleteAll, RepairAll;

        // ---- Resources
        public static ToggleCheat InfiniteResources, FastProduction, NoSpoilage;
        public static FloatOption InfiniteTarget, FastProductionMult;
        public static BoolOption[] InfiniteTypes;
        public static ActionCheat AddResources, AddCoins;
        public static ChoiceOption AddResourceType, AddResourceAmount, AddCoinsAmount;

        // ---- Base
        public static ToggleCheat UnlimitedPower, UnlimitedWater, UnlimitedOxygen, MaxWind;

        // ---- Trade
        public static ToggleCheat FreeTrade, UnlimitedStock;
        public static ActionCheat CallMerchant, CallColonists, CallVisitors;

        // ---- Threats
        public static ToggleCheat NoDisasters, MeteorShield, NoIntruders;
        public static BoolOption NoSandstorms, NoBlizzards, NoSolarFlares, NoMeteors, NoLightning;
        public static ActionCheat StopDisasters, KillIntruders, TriggerSandstorm, TriggerBlizzard, TriggerSolarFlare, DropMeteor;

        // ---- Time
        public static ToggleCheat GameSpeed, FreezeTime, AlwaysDay;
        public static FloatOption GameSpeedMult, TimeOfDay;
        public static ActionCheat SetTime;

        // ---- Progression
        public static ToggleCheat MaxPrestige;
        public static ActionCheat UnlockTechs, CompleteMilestones, RestoreSelected;

        private static ConfigFile _cfg;

        public static void Init(ConfigFile cfg)
        {
            _cfg = cfg;
            var none = KeyboardShortcut.Empty;

            // ================================================================ Colonists
            GodMode = Toggle("GodMode", Tab.Colonists, "sec.survival", Key(KeyCode.Keypad1), "characters");
            Needs = Toggle("Needs", Tab.Colonists, "sec.survival", Key(KeyCode.Keypad2), "characters");
            NeedNutrition = Bool("Needs", "Nutrition", "need.nutrition", true);
            NeedHydration = Bool("Needs", "Hydration", "need.hydration", true);
            NeedOxygen = Bool("Needs", "Oxygen", "need.oxygen", true);
            NeedSleep = Bool("Needs", "Sleep", "need.sleep", true);
            NeedMorale = Bool("Needs", "Morale", "need.morale", true);
            Needs.Options.Add(new BoolGroupOption("opt.needs", new[] { NeedNutrition, NeedHydration, NeedOxygen, NeedSleep, NeedMorale }));
            NoDiseases = Toggle("NoDiseases", Tab.Colonists, "sec.survival", none, "characters");
            NoDiseases.OnChanged = on => { if (on && G.InGame) GameActions.CureEveryone(); };
            HealAll = Command("HealAll", Tab.Colonists, "sec.survival", Key(KeyCode.Keypad1, ctrl: true), GameActions.HealEveryone);

            MoveSpeed = Toggle("MoveSpeed", Tab.Colonists, "sec.efficiency", none, "characters", "bots");
            MoveSpeedMult = Float("MoveSpeed", "Multiplier", "opt.mult", 2f, 1.5f, 5f, 0.5f, Times);
            MoveSpeed.Options.Add(MoveSpeedMult);
            WorkSpeed = Toggle("WorkSpeed", Tab.Colonists, "sec.efficiency", none, "characters");
            WorkSpeedMult = Float("WorkSpeed", "Multiplier", "opt.mult", 3f, 1.5f, 10f, 0.5f, Times);
            WorkSpeed.Options.Add(WorkSpeedMult);

            SpawnColonists = Command("SpawnColonists", Tab.Colonists, "sec.population", none, GameActions.SpawnColonists);
            ColonistType = Choice("SpawnColonists", "Type", "opt.type", 0, SpecializationCatalog.Colonists.Length,
                i => SpecializationCatalog.Name(SpecializationCatalog.Colonists[i]));
            ColonistCount = Choice("SpawnColonists", "Count", "opt.count", 0, GameActions.SpawnCounts.Length,
                i => GameActions.SpawnCounts[i].ToString());
            SpawnColonists.Options.Add(ColonistType);
            SpawnColonists.Options.Add(ColonistCount);

            InvincibleBots = Toggle("InvincibleBots", Tab.Colonists, "sec.bots", none, "bots");
            InvincibleBots.OnChanged = on => { if (on && G.InGame) GameActions.RepairBots(); };
            RepairBots = Command("RepairBots", Tab.Colonists, "sec.bots", none, GameActions.RepairBots);
            SpawnBots = Command("SpawnBots", Tab.Colonists, "sec.bots", none, GameActions.SpawnBots);
            BotType = Choice("SpawnBots", "Type", "opt.type", 0, SpecializationCatalog.Bots.Length,
                i => SpecializationCatalog.Name(SpecializationCatalog.Bots[i]));
            BotCount = Choice("SpawnBots", "Count", "opt.count", 0, GameActions.SpawnCounts.Length,
                i => GameActions.SpawnCounts[i].ToString());
            SpawnBots.Options.Add(BotType);
            SpawnBots.Options.Add(BotCount);

            // ================================================================ Construction
            FreeBuild = Toggle("FreeBuild", Tab.Construction, "sec.buildings", Key(KeyCode.Keypad3), "construction");
            FreeBuild.OnChanged = on => { if (on && G.InGame) GameActions.ClearPendingCosts(); };
            InstantBuild = Toggle("InstantBuild", Tab.Construction, "sec.buildings", Key(KeyCode.Keypad4), "construction");
            FastBuild = Toggle("FastBuild", Tab.Construction, "sec.buildings", none, "construction");
            FastBuildMult = Float("FastBuild", "Multiplier", "opt.mult", 5f, 2f, 20f, 1f, Times);
            FastBuild.Options.Add(FastBuildMult);
            CompleteAll = Command("CompleteAll", Tab.Construction, "sec.buildings", Key(KeyCode.Keypad4, ctrl: true), () =>
            {
                int count = GameActions.CompleteConstructions();
                return count > 0 ? ActionResult.Ok(Loc.F("res.completed", count)) : ActionResult.Fail(Loc.T("res.nothing"));
            }, "construction");

            Indestructible = Toggle("Indestructible", Tab.Construction, "sec.maintenance", none, "structures");
            NoWear = Toggle("NoWear", Tab.Construction, "sec.maintenance", none, "structures");
            RepairAll = Command("RepairAll", Tab.Construction, "sec.maintenance", none, GameActions.RepairStructures);

            // ================================================================ Resources
            InfiniteResources = Toggle("InfiniteResources", Tab.Resources, "sec.stock", Key(KeyCode.Keypad9));
            InfiniteTarget = Float("InfiniteResources", "TargetStock", "opt.target", 50f, 10f, 300f, 10f, v => Mathf.RoundToInt(v).ToString());
            InfiniteResources.Options.Add(InfiniteTarget);
            InfiniteTypes = new BoolOption[ResourceCatalog.Count];
            for (int i = 0; i < ResourceCatalog.Count; i++)
                InfiniteTypes[i] = Bool("InfiniteResources", ResourceCatalog.TypeNames[i], "rn." + ResourceCatalog.TypeNames[i], ResourceCatalog.RefilledByDefault[i]);
            InfiniteResources.Options.Add(new BoolGroupOption("opt.resources", InfiniteTypes));

            AddResources = Command("AddResources", Tab.Resources, "sec.stock", Key(KeyCode.Keypad9, ctrl: true), GameActions.AddResources);
            AddResourceType = Choice("AddResources", "Resource", "opt.resource", 0, ResourceCatalog.Count, ResourceCatalog.Name);
            AddResourceAmount = Choice("AddResources", "Amount", "opt.amount", 2, GameActions.ResourceAmounts.Length,
                i => GameActions.ResourceAmounts[i].ToString());
            AddResources.Options.Add(AddResourceType);
            AddResources.Options.Add(AddResourceAmount);
            AddCoins = Command("AddCoins", Tab.Resources, "sec.stock", none, GameActions.AddCoins);
            AddCoinsAmount = Choice("AddCoins", "Amount", "opt.amount", 1, GameActions.CoinAmounts.Length,
                i => GameActions.CoinAmounts[i].ToString());
            AddCoins.Options.Add(AddCoinsAmount);

            FastProduction = Toggle("FastProduction", Tab.Resources, "sec.production", none, "production");
            FastProductionMult = Float("FastProduction", "Multiplier", "opt.mult", 3f, 2f, 10f, 1f, Times);
            FastProduction.Options.Add(FastProductionMult);
            NoSpoilage = Toggle("NoSpoilage", Tab.Resources, "sec.production", none, "resources");

            // ================================================================ Base
            UnlimitedPower = Toggle("UnlimitedPower", Tab.Base, "sec.grids", Key(KeyCode.Keypad5), "grid");
            UnlimitedPower.OnChanged = _ => RecomputeGrids();
            UnlimitedWater = Toggle("UnlimitedWater", Tab.Base, "sec.grids", Key(KeyCode.Keypad6), "grid");
            UnlimitedWater.OnChanged = _ => RecomputeGrids();
            UnlimitedOxygen = Toggle("UnlimitedOxygen", Tab.Base, "sec.grids", Key(KeyCode.Keypad7), "oxygen");
            MaxWind = Toggle("MaxWind", Tab.Base, "sec.environment", none, "environment");

            // ================================================================ Trade
            FreeTrade = Toggle("FreeTrade", Tab.Trade, "sec.merchants", Key(KeyCode.Keypad8), "trade");
            UnlimitedStock = Toggle("UnlimitedStock", Tab.Trade, "sec.merchants", none, "trade");
            CallMerchant = Command("CallMerchant", Tab.Trade, "sec.ships", Key(KeyCode.Keypad8, ctrl: true), GameActions.CallMerchant);
            CallColonists = Command("CallColonists", Tab.Trade, "sec.ships", none, GameActions.CallColonists);
            CallVisitors = Command("CallVisitors", Tab.Trade, "sec.ships", none, GameActions.CallVisitors);

            // ================================================================ Threats
            NoDisasters = Toggle("NoDisasters", Tab.Threats, "sec.disasters", Key(KeyCode.Keypad0), "disasters");
            NoSandstorms = Bool("NoDisasters", "Sandstorms", "dis.sandstorms", true);
            NoBlizzards = Bool("NoDisasters", "Blizzards", "dis.blizzards", true);
            NoSolarFlares = Bool("NoDisasters", "SolarFlares", "dis.solarflares", true);
            NoMeteors = Bool("NoDisasters", "Meteors", "dis.meteors", true);
            NoLightning = Bool("NoDisasters", "Lightning", "dis.lightning", true);
            NoDisasters.Options.Add(new BoolGroupOption("opt.disasters", new[] { NoSandstorms, NoBlizzards, NoSolarFlares, NoMeteors, NoLightning }));
            MeteorShield = Toggle("MeteorShield", Tab.Threats, "sec.disasters", none, "disasters");
            StopDisasters = Command("StopDisasters", Tab.Threats, "sec.disasters", none, GameActions.StopDisasters);

            NoIntruders = Toggle("NoIntruders", Tab.Threats, "sec.security", Key(KeyCode.KeypadPeriod), "intruders");
            KillIntruders = Command("KillIntruders", Tab.Threats, "sec.security", Key(KeyCode.KeypadPeriod, ctrl: true), GameActions.KillIntruders);

            TriggerSandstorm = Command("TriggerSandstorm", Tab.Threats, "sec.sandbox", none,
                () => GameActions.TriggerDisaster(GameActions.DisasterKind.Sandstorm), "disasters");
            TriggerBlizzard = Command("TriggerBlizzard", Tab.Threats, "sec.sandbox", none,
                () => GameActions.TriggerDisaster(GameActions.DisasterKind.Blizzard), "disasters");
            TriggerSolarFlare = Command("TriggerSolarFlare", Tab.Threats, "sec.sandbox", none,
                () => GameActions.TriggerDisaster(GameActions.DisasterKind.SolarFlare), "disasters");
            DropMeteor = Command("DropMeteor", Tab.Threats, "sec.sandbox", none, GameActions.DropMeteor);
            TriggerSandstorm.Danger = TriggerBlizzard.Danger = TriggerSolarFlare.Danger = DropMeteor.Danger = true;
            DropMeteor.Confirm = true;

            // ================================================================ Time
            GameSpeed = Toggle("GameSpeed", Tab.Time, "sec.speed", Key(KeyCode.KeypadMultiply), "time");
            GameSpeedMult = Float("GameSpeed", "Multiplier", "opt.mult", 2f, 1.5f, 5f, 0.5f, Times);
            GameSpeed.Options.Add(GameSpeedMult);
            GameSpeed.OnChanged = _ => TimeControl.NotifyTimeScaleChanged();
            GameSpeedMult.Changed += () => { if (GameSpeed.On) TimeControl.NotifyTimeScaleChanged(); };

            FreezeTime = Toggle("FreezeTime", Tab.Time, "sec.daynight", Key(KeyCode.KeypadDivide), "daytime");
            FreezeTime.OnChanged = _ => TimeControl.ResetFrozen();
            AlwaysDay = Toggle("AlwaysDay", Tab.Time, "sec.daynight", none, "daytime");
            SetTime = Command("SetTime", Tab.Time, "sec.daynight", none, GameActions.SetTimeOfDay);
            TimeOfDay = Float("SetTime", "TimeOfDay", "opt.time", TimeControl.Noon, 0f, 0.99f, 1f / 96f, TimeControl.Clock);
            SetTime.Options.Add(TimeOfDay);

            // ================================================================ Progression
            UnlockTechs = Command("UnlockTechs", Tab.Progression, "sec.colony", none, GameActions.UnlockTechs);
            MaxPrestige = Toggle("MaxPrestige", Tab.Progression, "sec.colony", none, "colony");
            CompleteMilestones = Command("CompleteMilestones", Tab.Progression, "sec.colony", none, GameActions.CompleteMilestones);
            CompleteMilestones.Confirm = true;
            CompleteMilestones.Danger = true;
            RestoreSelected = Command("RestoreSelected", Tab.Progression, "sec.selection", Key(KeyCode.Keypad0, ctrl: true), GameActions.RestoreSelection);
        }

        /// <summary>Restores the cheats active during the previous session (after the patches have been applied).</summary>
        public static void RestoreStates()
        {
            bool remember = Settings.RememberStates.Value;
            foreach (var toggle in Toggles)
                toggle.Restore(remember);
        }

        public static void ResetHotkeys()
        {
            foreach (var cheat in All)
                cheat.Hotkey.Value = cheat.DefaultHotkey;
            Settings.MenuKey.Value = (KeyboardShortcut)Settings.MenuKey.DefaultValue;
            Settings.PanicKey.Value = (KeyboardShortcut)Settings.PanicKey.DefaultValue;
            Settings.MarkDirty();
        }

        public static int ActiveCount(Tab? tab = null)
        {
            int count = 0;
            foreach (var toggle in Toggles)
            {
                if (toggle.On && (tab == null || toggle.Tab == tab.Value))
                    count++;
            }
            return count;
        }

        private static void RecomputeGrids()
        {
            // Grids are rebuilt by the game on its next update.
            if (G.InGame)
                Construction.resetGrid();
        }

        private static string Times(float value) => "×" + value.ToString("0.#");

        private static KeyboardShortcut Key(KeyCode key, bool ctrl = false) =>
            ctrl ? new KeyboardShortcut(key, KeyCode.LeftControl) : new KeyboardShortcut(key);

        private static ToggleCheat Toggle(string id, Tab tab, string section, KeyboardShortcut key, params string[] groups)
        {
            var cheat = new ToggleCheat(id, tab, section) { PatchGroups = groups };
            cheat.BindHotkey(_cfg, key);
            cheat.BindState(_cfg);
            All.Add(cheat);
            Toggles.Add(cheat);
            return cheat;
        }

        private static ActionCheat Command(string id, Tab tab, string section, KeyboardShortcut key, Func<ActionResult> execute, params string[] groups)
        {
            var cheat = new ActionCheat(id, tab, section) { PatchGroups = groups, Execute = execute };
            cheat.BindHotkey(_cfg, key);
            All.Add(cheat);
            return cheat;
        }

        private static BoolOption Bool(string cheatId, string key, string labelKey, bool defaultValue) =>
            new BoolOption(_cfg, OptionsSection, cheatId + "." + key, labelKey, defaultValue);

        private static FloatOption Float(string cheatId, string key, string labelKey, float defaultValue, float min, float max,
            float step, Func<float, string> format) =>
            new FloatOption(_cfg, OptionsSection, cheatId + "." + key, labelKey, defaultValue, min, max, step, format);

        private static ChoiceOption Choice(string cheatId, string key, string labelKey, int defaultIndex, int count, Func<int, string> label) =>
            new ChoiceOption(_cfg, OptionsSection, cheatId + "." + key, labelKey, defaultIndex, count, label);
    }
}
