using System.Collections.Generic;
using PlanetbaseTrainer.Core;
using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>Short on-screen notifications (hotkey feedback, action results).</summary>
    internal static class Toasts
    {
        private const int MaxVisible = 5;

        private sealed class Toast
        {
            public string Text;
            public Color Color;
            public float Start;
            public float Duration;
        }

        private static readonly List<Toast> Items = new List<Toast>();

        public static void Info(string text, float duration = 2.8f) => Show(text, Theme.Accent, duration);

        public static void Success(string text) => Show(text, Theme.Success, 2.6f);

        public static void Warn(string text) => Show(text, Theme.Warning, 3.4f);

        public static void Muted(string text) => Show(text, Theme.Muted, 2.2f);

        private static void Show(string text, Color color, float duration)
        {
            if (string.IsNullOrEmpty(text) || Settings.ShowToasts == null || !Settings.ShowToasts.Value)
                return;
            // Repeated message (e.g. hammering a hotkey): refresh it instead of stacking duplicates.
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Text != text)
                    continue;
                Items[i].Start = Time.unscaledTime;
                Items[i].Color = color;
                return;
            }
            Items.Add(new Toast { Text = text, Color = color, Start = Time.unscaledTime, Duration = duration });
            while (Items.Count > MaxVisible)
                Items.RemoveAt(0);
        }

        /// <param name="scale">UI scale.</param>
        /// <param name="avoid">The open menu window, if any: the toasts are stacked above it (or below it when there is
        /// no room above) instead of covering its title bar and search box.</param>
        public static void Draw(float scale, Rect? avoid = null)
        {
            float now = Time.unscaledTime;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (now - Items[i].Start > Items[i].Duration)
                    Items.RemoveAt(i);
            }
            if (Items.Count == 0)
                return;

            float height = 38f * scale;
            float gap = 8f * scale;
            float y = Screen.height * 0.16f;
            if (avoid.HasValue)
            {
                var window = avoid.Value;
                float margin = 12f * scale;
                float stack = Items.Count * height + (Items.Count - 1) * gap;
                if (window.yMin - margin - stack < 4f && window.yMax + margin + stack <= Screen.height - 4f)
                    y = window.yMax + margin;
                else
                    y = Mathf.Max(4f, window.yMin - margin - stack);
            }
            for (int i = 0; i < Items.Count; i++)
            {
                var toast = Items[i];
                float age = now - toast.Start;
                float alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((toast.Duration - age) / 0.4f);
                float slide = (1f - Mathf.Clamp01(age / 0.2f)) * 10f * scale;

                float width = Mathf.Min(UI.Draw.Measure(toast.Text, Theme.BodyBold).x + 44f * scale, Screen.width * 0.8f);
                var rect = new Rect((Screen.width - width) / 2f, y - slide, width, height);
                UI.Draw.Box(new Rect(rect.x, rect.y + 3f * scale, rect.width, rect.height), Theme.Alpha(Theme.Shadow, alpha), 10f * scale);
                UI.Draw.Box(rect, Theme.Alpha(Theme.Panel, 0.96f * alpha), 10f * scale);
                UI.Draw.Outline(rect, Theme.Alpha(Theme.Border, alpha), 1f, 10f * scale);
                UI.Draw.Box(new Rect(rect.x + 12f * scale, rect.center.y - 4f * scale, 8f * scale, 8f * scale),
                    Theme.Alpha(toast.Color, alpha), 4f * scale);
                UI.Draw.Text(new Rect(rect.x + 28f * scale, rect.y, rect.width - 36f * scale, rect.height), toast.Text,
                    Theme.BodyBold, Theme.Alpha(Theme.Text, alpha));
                y += height + gap;
            }
        }
    }
}
