using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace PlanetbaseTrainer.Core
{
    /// <summary>
    /// Hotkey matching and formatting. Shortcuts are stored as BepInEx <see cref="KeyboardShortcut"/> (standard TOML
    /// format, editable with ConfigurationManager), but matched leniently: Ctrl/Shift/Alt accept the left OR right key,
    /// and modifiers must match exactly so "Num 1" does not also fire on "Ctrl + Num 1".
    /// </summary>
    internal static class HotkeyUtil
    {
        /// <summary>Keys used by Planetbase's default controls (binding them shows a warning).</summary>
        private static readonly HashSet<KeyCode> GameKeys = new HashSet<KeyCode>
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Q, KeyCode.E, KeyCode.R, KeyCode.F,
            KeyCode.X, KeyCode.N, KeyCode.Tab, KeyCode.Delete, KeyCode.Backspace, KeyCode.Space, KeyCode.Escape,
            KeyCode.KeypadPlus, KeyCode.KeypadMinus, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        };

        private static KeyCode[] _captureKeys;

        public static bool CtrlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        public static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        public static bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);

        /// <summary>Every key that can be bound (mouse side buttons included, left/right/middle click excluded).</summary>
        public static KeyCode[] CaptureKeys
        {
            get
            {
                if (_captureKeys != null)
                    return _captureKeys;
                var keys = new List<KeyCode>();
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if (key == KeyCode.None || IsModifier(key) || key == KeyCode.Mouse0 || key == KeyCode.Mouse1 || key == KeyCode.Mouse2)
                        continue;
                    // Keep the generic joystick buttons only (Joystick1Button0... duplicates are skipped).
                    if (key >= KeyCode.Joystick1Button0)
                        continue;
                    if (!keys.Contains(key))
                        keys.Add(key);
                }
                _captureKeys = keys.ToArray();
                return _captureKeys;
            }
        }

        public static bool IsModifier(KeyCode key) =>
            key == KeyCode.LeftControl || key == KeyCode.RightControl ||
            key == KeyCode.LeftShift || key == KeyCode.RightShift ||
            key == KeyCode.LeftAlt || key == KeyCode.RightAlt || key == KeyCode.AltGr ||
            key == KeyCode.LeftCommand || key == KeyCode.RightCommand ||
            key == KeyCode.LeftWindows || key == KeyCode.RightWindows;

        public static bool IsEmpty(KeyboardShortcut shortcut) => shortcut.MainKey == KeyCode.None;

        /// <summary>True during the frame the shortcut is pressed.</summary>
        public static bool IsDown(KeyboardShortcut shortcut)
        {
            var main = shortcut.MainKey;
            if (main == KeyCode.None || !Input.GetKeyDown(main))
                return false;
            Decompose(shortcut, out bool ctrl, out bool shift, out bool alt);
            return ctrl == CtrlHeld && shift == ShiftHeld && alt == AltHeld;
        }

        /// <summary>Builds a shortcut from a main key and the modifiers currently held.</summary>
        public static KeyboardShortcut FromCurrentModifiers(KeyCode main)
        {
            var modifiers = new List<KeyCode>(3);
            if (CtrlHeld) modifiers.Add(KeyCode.LeftControl);
            if (ShiftHeld) modifiers.Add(KeyCode.LeftShift);
            if (AltHeld) modifiers.Add(KeyCode.LeftAlt);
            return new KeyboardShortcut(main, modifiers.ToArray());
        }

        public static bool SameCombination(KeyboardShortcut a, KeyboardShortcut b)
        {
            if (a.MainKey == KeyCode.None || a.MainKey != b.MainKey)
                return false;
            Decompose(a, out bool ac, out bool ash, out bool aa);
            Decompose(b, out bool bc, out bool bsh, out bool ba);
            return ac == bc && ash == bsh && aa == ba;
        }

        public static bool IsGameKey(KeyboardShortcut shortcut)
        {
            Decompose(shortcut, out bool ctrl, out bool shift, out bool alt);
            return !ctrl && !alt && GameKeys.Contains(shortcut.MainKey);
        }

        public static string Format(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return Loc.T("ui.nokey");
            Decompose(shortcut, out bool ctrl, out bool shift, out bool alt);
            var sb = new StringBuilder();
            if (ctrl) sb.Append("Ctrl + ");
            if (shift) sb.Append(Loc.T("key.shift")).Append(" + ");
            if (alt) sb.Append("Alt + ");
            sb.Append(KeyName(shortcut.MainKey));
            return sb.ToString();
        }

        private static void Decompose(KeyboardShortcut shortcut, out bool ctrl, out bool shift, out bool alt)
        {
            ctrl = shift = alt = false;
            foreach (var key in shortcut.Modifiers)
            {
                switch (key)
                {
                    case KeyCode.LeftControl:
                    case KeyCode.RightControl:
                        ctrl = true;
                        break;
                    case KeyCode.LeftShift:
                    case KeyCode.RightShift:
                        shift = true;
                        break;
                    case KeyCode.LeftAlt:
                    case KeyCode.RightAlt:
                    case KeyCode.AltGr:
                        alt = true;
                        break;
                }
            }
        }

        public static string KeyName(KeyCode key)
        {
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
                return "Num " + (key - KeyCode.Keypad0);
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return ((int)(key - KeyCode.Alpha0)).ToString();
            if (key >= KeyCode.F1 && key <= KeyCode.F15)
                return "F" + (key - KeyCode.F1 + 1);
            if (key >= KeyCode.JoystickButton0 && key <= KeyCode.JoystickButton19)
                return Loc.T("key.pad") + " " + (key - KeyCode.JoystickButton0);
            switch (key)
            {
                case KeyCode.KeypadPeriod: return "Num .";
                case KeyCode.KeypadDivide: return "Num /";
                case KeyCode.KeypadMultiply: return "Num *";
                case KeyCode.KeypadMinus: return "Num -";
                case KeyCode.KeypadPlus: return "Num +";
                case KeyCode.KeypadEnter: return "Num " + Loc.T("key.enter");
                case KeyCode.KeypadEquals: return "Num =";
                case KeyCode.Return: return Loc.T("key.enter");
                case KeyCode.Space: return Loc.T("key.space");
                case KeyCode.Escape: return Loc.T("key.escape");
                case KeyCode.Backspace: return Loc.T("key.backspace");
                case KeyCode.Delete: return Loc.T("key.delete");
                case KeyCode.Insert: return Loc.T("key.insert");
                case KeyCode.Home: return Loc.T("key.home");
                case KeyCode.End: return Loc.T("key.end");
                case KeyCode.PageUp: return Loc.T("key.pageup");
                case KeyCode.PageDown: return Loc.T("key.pagedown");
                case KeyCode.UpArrow: return Loc.T("key.up");
                case KeyCode.DownArrow: return Loc.T("key.down");
                case KeyCode.LeftArrow: return Loc.T("key.left");
                case KeyCode.RightArrow: return Loc.T("key.right");
                case KeyCode.Mouse3: return Loc.T("key.mouse") + " 4";
                case KeyCode.Mouse4: return Loc.T("key.mouse") + " 5";
                case KeyCode.Mouse5: return Loc.T("key.mouse") + " 6";
                case KeyCode.Mouse6: return Loc.T("key.mouse") + " 7";
                case KeyCode.Pause: return "Pause";
                case KeyCode.ScrollLock: return "Scroll Lock";
                case KeyCode.Print: return "Print";
                case KeyCode.BackQuote: return "²";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash: return "\\";
                default: return key.ToString();
            }
        }
    }
}
