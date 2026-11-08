using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using Planetbase;
using PlanetbaseTrainer.Core;
using PlanetbaseTrainer.Features;
using PlanetbaseTrainer.Game;
using PlanetbaseTrainer.Patches;
using UnityEngine;

namespace PlanetbaseTrainer.UI
{
    /// <summary>The trainer window (IMGUI, drawn above the game's own GUI).</summary>
    internal static class TrainerUI
    {
        private const string SearchControl = "pbtrainer.search";
        private const float BaseMinWidth = 780f;
        private const float BaseMinHeight = 560f;

        private static readonly Tab[] TabOrder =
        {
            Tab.Favorites, Tab.Colonists, Tab.Construction, Tab.Resources, Tab.Base,
            Tab.Trade, Tab.Threats, Tab.Time, Tab.Progression, Tab.Settings,
        };

        private enum DragMode { None, Window, Resize, Scrollbar }

        private sealed class Row
        {
            public string Header;
            public Cheat Cheat;
            public float Height;
        }

        private static readonly List<Row> Rows = new List<Row>();
        private static readonly List<Row> RowPool = new List<Row>();
        private static readonly List<Rect> ChipRects = new List<Rect>();
        private static readonly Dictionary<string, Vector3> DescriptionHeights = new Dictionary<string, Vector3>();
        private static readonly Dictionary<Cheat, string> SearchIndex = new Dictionary<Cheat, string>();
        private static int _searchIndexVersion = -1;

        private static float _scale = 1f;
        private static float _layoutScale = -1f;
        private static Vector2 _layoutScreen;
        private static Rect _window;
        private static Tab _tab = Tab.Colonists;
        private static string _search = string.Empty;
        private static bool _searchFocused;
        private static bool _dropFocus;
        private static float _scroll;
        private static float _contentHeight;
        private static DragMode _drag;
        private static Vector2 _dragOffset;
        private static float _dragStartScroll;
        private static float _dragStartMouse;
        private static bool _suppressInput;
        private static int _releasedAtFrame = -1;
        private static bool _pausedByMenu;
        private static string _captureOwner;
        private static float _captureStarted;

        public static bool Visible { get; private set; }

        public static ConfigEntry<KeyboardShortcut> CaptureTarget { get; private set; }

        public static bool IsCapturing => CaptureTarget != null;

        /// <summary>The game must ignore the keyboard (typing in the search box, binding a key, or right after).</summary>
        public static bool WantsKeyboard => (Visible && (_searchFocused || IsCapturing)) || _suppressInput;

        /// <summary>Cheat hotkeys must not fire (text input anywhere, hotkey capture...).</summary>
        public static bool BlocksHotkeys =>
            IsCapturing || _suppressInput || (Visible && _searchFocused) || GUIUtility.keyboardControl != 0;

        /// <summary>The mouse is over the trainer window (or dragging one of its controls).</summary>
        public static bool IsMouseOverUI
        {
            get
            {
                if (!Visible)
                    return false;
                if (_drag != DragMode.None || Widgets.DraggedSlider != null)
                    return true;
                var mouse = Input.mousePosition;
                return _window.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            }
        }

        public static void Init()
        {
            int lastTab = Settings.LastTab.Value;
            if (lastTab >= 0 && lastTab < TabOrder.Length)
                _tab = (Tab)lastTab;
        }

        /// <summary>Programmatic navigation (used by the self-test).</summary>
        public static void Show(Tab tab, string search = "")
        {
            _tab = tab;
            _search = search ?? string.Empty;
            _scroll = 0f;
            SetVisible(true);
        }

        // ================================================================== Update (input polling)

        public static void Update()
        {
            UpdateInputSuppression();
            if (_drag != DragMode.None && !Input.GetMouseButton(0))
                EndDrag();
            if (Widgets.DraggedSlider != null && !Input.GetMouseButton(0))
                Widgets.ReleaseDrag();

            if (IsCapturing)
            {
                PollCapture();
                return;
            }
            if (HotkeyUtil.IsDown(Settings.MenuKey.Value))
                SetVisible(!Visible);
        }

        /// <summary>
        /// After a key is consumed by the trainer (Escape, a rebound key...), the game keeps ignoring the keyboard until
        /// every key has been released for a full frame — otherwise it would react to the key-up (e.g. open its menu).
        /// </summary>
        private static void UpdateInputSuppression()
        {
            if (!_suppressInput)
                return;
            if (Input.anyKey)
                _releasedAtFrame = -1;
            else if (_releasedAtFrame < 0)
                _releasedAtFrame = Time.frameCount;
            else if (Time.frameCount > _releasedAtFrame + 1)
                _suppressInput = false;
        }

        private static void SuppressInputUntilRelease()
        {
            _suppressInput = true;
            _releasedAtFrame = -1;
        }

        public static void SetVisible(bool visible)
        {
            if (Visible == visible)
                return;
            Visible = visible;
            if (!visible)
            {
                if (IsCapturing)
                    EndCapture();
                _dropFocus = true;
                _searchFocused = false;
                if (_drag != DragMode.None)
                    EndDrag();
                Widgets.ReleaseDrag();
            }
            ApplyPause(visible);
        }

