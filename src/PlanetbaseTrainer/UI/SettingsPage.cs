using System;
using BepInEx.Configuration;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Patches;
using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>The "Settings" tab: interface, global hotkeys, behavior and about.</summary>
    internal static class SettingsPage
    {
        private const float RowHeight = 66f;
        private const float RowGap = 8f;
        private const float HeaderHeight = 40f;
        private const float AboutHeight = 132f;

        private static readonly AccentColor[] Accents = (AccentColor[])Enum.GetValues(typeof(AccentColor));

        private enum Kind { Header, Row, About }

        private struct Item
        {
            public Kind Kind;
            public string Key;
            public Action<Rect, float> Control;
            public float ControlWidth;
        }

        private static Item[] _items;

        private static Item[] Items => _items ?? (_items = BuildItems());

        public static float Measure(float width, float s)
        {
            float total = 0f;
            var items = Items;
            for (int i = 0; i < items.Length; i++)
                total += Height(items[i], i, s);
            return total;
        }

        public static void Render(float width, float offsetY, float viewHeight, float s)
        {
            float y = offsetY;
            var items = Items;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                float height = Height(item, i, s);
                if (y + height >= 0f && y <= viewHeight)
                {
                    switch (item.Kind)
                    {
                        case Kind.Header:
                            TrainerUI.DrawSectionHeader(Loc.T(item.Key), new Rect(0f, y, width, height));
                            break;
                        case Kind.Row:
                            DrawRow(item, new Rect(0f, y, width, RowHeight * s), s);
                            break;
                        case Kind.About:
                            DrawAbout(new Rect(0f, y, width, AboutHeight * s), s);
                            break;
                    }
                }
                y += height;
            }
        }

        private static float Height(Item item, int index, float s)
        {
            switch (item.Kind)
            {
                case Kind.Header:
                    return (index == 0 ? 26f : HeaderHeight) * s;
                case Kind.About:
                    return (AboutHeight + RowGap) * s;
                default:
                    return (RowHeight + RowGap) * s;
            }
        }

        private static Item Header(string key) => new Item { Kind = Kind.Header, Key = key };

        private static Item Row(string key, float controlWidth, Action<Rect, float> control) =>
            new Item { Kind = Kind.Row, Key = key, Control = control, ControlWidth = controlWidth };

        private static Item[] BuildItems() => new[]
        {
            Header("set.ui"),
            Row("set.lang", 230f, (r, s) => Cycle(Settings.Language, r, s)),
            Row("set.scale", 400f, DrawScale),
            Row("set.accent", 250f, DrawAccents),
            Row("set.hud", 46f, (r, s) => SwitchRow(Settings.ShowHud, r, s)),
            Row("set.hudpos", 230f, (r, s) => Cycle(Settings.HudPos, r, s)),
            Row("set.toasts", 46f, (r, s) => SwitchRow(Settings.ShowToasts, r, s)),
            Row("set.pause", 46f, (r, s) => SwitchRow(Settings.PauseWhenOpen, r, s)),

            Header("set.keys"),
            Row("set.menukey", 150f, (r, s) => HotkeyRow(Settings.MenuKey, "set.menukey", r, s)),
            Row("set.panickey", 150f, (r, s) => HotkeyRow(Settings.PanicKey, "set.panickey", r, s)),
            Row("set.resetkeys", 150f, (r, s) =>
            {
                if (Widgets.Button(r, Loc.T("set.reset"), Widgets.ButtonKind.Secondary, true, s))
                {
                    Cheats.ResetHotkeys();
                    Toasts.Info(Loc.T("toast.keysreset"));
                }
            }),

            Header("set.behavior"),
            Row("set.remember", 46f, (r, s) => SwitchRow(Settings.RememberStates, r, s)),
            Row("set.achievements", 46f, (r, s) => SwitchRow(Settings.BlockAchievements, r, s)),
            Row("set.milestones", 46f, (r, s) => SwitchRow(Settings.ProtectMilestones, r, s)),

            Header("set.about"),
            new Item { Kind = Kind.About },
        };

        private static void DrawRow(Item item, Rect r, float s)
        {
            bool hover = Draw.Hover(r);
            Draw.Box(r, hover ? Theme.CardHover : Theme.Card, 11f * s);
            Draw.Outline(r, Theme.BorderSoft, 1f, 11f * s);
            float padding = 16f * s;
            float controlWidth = Mathf.Min(item.ControlWidth * s, r.width * 0.55f);
            var control = new Rect(r.xMax - padding - controlWidth, r.center.y - 14f * s, controlWidth, 28f * s);
            var text = new Rect(r.x + padding, r.y + 12f * s, Mathf.Max(0f, control.x - r.x - padding * 2f), 22f * s);
            Draw.Text(text, Loc.T(item.Key), Theme.Title, Theme.Text);
            Draw.Text(new Rect(text.x, text.yMax + 1f * s, text.width, 18f * s), Loc.T(item.Key + ".d"), Theme.Small, Theme.SubText);
            item.Control(control, s);
        }

        private static void SwitchRow(BoolOption option, Rect r, float s)
        {
            var rect = new Rect(r.xMax - 46f * s, r.center.y - 12f * s, 46f * s, 24f * s);
            if (Widgets.Switch(rect, option.Value, option, true, s))
                option.Set(!option.Value);
        }

        private static void Cycle<T>(EnumOption<T> option, Rect r, float s) where T : struct, Enum
        {
            int direction = Widgets.Stepper(r, option.Display, true, s);
            if (direction != 0)
                option.Cycle(direction);
        }

        private static void HotkeyRow(ConfigEntry<KeyboardShortcut> entry, string nameKey, Rect r, float s) =>
            Widgets.HotkeyChip(r, entry, Loc.T(nameKey), s);

        private static void DrawScale(Rect r, float s)
        {
            var auto = new Rect(r.x, r.y + 2f * s, 74f * s, 24f * s);
            if (Widgets.Chip(auto, Loc.T("set.scaleauto"), Settings.ScaleAuto.Value, true, s))
                Settings.ScaleAuto.Set(!Settings.ScaleAuto.Value);
            var slider = new Rect(auto.xMax + 12f * s, r.y, r.width - auto.width - 12f * s, r.height);
            Widgets.Slider(slider, Settings.Scale, !Settings.ScaleAuto.Value, s);
        }

        private static void DrawAccents(Rect r, float s)
        {
            float size = 24f * s;
            float gap = (r.width - size * Accents.Length) / Mathf.Max(1, Accents.Length - 1);
            for (int i = 0; i < Accents.Length; i++)
            {
                var swatch = new Rect(r.x + i * (size + gap), r.center.y - size / 2f, size, size);
                bool selected = Settings.Accent.Value == Accents[i];
                if (selected)
                    Draw.Outline(swatch.Shrink(-4f * s), Theme.AccentOf(Accents[i]), 2f, size);
                Draw.Box(swatch, Theme.AccentOf(Accents[i]), size / 2f);
                if (Draw.Hover(swatch) && !selected)
                    Draw.Outline(swatch.Shrink(-3f * s), Theme.Border, 1.5f, size);
                if (Draw.Clicked(swatch))
                    Settings.Accent.Set(Accents[i]);
            }
        }

        private static void DrawAbout(Rect r, float s)
        {
            Draw.Box(r, Theme.Card, 11f * s);
            Draw.Outline(r, Theme.BorderSoft, 1f, 11f * s);
            float padding = 16f * s;
            float labelWidth = Mathf.Min(190f * s, r.width * 0.4f);
            float y = r.y + 14f * s;
            string game;
            try
            {
                game = "Planetbase " + Definitions.VersionNumber + "  ·  Unity " + Application.unityVersion;
            }
            catch (Exception)
            {
                game = "Planetbase  ·  Unity " + Application.unityVersion;
            }
            bool allPatched = PatchManager.Failures.Count == 0;
            string patches = PatchManager.AppliedCount + " / " + PatchManager.TotalCount + "  ·  " +
                             (allPatched ? Loc.T("set.about.allok") : Loc.F("set.about.failed", string.Join(", ", PatchManager.Failures)));

            Line(ref y, r, padding, labelWidth, s, "set.about.version", MyPluginInfo.PLUGIN_NAME + " " + MyPluginInfo.PLUGIN_VERSION, Theme.Text);
            Line(ref y, r, padding, labelWidth, s, "set.about.game", game, Theme.Text);
            Line(ref y, r, padding, labelWidth, s, "set.about.patches", patches, allPatched ? Theme.Success : Theme.Danger);
            Line(ref y, r, padding, labelWidth, s, "set.about.config", Settings.Config.ConfigFilePath, Theme.SubText);
        }

        private static void Line(ref float y, Rect r, float padding, float labelWidth, float s, string labelKey, string value, Color color)
        {
            Draw.Text(new Rect(r.x + padding, y, labelWidth, 22f * s), Loc.T(labelKey), Theme.SmallBold, Theme.SubText);
            Draw.Text(new Rect(r.x + padding + labelWidth, y, r.width - labelWidth - padding * 2f, 22f * s), value, Theme.Small, color);
            y += 26f * s;
        }
    }
}
