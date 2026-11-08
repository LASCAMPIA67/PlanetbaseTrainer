using System;
using System.Collections.Generic;
using PlanetbaseTrainer.Core;
using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>Colors, fonts and text styles. Styles are rebuilt only when the UI scale changes.</summary>
    internal static class Theme
    {
        public static readonly Color Background = Rgb(13, 16, 22, 0.97f);
        public static readonly Color Panel = Rgb(17, 21, 29);
        public static readonly Color Card = Rgb(23, 28, 38);
        public static readonly Color CardHover = Rgb(28, 34, 46);
        public static readonly Color Field = Rgb(10, 13, 18);
        public static readonly Color Border = Rgb(41, 49, 64);
        public static readonly Color BorderSoft = Rgb(31, 38, 51);
        public static readonly Color Text = Rgb(232, 236, 243);
        public static readonly Color SubText = Rgb(150, 160, 178);
        public static readonly Color Muted = Rgb(98, 108, 126);
        public static readonly Color TrackOff = Rgb(50, 58, 75);
        public static readonly Color Danger = Rgb(248, 113, 113);
        public static readonly Color Success = Rgb(74, 222, 128);
        public static readonly Color Warning = Rgb(251, 191, 36);
        public static readonly Color Star = Rgb(250, 204, 21);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);

        private static float _builtScale = -1f;
        private static Font _regular;
        private static Font _semibold;

        public static Color Accent => AccentOf(Settings.Accent.Value);

        /// <summary>Dark text drawn on accent-colored surfaces.</summary>
        public static Color OnAccent => Rgb(8, 12, 18);

        public static GUIStyle Brand { get; private set; }
        public static GUIStyle Title { get; private set; }
        public static GUIStyle Body { get; private set; }
        public static GUIStyle BodyBold { get; private set; }
        public static GUIStyle Small { get; private set; }
        public static GUIStyle SmallBold { get; private set; }
        public static GUIStyle SmallCenter { get; private set; }
        public static GUIStyle SmallBoldCenter { get; private set; }
        public static GUIStyle Tiny { get; private set; }
        public static GUIStyle TinyBold { get; private set; }
        public static GUIStyle Section { get; private set; }
        public static GUIStyle Description { get; private set; }
        public static GUIStyle Input { get; private set; }

        public static Color AccentOf(AccentColor accent)
        {
            switch (accent)
            {
                case AccentColor.Violet: return Rgb(167, 139, 250);
                case AccentColor.Emerald: return Rgb(52, 211, 153);
                case AccentColor.Amber: return Rgb(251, 191, 36);
                case AccentColor.Rose: return Rgb(251, 113, 133);
                case AccentColor.Orange: return Rgb(251, 146, 60);
                default: return Rgb(34, 211, 238);
            }
        }

        public static Color Alpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        /// <summary>Must be called from OnGUI.</summary>
        public static void Ensure(float scale)
        {
            if (Body != null && Mathf.Approximately(scale, _builtScale))
                return;
            _builtScale = scale;
            EnsureFonts();
            int Px(float size) => Mathf.Max(8, Mathf.RoundToInt(size * scale));

            Brand = Make(_semibold, Px(17), FontStyle.Normal, TextAnchor.MiddleLeft);
            Title = Make(_semibold, Px(14.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
            Body = Make(_regular, Px(13.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
            BodyBold = Make(_semibold, Px(13.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
            Small = Make(_regular, Px(12.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
            SmallBold = Make(_semibold, Px(12.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
            SmallCenter = Make(_regular, Px(12.5f), FontStyle.Normal, TextAnchor.MiddleCenter);
            SmallBoldCenter = Make(_semibold, Px(12.5f), FontStyle.Normal, TextAnchor.MiddleCenter);
            Tiny = Make(_regular, Px(11f), FontStyle.Normal, TextAnchor.MiddleLeft);
            TinyBold = Make(_semibold, Px(10.5f), FontStyle.Normal, TextAnchor.MiddleCenter);
            Section = Make(_semibold, Px(11.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
            Description = Make(_regular, Px(12.5f), FontStyle.Normal, TextAnchor.UpperLeft);
            Description.wordWrap = true;

            // A background-less text field: the rounded box is drawn by the widget itself.
            Input = Make(_regular, Px(13.5f), FontStyle.Normal, TextAnchor.MiddleLeft);
            Input.focused.textColor = Color.white;
        }

        private static GUIStyle Make(Font font, int size, FontStyle style, TextAnchor anchor)
        {
            var guiStyle = new GUIStyle
            {
                font = font,
                fontSize = size,
                fontStyle = style,
                alignment = anchor,
                wordWrap = false,
                clipping = TextClipping.Clip,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            // Text is tinted through GUI.contentColor, so the style itself stays white.
            guiStyle.normal.textColor = Color.white;
            guiStyle.hover.textColor = Color.white;
            guiStyle.active.textColor = Color.white;
            guiStyle.focused.textColor = Color.white;
            return guiStyle;
        }

        private static void EnsureFonts()
        {
            if (_regular != null)
                return;
            try
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                _regular = CreateFont(installed, "Segoe UI", "Segoe UI Symbol", "Arial");
                _semibold = CreateFont(installed, "Segoe UI Semibold", "Segoe UI", "Segoe UI Symbol", "Arial");
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("theme.fonts", ex);
            }
            if (_regular == null)
                _regular = GUI.skin.font;
            if (_semibold == null)
                _semibold = _regular;
        }

        private static Font CreateFont(HashSet<string> installed, params string[] names)
        {
            // Unity uses the first installed name and falls back to the next ones for missing glyphs.
            var available = new List<string>();
            foreach (var name in names)
            {
                if (installed.Contains(name))
                    available.Add(name);
            }
            if (available.Count == 0)
                return null;
            var font = Font.CreateDynamicFontFromOSFont(available.ToArray(), 16);
            if (font != null)
                font.hideFlags = HideFlags.HideAndDontSave;
            return font;
        }

        private static Color Rgb(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
    }
}
