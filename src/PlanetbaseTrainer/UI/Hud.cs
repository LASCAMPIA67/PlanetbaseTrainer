using System.Collections.Generic;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Game;
using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>
    /// Compact, non-interactive list of the active cheats. It stays clear of Planetbase's own HUD (colony panel top-left,
    /// resource panel top-right, hints bottom-left, command bar bottom-center) and collapses into a "+N" chip when full.
    /// </summary>
    internal static class Hud
    {
        private const int MaxRows = 2;

        private static readonly List<ToggleCheat> Active = new List<ToggleCheat>();
        private static readonly List<float> Widths = new List<float>();
        private static readonly List<int> RowStarts = new List<int>();
        private static readonly List<float> RowWidths = new List<float>();

        public static void Draw(float scale)
        {
            if (!Settings.ShowHud.Value || !G.InGame)
                return;
            Active.Clear();
            foreach (var toggle in Cheats.Toggles)
            {
                if (toggle.On)
                    Active.Add(toggle);
            }
            if (Active.Count == 0)
                return;

            float height = 22f * scale;
            float gap = 5f * scale;
            float margin = 8f * scale;
            // The game's top panels are about 0.45 × screen height wide each (they scale with the resolution).
            float gamePanel = Screen.height * 0.45f;
            var position = Settings.HudPos.Value;
            float maxWidth = position == HudPosition.TopCenter
                ? Mathf.Max(260f * scale, Screen.width - gamePanel * 2f - margin * 2f)
                : Mathf.Max(260f * scale, Screen.width * 0.32f);

            Widths.Clear();
            foreach (var toggle in Active)
                Widths.Add(UI.Draw.Measure(toggle.Name, Theme.Tiny).x + 24f * scale);

            // Greedy layout into at most MaxRows rows; whatever does not fit is summarized by a "+N" chip.
            RowStarts.Clear();
            RowWidths.Clear();
            int placed = 0;
            for (int row = 0; row < MaxRows && placed < Active.Count; row++)
            {
                RowStarts.Add(placed);
                float width = 0f;
                while (placed < Active.Count)
                {
                    float next = width + (width > 0f ? gap : 0f) + Widths[placed];
                    if (width > 0f && next > maxWidth)
                        break;
                    width = next;
                    placed++;
                }
                RowWidths.Add(width);
            }
            int overflow = Active.Count - placed;
            string overflowLabel = null;
            float overflowWidth = 0f;
            if (overflow > 0)
            {
                // Make room for the "+N" chip at the end of the last row.
                int last = RowWidths.Count - 1;
                while (true)
                {
                    overflowLabel = "+" + overflow;
                    overflowWidth = UI.Draw.Measure(overflowLabel, Theme.TinyBold).x + 18f * scale;
                    if (RowWidths[last] + gap + overflowWidth <= maxWidth || placed - 1 <= RowStarts[last])
                        break;
                    placed--;
                    overflow++;
                    RowWidths[last] -= Widths[placed] + gap;
                }
                RowWidths[last] += gap + overflowWidth;
            }

            float totalHeight = RowWidths.Count * height + (RowWidths.Count - 1) * gap;
            bool bottom = position == HudPosition.BottomLeft || position == HudPosition.BottomRight;
            float y;
            if (position == HudPosition.TopCenter)
                y = margin;
            else if (bottom)
                y = Screen.height - Screen.height * 0.12f - totalHeight; // above the hints box / command bar
            else
                y = Screen.height * 0.12f; // below the game's top panels

            var accent = Theme.Accent;
            for (int row = 0; row < RowWidths.Count; row++)
            {
                float rowWidth = RowWidths[row];
                float x;
                switch (position)
                {
                    case HudPosition.TopLeft:
                    case HudPosition.BottomLeft:
                        x = margin;
                        break;
                    case HudPosition.TopRight:
                    case HudPosition.BottomRight:
                        x = Screen.width - margin - rowWidth;
                        break;
                    default:
                        x = (Screen.width - rowWidth) / 2f;
                        break;
                }
                int end = row + 1 < RowStarts.Count ? RowStarts[row + 1] : placed;
                for (int i = RowStarts[row]; i < end; i++)
                {
                    var rect = new Rect(x, y, Widths[i], height);
                    UI.Draw.Box(rect, new Color(0.04f, 0.05f, 0.07f, 0.8f), height / 2f);
                    UI.Draw.Outline(rect, Theme.Alpha(accent, 0.45f), 1f, height / 2f);
                    UI.Draw.Box(new Rect(rect.x + 8f * scale, rect.center.y - 3f * scale, 6f * scale, 6f * scale), accent, 3f * scale);
                    UI.Draw.Text(new Rect(rect.x + 18f * scale, rect.y, Widths[i] - 22f * scale, height), Active[i].Name, Theme.Tiny, Theme.Text);
                    x += Widths[i] + gap;
                }
                if (row == RowWidths.Count - 1 && overflowLabel != null)
                {
                    var rect = new Rect(x, y, overflowWidth, height);
                    UI.Draw.Box(rect, Theme.Alpha(accent, 0.85f), height / 2f);
                    UI.Draw.Text(rect, overflowLabel, Theme.TinyBold, Theme.OnAccent);
                }
                y += height + gap;
            }
        }
    }
}
