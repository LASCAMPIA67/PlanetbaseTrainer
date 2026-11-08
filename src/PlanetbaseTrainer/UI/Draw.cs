using System.Collections.Generic;
using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>
    /// Immediate-mode drawing and hit-testing primitives. Rounded shapes use GUI.DrawTexture's border-radius overload
    /// (no custom textures). Hit tests honour the current clip rect so that controls scrolled out of view are inert.
    /// </summary>
    internal static class Draw
    {
        private static readonly GUIContent TempContent = new GUIContent();
        private static readonly Stack<Rect> ClipStack = new Stack<Rect>();
        private static readonly Rect NoClip = new Rect(-100000f, -100000f, 200000f, 200000f);
        private static Rect _clip = NoClip;

        /// <summary>When false, every hit test fails (e.g. while capturing a hotkey).</summary>
        public static bool Interactive { get; set; } = true;

        public static bool IsRepaint => Event.current.type == EventType.Repaint;

        public static Vector2 Mouse => Event.current.mousePosition;

        public static void PushClip(Rect clip)
        {
            ClipStack.Push(_clip);
            _clip = clip;
        }

        public static void PopClip()
        {
            _clip = ClipStack.Count > 0 ? ClipStack.Pop() : NoClip;
        }

        public static void ResetClip()
        {
            ClipStack.Clear();
            _clip = NoClip;
        }

        public static void Box(Rect rect, Color color, float radius = 0f)
        {
            if (!IsRepaint || color.a <= 0f)
                return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        /// <summary>Rounded rectangle with per-corner radii (top-left, top-right, bottom-right, bottom-left).</summary>
        public static void Box(Rect rect, Color color, Vector4 radii)
        {
            if (!IsRepaint || color.a <= 0f)
                return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, Vector4.zero, radii);
        }

        public static void Outline(Rect rect, Color color, float width, float radius = 0f)
        {
            if (!IsRepaint || color.a <= 0f)
                return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);
        }

        public static void Text(Rect rect, string text, GUIStyle style, Color color)
        {
            if (!IsRepaint || string.IsNullOrEmpty(text))
                return;
            var previous = GUI.contentColor;
            GUI.contentColor = color;
            TempContent.text = text;
            style.Draw(rect, TempContent, false, false, false, false);
            GUI.contentColor = previous;
        }

        public static void Icon(Texture2D texture, Rect rect, Color color)
        {
            if (!IsRepaint || texture == null)
                return;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true, 0f, color, 0f, 0f);
        }

        public static Vector2 Measure(string text, GUIStyle style)
        {
            TempContent.text = text ?? string.Empty;
            return style.CalcSize(TempContent);
        }

        public static float MeasureHeight(string text, GUIStyle style, float width)
        {
            TempContent.text = text ?? string.Empty;
            return style.CalcHeight(TempContent, width);
        }

        public static bool Hover(Rect rect)
        {
            var mouse = Event.current.mousePosition;
            return Interactive && rect.Contains(mouse) && _clip.Contains(mouse);
        }

        /// <summary>Click detection on mouse-down (robust with scrolling and culling, unlike control IDs).</summary>
        public static bool Clicked(Rect rect, int button = 0)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != button || !Hover(rect))
                return false;
            e.Use();
            return true;
        }
    }
}
