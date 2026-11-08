using System.Collections.Generic;
using BepInEx.Configuration;
using PlanetbaseTrainer.Core;
using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>Reusable controls: switch, slider, selector, chips, buttons and hotkey chips.</summary>
    internal static class Widgets
    {
        private static readonly Dictionary<object, float> Animations = new Dictionary<object, float>();

        /// <summary>The slider currently being dragged (object identity, robust across frames).</summary>
        public static FloatOption DraggedSlider { get; private set; }

        public static void ReleaseDrag() => DraggedSlider = null;

        /// <summary>Smoothly animates a 0..1 value towards its target (advanced on repaint only).</summary>
        public static float Animate(object key, float target, float speed = 9f)
        {
            if (!Animations.TryGetValue(key, out float value))
                value = target;
            if (Draw.IsRepaint)
                value = Mathf.MoveTowards(value, target, Time.unscaledDeltaTime * speed);
            Animations[key] = value;
            return value;
        }

        public static bool Switch(Rect rect, bool on, object key, bool enabled, float scale)
        {
            float t = Animate(key, on ? 1f : 0f);
            var track = Color.Lerp(Theme.TrackOff, Theme.Accent, t);
            if (enabled && Draw.Hover(rect))
                track = Color.Lerp(track, Color.white, 0.08f);
            Draw.Box(rect, enabled ? track : Theme.Alpha(track, 0.35f), rect.height / 2f);
            float pad = 3f * scale;
            float knob = rect.height - pad * 2f;
            float x = Mathf.Lerp(rect.x + pad, rect.xMax - pad - knob, t);
            Draw.Box(new Rect(x, rect.y + pad, knob, knob), enabled ? Color.white : Theme.Alpha(Color.white, 0.4f), knob / 2f);
            return enabled && Draw.Clicked(rect);
        }

        public static void Slider(Rect rect, FloatOption option, bool enabled, float scale)
        {
            float valueWidth = 62f * scale;
            var track = new Rect(rect.x + 7f * scale, rect.center.y - 3f * scale, rect.width - valueWidth - 14f * scale, 6f * scale);
            float t = Mathf.InverseLerp(option.Min, option.Max, option.Value);
            var accent = enabled ? Theme.Accent : Theme.Muted;

            Draw.Box(track, Theme.TrackOff, 3f * scale);
            Draw.Box(new Rect(track.x, track.y, track.width * t, track.height), accent, 3f * scale);
            float knobSize = (DraggedSlider == option ? 16f : 14f) * scale;
            var knob = new Rect(track.x + track.width * t - knobSize / 2f, track.center.y - knobSize / 2f, knobSize, knobSize);
            Draw.Box(knob, enabled ? Color.white : Theme.SubText, knobSize / 2f);

            var valueRect = new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height);
            Draw.Box(new Rect(valueRect.x + 6f * scale, rect.center.y - 11f * scale, valueWidth - 6f * scale, 22f * scale), Theme.Field, 6f * scale);
            Draw.Text(valueRect, option.Display, Theme.SmallBoldCenter, enabled ? Theme.Text : Theme.Muted);

            var e = Event.current;
            var hitArea = new Rect(track.x - 8f * scale, rect.y, track.width + 16f * scale, rect.height);
            if (enabled && e.type == EventType.MouseDown && e.button == 0 && Draw.Hover(hitArea))
            {
                DraggedSlider = option;
                option.Set(Mathf.Lerp(option.Min, option.Max, Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x)));
                e.Use();
            }
            else if (DraggedSlider == option)
            {
                if (e.type == EventType.MouseDrag)
                {
                    option.Set(Mathf.Lerp(option.Min, option.Max, Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x)));
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    DraggedSlider = null;
                    e.Use();
                }
            }
        }

        /// <summary>‹ value › selector. Returns -1 / +1 when an arrow is clicked, 0 otherwise.</summary>
        public static int Stepper(Rect rect, string value, bool enabled, float scale)
        {
            Draw.Box(rect, Theme.Field, 8f * scale);
            float arrow = rect.height;
            var left = new Rect(rect.x, rect.y, arrow, rect.height);
            var right = new Rect(rect.xMax - arrow, rect.y, arrow, rect.height);
            int result = 0;
            if (StepperArrow(left, Icons.ChevronLeft, enabled, scale)) result = -1;
            if (StepperArrow(right, Icons.ChevronRight, enabled, scale)) result = 1;
            Draw.Text(new Rect(left.xMax, rect.y, rect.width - arrow * 2f, rect.height), value, Theme.SmallBoldCenter,
                enabled ? Theme.Text : Theme.Muted);
            // Clicking the value itself cycles forward, like most game menus.
            if (result == 0 && enabled && Draw.Clicked(new Rect(left.xMax, rect.y, rect.width - arrow * 2f, rect.height)))
                result = 1;
            return result;
        }

        private static bool StepperArrow(Rect rect, Texture2D chevron, bool enabled, float scale)
        {
            bool hover = enabled && Draw.Hover(rect);
            if (hover)
                Draw.Box(rect.Shrink(3f * scale), Theme.CardHover, 6f * scale);
            float size = 13f * scale;
            var icon = new Rect(rect.center.x - size / 2f, rect.center.y - size / 2f, size, size);
            Draw.Icon(chevron, icon, !enabled ? Theme.Muted : hover ? Theme.Accent : Theme.SubText);
            return enabled && Draw.Clicked(rect);
        }

        public static float ChipWidth(string label, float scale) => Draw.Measure(label, Theme.SmallBold).x + 26f * scale;

        public static bool Chip(Rect rect, string label, bool on, bool enabled, float scale)
        {
            var accent = Theme.Accent;
            bool hover = enabled && Draw.Hover(rect);
            if (on)
            {
                Draw.Box(rect, Theme.Alpha(accent, enabled ? 0.18f : 0.08f), rect.height / 2f);
                Draw.Outline(rect, Theme.Alpha(accent, enabled ? 0.75f : 0.3f), 1f, rect.height / 2f);
            }
            else
            {
                Draw.Box(rect, hover ? Theme.CardHover : Theme.Field, rect.height / 2f);
                Draw.Outline(rect, Theme.BorderSoft, 1f, rect.height / 2f);
            }
            var dot = new Rect(rect.x + 9f * scale, rect.center.y - 3f * scale, 6f * scale, 6f * scale);
            Draw.Box(dot, on ? (enabled ? accent : Theme.Muted) : Theme.Muted, 3f * scale);
            Draw.Text(new Rect(dot.xMax + 5f * scale, rect.y, rect.width - 20f * scale, rect.height), label, Theme.SmallBold,
                !enabled ? Theme.Muted : on ? Theme.Text : Theme.SubText);
            return enabled && Draw.Clicked(rect);
        }

        public enum ButtonKind { Primary, Secondary, Danger, Ghost }

        public static bool Button(Rect rect, string label, ButtonKind kind, bool enabled, float scale)
        {
            bool hover = enabled && Draw.Hover(rect);
            Color background, foreground;
            switch (kind)
            {
                case ButtonKind.Primary:
                    background = enabled ? Theme.Accent : Theme.TrackOff;
                    foreground = enabled ? Theme.OnAccent : Theme.Muted;
                    if (hover) background = Color.Lerp(background, Color.white, 0.15f);
                    break;
                case ButtonKind.Danger:
                    background = enabled ? Theme.Alpha(Theme.Danger, hover ? 0.32f : 0.2f) : Theme.TrackOff;
                    foreground = enabled ? Theme.Danger : Theme.Muted;
                    break;
                case ButtonKind.Ghost:
                    background = hover ? Theme.CardHover : new Color(0f, 0f, 0f, 0f);
                    foreground = enabled ? (hover ? Theme.Text : Theme.SubText) : Theme.Muted;
                    break;
                default:
                    background = hover ? Theme.Border : Theme.CardHover;
                    foreground = enabled ? Theme.Text : Theme.Muted;
                    break;
            }
            Draw.Box(rect, background, 8f * scale);
            if (kind == ButtonKind.Danger && enabled)
                Draw.Outline(rect, Theme.Alpha(Theme.Danger, 0.6f), 1f, 8f * scale);
            Draw.Text(rect, label, Theme.SmallBoldCenter, foreground);
            return enabled && Draw.Clicked(rect);
        }

        /// <summary>Shows a hotkey; left click starts rebinding, right click clears it.</summary>
        public static void HotkeyChip(Rect rect, ConfigEntry<KeyboardShortcut> entry, string ownerName, float scale)
        {
            bool capturing = TrainerUI.CaptureTarget == entry;
            bool empty = HotkeyUtil.IsEmpty(entry.Value);
            bool hover = Draw.Hover(rect);
            var accent = Theme.Accent;

            if (capturing)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
                Draw.Box(rect, Theme.Alpha(accent, 0.16f), 7f * scale);
                Draw.Outline(rect, Theme.Alpha(accent, pulse), 1.5f, 7f * scale);
            }
            else
            {
                Draw.Box(rect, hover ? Theme.CardHover : Theme.Field, 7f * scale);
                Draw.Outline(rect, hover ? Theme.Border : Theme.BorderSoft, 1f, 7f * scale);
            }
            string label = capturing ? Loc.T("ui.presskey") : HotkeyUtil.Format(entry.Value);
            var color = capturing ? accent : empty ? Theme.Muted : Theme.Text;
            Draw.Text(rect.Shrink(6f * scale, 0f), label, Theme.SmallBoldCenter, color);

            if (Draw.Clicked(rect, 0))
                TrainerUI.BeginCapture(entry, ownerName);
            else if (Draw.Clicked(rect, 1))
                TrainerUI.ClearHotkey(entry, ownerName);
        }

        public static Rect Shrink(this Rect rect, float amount) => Shrink(rect, amount, amount);

        public static Rect Shrink(this Rect rect, float horizontal, float vertical) =>
            new Rect(rect.x + horizontal, rect.y + vertical, rect.width - horizontal * 2f, rect.height - vertical * 2f);
    }
}