        private static void ApplyPause(bool visible)
        {
            var time = Singleton<TimeManager>.mInstance;
            if (visible)
            {
                if (Settings.PauseWhenOpen.Value && G.InGame && time != null && !time.isPaused())
                {
                    time.pause();
                    _pausedByMenu = true;
                }
            }
            else if (_pausedByMenu)
            {
                _pausedByMenu = false;
                if (time != null && G.InGame && time.isPaused())
                    time.unpause();
            }
        }

        // ================================================================== Hotkey capture

        public static void BeginCapture(ConfigEntry<KeyboardShortcut> entry, string ownerName)
        {
            CaptureTarget = entry;
            _captureOwner = ownerName;
            _captureStarted = Time.unscaledTime;
            _dropFocus = true;
        }

        private static void EndCapture()
        {
            CaptureTarget = null;
            _captureOwner = null;
            SuppressInputUntilRelease();
        }

        public static void ClearHotkey(ConfigEntry<KeyboardShortcut> entry, string ownerName)
        {
            entry.Value = KeyboardShortcut.Empty;
            Settings.MarkDirty();
            Toasts.Muted(Loc.F("toast.keycleared", ownerName));
        }

        private static void PollCapture()
        {
            if (Time.unscaledTime - _captureStarted < 0.1f)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EndCapture();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
            {
                var entry = CaptureTarget;
                string owner = _captureOwner;
                EndCapture();
                ClearHotkey(entry, owner);
                return;
            }
            var keys = HotkeyUtil.CaptureKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!Input.GetKeyDown(keys[i]))
                    continue;
                Assign(HotkeyUtil.FromCurrentModifiers(keys[i]));
                return;
            }
        }

        private static void Assign(KeyboardShortcut shortcut)
        {
            var target = CaptureTarget;
            string owner = _captureOwner;
            EndCapture();
            string label = HotkeyUtil.Format(shortcut);

            // One combination = one action: remove it from whoever used it before.
            ReleaseConflict(Settings.MenuKey, Loc.T("set.menukey"), target, shortcut, label);
            ReleaseConflict(Settings.PanicKey, Loc.T("set.panickey"), target, shortcut, label);
            foreach (var cheat in Cheats.All)
                ReleaseConflict(cheat.Hotkey, cheat.Name, target, shortcut, label);

            target.Value = shortcut;
            Settings.MarkDirty();
            Toasts.Info(Loc.F("toast.keyset", owner, label));
            if (HotkeyUtil.IsGameKey(shortcut))
                Toasts.Warn(Loc.F("toast.keygame", label));
        }

        private static void ReleaseConflict(ConfigEntry<KeyboardShortcut> entry, string name, ConfigEntry<KeyboardShortcut> target,
            KeyboardShortcut shortcut, string label)
        {
            if (entry == target || !HotkeyUtil.SameCombination(entry.Value, shortcut))
                return;
            entry.Value = KeyboardShortcut.Empty;
            Toasts.Warn(Loc.F("toast.keyconflict", label, name));
        }

        // ================================================================== OnGUI

        public static void OnGUI()
        {
            var e = Event.current;
            if (e == null)
                return;
            GUI.depth = -1000; // above the game's GUI, so the trainer also receives input first
            var previousColor = GUI.color;
            var previousContent = GUI.contentColor;
            var previousBackground = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
            try
            {
                _scale = ComputeScale();
                Theme.Ensure(_scale);
                Draw.ResetClip();
                Draw.Interactive = true;
                if (_dropFocus)
                {
                    _dropFocus = false;
                    _searchFocused = false;
                    GUI.FocusControl(null);
                    GUIUtility.keyboardControl = 0;
                }

                Hud.Draw(_scale);
                if (Visible)
                    DrawWindow(e);
                Toasts.Draw(_scale, Visible ? _window : (Rect?)null);
            }
            catch (Exception ex) when (!(ex is ExitGUIException))
            {
                Log.ErrorOnce("ui.ongui", ex);
            }
            finally
            {
                Draw.Interactive = true;
                GUI.color = previousColor;
                GUI.contentColor = previousContent;
                GUI.backgroundColor = previousBackground;
            }
        }

        private static float ComputeScale()
        {
            // While the "menu size" slider is dragged, keep the current scale: resizing the window under the cursor
            // would move the slider itself and make the value oscillate. The new size applies on release.
            if (Widgets.DraggedSlider == Settings.Scale && _layoutScale > 0f)
                return _scale;
            float scale = Settings.ScaleAuto.Value ? Mathf.Clamp(Screen.height / 1080f, 0.85f, 2.5f) : Settings.Scale.Value;
            return Mathf.Round(scale * 20f) / 20f;
        }

        private static void EnsureWindow()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            if (Mathf.Approximately(_layoutScale, _scale) && _layoutScreen == screen)
                return;
            _layoutScale = _scale;
            _layoutScreen = screen;
            float width = Mathf.Max(Settings.WinW.Value, BaseMinWidth) * _scale;
            float height = Mathf.Max(Settings.WinH.Value, BaseMinHeight) * _scale;
            float x = Settings.WinX.Value;
            float y = Settings.WinY.Value;
            if (x < 0f || y < 0f)
            {
                x = (Screen.width - width) / 2f;
                y = (Screen.height - height) / 2f;
            }
            _window = new Rect(x, y, width, height);
            ClampWindow();
        }

        private static void ClampWindow()
        {
            _window.width = Mathf.Min(_window.width, Screen.width - 8f);
            _window.height = Mathf.Min(_window.height, Screen.height - 8f);
            _window.x = Mathf.Clamp(_window.x, 4f, Mathf.Max(4f, Screen.width - _window.width - 4f));
            _window.y = Mathf.Clamp(_window.y, 4f, Mathf.Max(4f, Screen.height - _window.height - 4f));
        }

        private static void DrawWindow(Event e)
        {
            EnsureWindow();
            float s = _scale;
            var w = _window;

            // While waiting for a key, any click cancels the capture (and does nothing else).
            if (IsCapturing && e.type == EventType.MouseDown)
            {
                EndCapture();
                e.Use();
            }
            Draw.Interactive = !IsCapturing;

            Draw.Box(new Rect(w.x - 3f * s, w.y + 2f * s, w.width + 6f * s, w.height + 10f * s), Theme.Shadow, 18f * s);
            Draw.Box(w, Theme.Background, 14f * s);

            float headerHeight = 62f * s;
            float footerHeight = 42f * s;
            float sidebarWidth = 210f * s;
            var header = new Rect(w.x, w.y, w.width, headerHeight);
            var sidebar = new Rect(w.x, w.y + headerHeight, sidebarWidth, w.height - headerHeight - footerHeight);
            var content = new Rect(w.x + sidebarWidth, w.y + headerHeight, w.width - sidebarWidth, w.height - headerHeight - footerHeight);
            var footer = new Rect(w.x, w.yMax - footerHeight, w.width, footerHeight);

            DrawHeader(header, e);
            DrawSidebar(sidebar);
            DrawContent(content, e);
            DrawFooter(footer);
            DrawResizeGrip(e);
            HandleWindowDrag(header, e);
            Draw.Outline(w, Theme.Border, 1f, 14f * s);

            Draw.Interactive = true;
            // Swallow every mouse event aimed at the window so the game never sees it.
            bool overWindow = w.Contains(e.mousePosition) || _drag != DragMode.None || Widgets.DraggedSlider != null;
            if (overWindow && (e.isMouse || e.type == EventType.ScrollWheel))
                e.Use();
        }

        // ================================================================== Header

        private static void DrawHeader(Rect r, Event e)
        {
            float s = _scale;
            Draw.Box(r, Theme.Panel, new Vector4(14f * s, 14f * s, 0f, 0f));
            Draw.Box(new Rect(r.x, r.yMax - 1f, r.width, 1f), Theme.BorderSoft);

            var mark = new Rect(r.x + 18f * s, r.y + 15f * s, 32f * s, 32f * s);
            Draw.Box(mark, Theme.Accent, 9f * s);
            Draw.Box(new Rect(mark.x + 9f * s, mark.y + 9f * s, 14f * s, 14f * s), Theme.Alpha(Theme.OnAccent, 0.85f), 7f * s);
            Draw.Box(new Rect(mark.x + 4f * s, mark.center.y - 1.5f * s, 24f * s, 3f * s), Theme.Alpha(Theme.OnAccent, 0.85f), 1.5f * s);

            float textX = mark.xMax + 12f * s;
            const string brand = "PLANETBASE";
            float brandWidth = Draw.Measure(brand, Theme.Brand).x;
            Draw.Text(new Rect(textX, r.y + 11f * s, brandWidth + 4f, 24f * s), brand, Theme.Brand, Theme.Accent);
            Draw.Text(new Rect(textX + brandWidth + 7f * s, r.y + 11f * s, 120f * s, 24f * s), "TRAINER", Theme.Brand, Theme.Text);
            string subtitle = "v" + MyPluginInfo.PLUGIN_VERSION + "   ·   " + Loc.T(G.InGame ? "ui.ingame" : "ui.notingame");
            Draw.Text(new Rect(textX, r.y + 35f * s, 320f * s, 16f * s), subtitle, Theme.Tiny, Theme.Muted);

            var close = new Rect(r.xMax - 48f * s, r.y + 15f * s, 32f * s, 32f * s);
            bool hover = Draw.Hover(close);
            if (hover)
                Draw.Box(close, Theme.Alpha(Theme.Danger, 0.18f), 8f * s);
            Draw.Icon(Icons.Close, close.Shrink(9f * s), hover ? Theme.Danger : Theme.SubText);
            if (Draw.Clicked(close))
                SetVisible(false);

            float searchWidth = Mathf.Min(310f * s, r.width * 0.36f);
            DrawSearch(new Rect(close.x - 14f * s - searchWidth, r.y + 14f * s, searchWidth, 34f * s), e);
        }

        private static void DrawSearch(Rect r, Event e)
        {
            float s = _scale;
            bool focused = _searchFocused;
            Draw.Box(r, Theme.Field, 9f * s);
            Draw.Outline(r, focused ? Theme.Alpha(Theme.Accent, 0.85f) : Draw.Hover(r) ? Theme.Border : Theme.BorderSoft,
                focused ? 1.5f : 1f, 9f * s);
            var icon = new Rect(r.x + 11f * s, r.center.y - 7f * s, 14f * s, 14f * s);
            Draw.Icon(Icons.Search, icon, focused ? Theme.Accent : Theme.Muted);
            var field = new Rect(icon.xMax + 9f * s, r.y, r.width - (icon.xMax - r.x) - 40f * s, r.height);

            if (focused && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (_search.Length > 0)
                    _search = string.Empty;
                else
                    _dropFocus = true;
                SuppressInputUntilRelease();
                e.Use();
            }
            if (focused && e.type == EventType.MouseDown && !r.Contains(e.mousePosition))
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
            }

            if (!IsCapturing)
            {
                var skinSettings = GUI.skin.settings;
                var previousCursor = skinSettings.cursorColor;
                var previousSelection = skinSettings.selectionColor;
                skinSettings.cursorColor = Theme.Accent;
                skinSettings.selectionColor = Theme.Alpha(Theme.Accent, 0.35f);
                var previousContent = GUI.contentColor;
                GUI.contentColor = Theme.Text;
                GUI.SetNextControlName(SearchControl);
                string value = GUI.TextField(field, _search, 64, Theme.Input);
                GUI.contentColor = previousContent;
                skinSettings.cursorColor = previousCursor;
                skinSettings.selectionColor = previousSelection;
                if (value != _search)
                {
                    _search = value;
                    _scroll = 0f;
                }
            }
            _searchFocused = GUI.GetNameOfFocusedControl() == SearchControl;

            if (_search.Length == 0 && !_searchFocused)
                Draw.Text(field, Loc.T("ui.search"), Theme.Body, Theme.Muted);
            if (_search.Length > 0)
            {
                var clear = new Rect(r.xMax - 32f * s, r.center.y - 12f * s, 24f * s, 24f * s);
                bool hover = Draw.Hover(clear);
                if (hover)
                    Draw.Box(clear, Theme.CardHover, 12f * s);
                Draw.Icon(Icons.Close, clear.Shrink(6f * s), hover ? Theme.Text : Theme.SubText);
                if (Draw.Clicked(clear))
                {
                    _search = string.Empty;
                    _scroll = 0f;
                }
            }
        }

        // ================================================================== Sidebar

        private static void DrawSidebar(Rect r)
        {
            float s = _scale;
            Draw.Box(r, Theme.Panel);
            Draw.Box(new Rect(r.xMax - 1f, r.y, 1f, r.height), Theme.BorderSoft);

            bool searching = _search.Length > 0;
            float y = r.y + 12f * s;
            foreach (var tab in TabOrder)
            {
                if (tab == Tab.Settings)
                {
                    Draw.Box(new Rect(r.x + 18f * s, y + 3f * s, r.width - 36f * s, 1f), Theme.BorderSoft);
                    y += 8f * s;
                }
                var item = new Rect(r.x + 10f * s, y, r.width - 20f * s, 34f * s);
                bool selected = tab == _tab && !searching;
                bool hover = Draw.Hover(item);
                if (selected)
                {
                    Draw.Box(item, Theme.Alpha(Theme.Accent, 0.13f), 9f * s);
                    Draw.Box(new Rect(item.x, item.y + 8f * s, 3f * s, item.height - 16f * s), Theme.Accent, 1.5f * s);
                }
                else if (hover)
                {
                    Draw.Box(item, Theme.CardHover, 9f * s);
                }

                if (tab == Tab.Favorites)
                    Draw.Icon(Icons.Star, new Rect(item.x + 12f * s, item.center.y - 7f * s, 14f * s, 14f * s), selected ? Theme.Star : Theme.Muted);
                else
                    Draw.Box(new Rect(item.x + 15f * s, item.center.y - 4f * s, 8f * s, 8f * s), selected ? Theme.Accent : Theme.Muted, 4f * s);
                Draw.Text(new Rect(item.x + 34f * s, item.y, item.width - 74f * s, item.height), Loc.T("tab." + tab), Theme.BodyBold,
                    selected || hover ? Theme.Text : Theme.SubText);

                int active = CountActive(tab);
                if (active > 0)
                {
                    var badge = new Rect(item.xMax - 34f * s, item.center.y - 10f * s, 26f * s, 20f * s);
                    Draw.Box(badge, Theme.Alpha(Theme.Accent, 0.2f), 10f * s);
                    Draw.Text(badge, active.ToString(), Theme.TinyBold, Theme.Accent);
                }
                if (Draw.Clicked(item))
                {
                    _tab = tab;
                    _scroll = 0f;
                    _search = string.Empty;
                    Settings.LastTab.Value = (int)tab;
                    Settings.MarkDirty();
                }
                y += 37f * s;
                if (tab == Tab.Favorites)
                {
                    Draw.Box(new Rect(r.x + 18f * s, y + 2f * s, r.width - 36f * s, 1f), Theme.BorderSoft);
                    y += 8f * s;
                }
            }

            var status = new Rect(r.x + 12f * s, r.yMax - 60f * s, r.width - 24f * s, 48f * s);
            if (status.y < y + 4f * s)
                return; // not enough room: the tabs have priority
            bool inGame = G.InGame;
            Draw.Box(status, Theme.Field, 10f * s);
            Draw.Box(new Rect(status.x + 12f * s, status.y + 13f * s, 8f * s, 8f * s), inGame ? Theme.Success : Theme.Warning, 4f * s);
            Draw.Text(new Rect(status.x + 28f * s, status.y + 7f * s, status.width - 36f * s, 20f * s),
                Loc.T(inGame ? "ui.ingame" : "ui.notingame"), Theme.SmallBold, Theme.Text);
            bool allPatched = PatchManager.Failures.Count == 0;
            Draw.Text(new Rect(status.x + 28f * s, status.y + 25f * s, status.width - 36f * s, 16f * s),
                Loc.F("ui.patches", PatchManager.AppliedCount, PatchManager.TotalCount), Theme.Tiny,
                allPatched ? Theme.Muted : Theme.Danger);
        }

        private static int CountActive(Tab tab)
        {
            if (tab == Tab.Settings)
                return 0;
            if (tab != Tab.Favorites)
                return Cheats.ActiveCount(tab);
            int count = 0;
            foreach (var toggle in Cheats.Toggles)
            {
                if (toggle.On && Settings.IsFavorite(toggle.Id))
                    count++;
            }
            return count;
        }

        // ================================================================== Content (scrollable)

        private static void DrawContent(Rect area, Event e)
        {
            float s = _scale;
            var view = new Rect(area.x + 18f * s, area.y + 14f * s, area.width - 28f * s, area.height - 20f * s);
            float width = view.width - 14f * s;
            bool settingsPage = _tab == Tab.Settings && _search.Length == 0;

            if (settingsPage)
            {
                _contentHeight = SettingsPage.Measure(width, s);
            }
            else
            {
                BuildRows();
                _contentHeight = MeasureRows(width);
            }

            float maxScroll = Mathf.Max(0f, _contentHeight - view.height);
            if (e.type == EventType.ScrollWheel && Draw.Hover(view))
            {
                _scroll += e.delta.y * 40f * s;
                e.Use();
            }
            _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);

            GUI.BeginGroup(view);
            Draw.PushClip(new Rect(0f, 0f, view.width, view.height));
            try
            {
                if (settingsPage)
                    SettingsPage.Render(width, -_scroll, view.height, s);
                else
                    DrawRows(width, view.height);
            }
            finally
            {
                Draw.PopClip();
                GUI.EndGroup();
            }
            DrawScrollbar(view, maxScroll, e);
        }

        private static Row NextRow()
        {
            int index = Rows.Count;
            if (index >= RowPool.Count)
                RowPool.Add(new Row());
            var row = RowPool[index];
            row.Header = null;
            row.Cheat = null;
            row.Height = 0f;
            Rows.Add(row);
            return row;
        }

        private static void BuildRows()
        {
            Rows.Clear();
            if (_search.Length > 0)
            {
                EnsureSearchIndex();
                var terms = Normalize(_search).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tab in TabOrder)
                {
                    bool headerAdded = false;
                    foreach (var cheat in Cheats.All)
                    {
                        if (cheat.Tab != tab || !Matches(cheat, terms))
                            continue;
                        if (!headerAdded)
                        {
                            NextRow().Header = Loc.T("tab." + tab);
                            headerAdded = true;
                        }
                        NextRow().Cheat = cheat;
                    }
                }
                return;
            }

            if (_tab == Tab.Favorites)
            {
                foreach (var tab in TabOrder)
                {
                    bool headerAdded = false;
                    foreach (var cheat in Cheats.All)
                    {
                        if (cheat.Tab != tab || !Settings.IsFavorite(cheat.Id))
                            continue;
                        if (!headerAdded)
                        {
                            NextRow().Header = Loc.T("tab." + tab);
                            headerAdded = true;
                        }
                        NextRow().Cheat = cheat;
                    }
                }
                return;
            }

            string section = null;
            foreach (var cheat in Cheats.All)
            {
                if (cheat.Tab != _tab)
                    continue;
                if (cheat.SectionKey != section)
                {
                    section = cheat.SectionKey;
                    NextRow().Header = Loc.T(section);
                }
                NextRow().Cheat = cheat;
            }
        }

        private static float MeasureRows(float width)
        {
            float s = _scale;
            float total = 0f;
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                row.Height = row.Cheat != null ? MeasureCard(row.Cheat, width) : (i == 0 ? 26f : 40f) * s;
                total += row.Height + (row.Cheat != null ? 8f * s : 0f);
            }
            return Rows.Count == 0 ? 120f * s : total;
        }

        private static void DrawRows(float width, float viewHeight)
        {
            float s = _scale;
            if (Rows.Count == 0)
            {
                string message = _search.Length > 0 ? Loc.F("ui.noresults", _search) : Loc.T("ui.favempty");
                var rect = new Rect(0f, 30f * s, width, 60f * s);
                if (_search.Length == 0)
                    Draw.Icon(Icons.StarOutline, new Rect(width / 2f - 14f * s, 0f, 28f * s, 28f * s), Theme.Muted);
                Draw.Text(rect, message, Theme.SmallCenter, Theme.SubText);
                return;
            }
            float y = -_scroll;
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                if (y + row.Height >= 0f && y <= viewHeight)
                {
                    var rect = new Rect(0f, y, width, row.Height);
                    if (row.Cheat != null)
                        DrawCard(row.Cheat, rect);
                    else
                        DrawSectionHeader(row.Header, rect);
                }
                y += row.Height + (row.Cheat != null ? 8f * s : 0f);
            }
        }

        public static void DrawSectionHeader(string text, Rect r)
        {
            float s = _scale;
            string upper = text.ToUpperInvariant();
            var labelRect = new Rect(r.x + 2f * s, r.yMax - 24f * s, r.width, 18f * s);
            Draw.Text(labelRect, upper, Theme.Section, Theme.Muted);
            float textWidth = Draw.Measure(upper, Theme.Section).x;
            Draw.Box(new Rect(r.x + textWidth + 14f * s, labelRect.center.y, Mathf.Max(0f, r.width - textWidth - 16f * s), 1f), Theme.BorderSoft);
        }

        private static void DrawScrollbar(Rect view, float maxScroll, Event e)
        {
            if (maxScroll <= 0.5f)
                return;
            float s = _scale;
            var track = new Rect(view.xMax - 6f * s, view.y + 2f * s, 5f * s, view.height - 4f * s);
            var hitArea = new Rect(track.x - 7f * s, track.y, track.width + 12f * s, track.height);
            float thumbHeight = Mathf.Max(34f * s, track.height * view.height / Mathf.Max(1f, _contentHeight));
            float travel = Mathf.Max(1f, track.height - thumbHeight);
            var thumb = new Rect(track.x, track.y + travel * (_scroll / maxScroll), track.width, thumbHeight);
            bool active = _drag == DragMode.Scrollbar || Draw.Hover(hitArea);

            Draw.Box(track, Theme.Alpha(Theme.Border, 0.35f), 2.5f * s);
            Draw.Box(thumb, active ? Theme.SubText : Theme.Border, 2.5f * s);

            if (e.type == EventType.MouseDown && e.button == 0 && Draw.Hover(hitArea))
            {
                if (!thumb.Contains(e.mousePosition))
                    _scroll = Mathf.Clamp((e.mousePosition.y - track.y - thumbHeight / 2f) / travel * maxScroll, 0f, maxScroll);
                _drag = DragMode.Scrollbar;
                _dragStartMouse = e.mousePosition.y;
                _dragStartScroll = _scroll;
                e.Use();
            }
            else if (_drag == DragMode.Scrollbar && e.type == EventType.MouseDrag)
            {
                _scroll = Mathf.Clamp(_dragStartScroll + (e.mousePosition.y - _dragStartMouse) / travel * maxScroll, 0f, maxScroll);
                e.Use();
            }
        }

        // ================================================================== Cheat cards

        private static float CardPadding => 14f * _scale;

        private static float CardInnerWidth(float width) => width - CardPadding * 2f - 26f * _scale;

        private static float MeasureCard(Cheat cheat, float width)
        {
            float s = _scale;
            float inner = CardInnerWidth(width);
            float height = CardPadding + 26f * s + DescriptionHeight(cheat, inner);
            if (!cheat.IsSupported)
                height += 20f * s;
            if (cheat.Options.Count > 0)
            {
                height += 10f * s;
                for (int i = 0; i < cheat.Options.Count; i++)
                    height += OptionHeight(cheat.Options[i], inner) + (i > 0 ? 6f * s : 0f);
            }
            return height + CardPadding;
        }

        private static float DescriptionHeight(Cheat cheat, float width)
        {
            // Cache key: language version + scale + width (CalcHeight on wrapped text is not free).
            if (DescriptionHeights.TryGetValue(cheat.Id, out var cached) &&
                Mathf.Approximately(cached.x, width) && Mathf.Approximately(cached.y, Loc.Version * 1000f + _scale))
                return cached.z;
            float height = Mathf.Ceil(Draw.MeasureHeight(cheat.Description, Theme.Description, width));
            DescriptionHeights[cheat.Id] = new Vector3(width, Loc.Version * 1000f + _scale, height);
            return height;
        }

        private static void DrawCard(Cheat cheat, Rect r)
        {
            float s = _scale;
            float p = CardPadding;
            bool supported = cheat.IsSupported;
            bool inGame = G.InGame;
            var toggle = cheat as ToggleCheat;
            var action = cheat as ActionCheat;
            bool on = toggle != null && toggle.On;
            bool hover = Draw.Hover(r);

            Draw.Box(r, hover ? Theme.CardHover : Theme.Card, 11f * s);
            if (on)
            {
                Draw.Outline(r, Theme.Alpha(Theme.Accent, 0.4f), 1f, 11f * s);
                Draw.Box(new Rect(r.x, r.y + 12f * s, 3f * s, r.height - 24f * s), Theme.Accent, 1.5f * s);
            }
            else
            {
                Draw.Outline(r, Theme.BorderSoft, 1f, 11f * s);
            }

            // Favorite star.
            var star = new Rect(r.x + p - 2f * s, r.y + p + 3f * s, 18f * s, 18f * s);
            bool favorite = Settings.IsFavorite(cheat.Id);
            bool starHover = Draw.Hover(star);
            Draw.Icon(favorite ? Icons.Star : Icons.StarOutline, star,
                favorite ? Theme.Star : starHover ? Theme.SubText : Theme.Alpha(Theme.Muted, 0.8f));
            if (Draw.Clicked(star))
                Settings.ToggleFavorite(cheat.Id);

            float left = r.x + p + 26f * s;
            float right = r.xMax - p;

            // Main control (switch or run button).
            if (toggle != null)
            {
                var switchRect = new Rect(right - 46f * s, r.y + p, 46f * s, 24f * s);
                if (Widgets.Switch(switchRect, on, cheat, supported, s))
                    toggle.Set(!on);
                right = switchRect.x - 12f * s;
            }
            else if (action != null)
            {
                bool confirming = TrainerRuntime.IsAwaitingConfirm(action);
                var button = new Rect(right - 120f * s, r.y + p - 2f * s, 120f * s, 28f * s);
                string label = !inGame ? Loc.T("ui.needgame") : confirming ? Loc.T("ui.confirm") : Loc.T("ui.run");
                var kind = confirming || action.Danger ? Widgets.ButtonKind.Danger : Widgets.ButtonKind.Primary;
                if (Widgets.Button(button, label, kind, supported && inGame, s))
                    TrainerRuntime.Run(action, fromHotkey: false);
                right = button.x - 12f * s;
            }

            var hotkey = new Rect(right - 118f * s, r.y + p - 1f * s, 118f * s, 26f * s);
            Widgets.HotkeyChip(hotkey, cheat.Hotkey, cheat.Name, s);
            right = hotkey.x - 10f * s;

            // Title (+ "risky" tag).
            var title = new Rect(left, r.y + p, Mathf.Max(0f, right - left), 24f * s);
            Draw.Text(title, cheat.Name, Theme.Title, supported ? Theme.Text : Theme.Muted);
            if (cheat.Danger)
            {
                float titleWidth = Draw.Measure(cheat.Name, Theme.Title).x;
                string tagText = Loc.T("ui.danger");
                var tag = new Rect(left + titleWidth + 9f * s, title.center.y - 9f * s, Draw.Measure(tagText, Theme.TinyBold).x + 14f * s, 18f * s);
                if (tag.xMax < right)
                {
                    Draw.Box(tag, Theme.Alpha(Theme.Danger, 0.16f), 9f * s);
                    Draw.Text(tag, tagText, Theme.TinyBold, Theme.Danger);
                }
            }

            // Description, status and options use the full width below the title row.
            float inner = CardInnerWidth(r.width);
            float y = r.y + p + 26f * s;
            float descriptionHeight = DescriptionHeight(cheat, inner);
            Draw.Text(new Rect(left, y, inner, descriptionHeight + 2f), cheat.Description, Theme.Description, Theme.SubText);
            y += descriptionHeight;
            if (!supported)
            {
                Draw.Text(new Rect(left, y + 2f * s, inner, 18f * s), Loc.T("ui.unsupported"), Theme.SmallBold, Theme.Danger);
                y += 20f * s;
            }
            if (cheat.Options.Count == 0)
                return;
            y += 10f * s;
            for (int i = 0; i < cheat.Options.Count; i++)
            {
                if (i > 0)
                    y += 6f * s;
                var option = cheat.Options[i];
                float height = OptionHeight(option, inner);
                DrawOption(option, new Rect(left, y, inner, height), supported);
                y += height;
            }
        }

        private static float OptionLabelWidth(float width) => Mathf.Min(140f * _scale, width * 0.3f);

        private static float OptionHeight(Option option, float width)
        {
            float s = _scale;
            if (option is BoolGroupOption group)
            {
                float rowsHeight = LayoutChips(group.Items, width - OptionLabelWidth(width), null);
                return Mathf.Max(28f * s, rowsHeight);
            }
            return 28f * s;
        }

        private static void DrawOption(Option option, Rect r, bool enabled)
        {
            float s = _scale;
            float labelWidth = OptionLabelWidth(r.width);
            Draw.Text(new Rect(r.x, r.y, labelWidth, 28f * s), option.Label, Theme.SmallBold, enabled ? Theme.SubText : Theme.Muted);
            var control = new Rect(r.x + labelWidth, r.y, r.width - labelWidth, r.height);
            switch (option)
            {
                case FloatOption slider:
                    Widgets.Slider(new Rect(control.x, control.y, Mathf.Min(control.width, 440f * s), 28f * s), slider, enabled, s);
                    break;
                case ChoiceOption choice:
                {
                    int direction = Widgets.Stepper(new Rect(control.x, control.y + 1f * s, Mathf.Min(control.width, 250f * s), 26f * s),
                        choice.Display, enabled, s);
                    if (direction != 0)
                        choice.Cycle(direction);
                    break;
                }
                case BoolGroupOption group:
                {
                    LayoutChips(group.Items, control.width, ChipRects);
                    for (int i = 0; i < group.Items.Length; i++)
                    {
                        var item = group.Items[i];
                        var chip = ChipRects[i];
                        chip.x += control.x;
                        chip.y += control.y;
                        if (Widgets.Chip(chip, item.Label, item.Value, enabled, s))
                            item.Set(!item.Value);
                    }
                    break;
                }
            }
        }

        /// <summary>Flows chips into rows. Returns the total height; fills <paramref name="output"/> with relative rects.</summary>
        private static float LayoutChips(BoolOption[] items, float width, List<Rect> output)
        {
            float s = _scale;
            float height = 26f * s;
            float gap = 6f * s;
            output?.Clear();
            float x = 0f, y = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                float chipWidth = Mathf.Min(Widgets.ChipWidth(items[i].Label, s), width);
                if (x > 0f && x + chipWidth > width)
                {
                    x = 0f;
                    y += height + gap;
                }
                output?.Add(new Rect(x, y + 1f * s, chipWidth, height));
                x += chipWidth + gap;
            }
            return y + height + 2f * s;
        }

        // ================================================================== Footer, resize & drag

        private static void DrawFooter(Rect r)
        {
            float s = _scale;
            Draw.Box(r, Theme.Panel, new Vector4(0f, 0f, 14f * s, 14f * s));
            Draw.Box(new Rect(r.x, r.y, r.width, 1f), Theme.BorderSoft);

            int active = Cheats.ActiveCount();
            var button = new Rect(r.xMax - 30f * s - 150f * s, r.y + 7f * s, 150f * s, r.height - 14f * s);
            if (Widgets.Button(button, Loc.T("ui.disableall"), Widgets.ButtonKind.Danger, active > 0, s))
                TrainerRuntime.DisableAll();

            string count = Loc.F("ui.active", active);
            float countWidth = Draw.Measure(count, Theme.SmallBold).x;
            var countRect = new Rect(button.x - 14f * s - countWidth, r.y, countWidth + 2f, r.height);
            Draw.Text(countRect, count, Theme.SmallBold, active > 0 ? Theme.Accent : Theme.Muted);

            string hint = IsCapturing ? Loc.T("ui.capturehint") : Loc.F("ui.hint", HotkeyUtil.Format(Settings.MenuKey.Value));
            Draw.Text(new Rect(r.x + 20f * s, r.y, Mathf.Max(0f, countRect.x - r.x - 34f * s), r.height), hint, Theme.Small,
                IsCapturing ? Theme.Accent : Theme.Muted);
        }

        private static void DrawResizeGrip(Event e)
        {
            float s = _scale;
            var grip = new Rect(_window.xMax - 20f * s, _window.yMax - 20f * s, 16f * s, 16f * s);
            bool active = _drag == DragMode.Resize || Draw.Hover(grip);
            var color = active ? Theme.SubText : Theme.Muted;
            float dot = 2.5f * s;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col <= row; col++)
                {
                    float x = grip.xMax - (col + 1) * 5f * s;
                    float y = grip.yMax - (row - col + 1) * 5f * s;
                    Draw.Box(new Rect(x, y, dot, dot), color, dot / 2f);
                }
            }
            if (e.type == EventType.MouseDown && e.button == 0 && Draw.Hover(grip))
            {
                _drag = DragMode.Resize;
                _dragOffset = new Vector2(_window.xMax - e.mousePosition.x, _window.yMax - e.mousePosition.y);
                e.Use();
            }
        }

        private static void HandleWindowDrag(Rect header, Event e)
        {
            float s = _scale;
            if (e.type == EventType.MouseDown && e.button == 0 && Draw.Hover(header))
            {
                _drag = DragMode.Window;
                _dragOffset = e.mousePosition - _window.position;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (_drag == DragMode.Window)
                {
                    _window.position = e.mousePosition - _dragOffset;
                    ClampWindow();
                    e.Use();
                }
                else if (_drag == DragMode.Resize)
                {
                    _window.width = Mathf.Max(BaseMinWidth * s, e.mousePosition.x + _dragOffset.x - _window.x);
                    _window.height = Mathf.Max(BaseMinHeight * s, e.mousePosition.y + _dragOffset.y - _window.y);
                    ClampWindow();
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && _drag != DragMode.None)
            {
                EndDrag();
                e.Use();
            }
        }

        private static void EndDrag()
        {
            if (_drag == DragMode.Window || _drag == DragMode.Resize)
            {
                Settings.WinX.Value = _window.x;
                Settings.WinY.Value = _window.y;
                Settings.WinW.Value = _window.width / _scale;
                Settings.WinH.Value = _window.height / _scale;
                Settings.MarkDirty();
            }
            _drag = DragMode.None;
        }

        // ================================================================== Search

        private static void EnsureSearchIndex()
        {
            if (_searchIndexVersion == Loc.Version)
                return;
            _searchIndexVersion = Loc.Version;
            SearchIndex.Clear();
            foreach (var cheat in Cheats.All)
            {
                var text = new StringBuilder();
                text.Append(cheat.Name).Append(' ').Append(cheat.Description).Append(' ').Append(Loc.T("tab." + cheat.Tab));
                foreach (var option in cheat.Options)
                {
                    text.Append(' ').Append(option.Label);
                    if (option is BoolGroupOption group)
                    {
                        foreach (var item in group.Items)
                            text.Append(' ').Append(item.Label);
                    }
                }
                SearchIndex[cheat] = Normalize(text.ToString());
            }
        }

        private static bool Matches(Cheat cheat, string[] terms)
        {
            if (!SearchIndex.TryGetValue(cheat, out var text))
                return false;
            foreach (var term in terms)
            {
                if (text.IndexOf(term, StringComparison.Ordinal) < 0)
                    return false;
            }
            return true;
        }

        /// <summary>Lower-case, accent-insensitive form ("Énergie" matches "energie").</summary>
        private static string Normalize(string text)
        {
            try
            {
                var decomposed = text.Normalize(NormalizationForm.FormD);
                var builder = new StringBuilder(decomposed.Length);
                foreach (char c in decomposed)
                {
                    if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                        builder.Append(char.ToLowerInvariant(c));
                }
                return builder.ToString();
            }
            catch (Exception)
            {
                return text.ToLowerInvariant();
            }
        }
    }
}
