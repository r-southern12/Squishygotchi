using System;
using System.Collections.Generic;
using Squishy.Runtime.Game;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// The prototype's phone UI rebuilt in UI Toolkit at its 375px CSS scale: top HUD (name, coins, sound, needs,
    /// condition, comfort, view), bottom controls with the big Unbox/Hold/Done button, tray, zoom bar, speech bubble,
    /// floaters, reveal and memorial cards, panels, catalogue sheet and the wipe.
    /// </summary>
    public sealed partial class Hud
    {
        public const string Ink = "#33261D", Muted = "#7A6656", Cream = "#FFF7EC";
        public static readonly Color ChipFill = C("rgba(247,240,228,.94)");
        public static readonly Shadow ChipShadow = new Shadow(0, 3, 0, 0, C("rgba(90,60,40,.18)"));

        private readonly SteamerGame _g;
        private readonly GameContent _c;
        public readonly VisualElement Root;
        public readonly Tweens Tw = new Tweens();

        private readonly List<(VisualElement el, string[] modes)> _modal = new List<(VisualElement, string[])>();
        private VisualElement _top, _bottom, _canvas, _floaters;
        private Frame _gift;
        private Frame _prestige;
        private Frame _expandDot;
        private Label _prestigeLbl;
        private Label _giftLbl;
        private Label _name, _sub, _coins, _condTx, _hint, _mainLbl, _badge, _fps, _tiltLbl, _trayTitle, _oddsBtnLbl, _pityTx;
        private Frame _condDot, _comfort, _main, _taskDot, _bubble, _undo, _rot, _putAway, _snd;
        private Glyph _sndIcon, _viewIcon, _bIcon, _ring;
        private Label _bText;
        private readonly Frame[] _bars = new Frame[4], _barFill = new Frame[4];
        private readonly float[] _barW = { -1, -1, -1, -1 };
        private ScrollView _trayList;
        private VisualElement _wipe;
        private float _wipeR, _charge;
        private bool _soundOn, _mainDown, _bubbleOn, _hudShown = true;
        private string _mode = "home";

        public float Width { get { float w = Root.layout.width; return float.IsNaN(w) || w <= 0 ? 375 : w; } }
        public float Height { get { float h = Root.layout.height; return float.IsNaN(h) || h <= 0 ? 375f * Screen.height / Mathf.Max(1, Screen.width) : h; } }
        public bool BubbleOn { get { return _bubbleOn; } }

        public Hud(SteamerGame game, GameContent content)
        {
            _g = game;
            _c = content;
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(375, 812);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0;
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/Theme");
            ps.clearColor = false;
            var go = new GameObject("UI");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            Root = doc.rootVisualElement;
            Root.style.flexGrow = 1;
            Root.Text("Figtree", 400, 15, Ink);
            Build();
        }

        // ---------------- structure ----------------

        private T Modal<T>(T el, params string[] modes) where T : VisualElement { _modal.Add((el, modes)); return el; }

        public static Frame Chip(VisualElement parent, float radius)
        {
            var b = new Frame().Set(ChipFill, radius, ChipShadow);
            parent?.Add(b);
            return b;
        }

        /// <summary>A pressable element (button semantics: click on release inside).</summary>
        public static T Tap<T>(T el, Action onClick) where T : VisualElement
        {
            el.pickingMode = PickingMode.Position;
            el.AddManipulator(new Clickable(() => { if (el.enabledSelf) onClick(); }));
            return el;
        }

        public static void Enable(VisualElement el, bool on, float disabledOpacity = .45f)
        {
            el.SetEnabled(on);
            el.style.opacity = on ? 1 : disabledOpacity;
        }

        private Frame IconBtn(VisualElement parent, string icon, float size, float iconSize, float radius, Action onClick, out Glyph glyph, string color = Ink)
        {
            var b = Chip(parent, radius).Size(size, size);
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            b.style.flexShrink = 0;
            glyph = Icons.Make(icon, iconSize, color);
            b.Add(glyph);
            return Tap(b, onClick);
        }

        private void Build()
        {
            var safe = Screen.safeArea;
            float k = 375f / Mathf.Max(1, Screen.width);
            float safeTop = (Screen.height - safe.yMax) * k, safeBottom = safe.yMin * k;

            _canvas = new VisualElement().Abs(0, 0, 0, 0).In(Root);
            _canvas.pickingMode = PickingMode.Position;
            _canvas.RegisterCallback<PointerDownEvent>(e => { _canvas.CapturePointer(e.pointerId); _g.CanvasDown(e.pointerId, e.localPosition, e.pointerType == UnityEngine.UIElements.PointerType.touch); });
            _canvas.RegisterCallback<PointerMoveEvent>(e => _g.CanvasMove(e.pointerId, e.localPosition));
            _canvas.RegisterCallback<PointerUpEvent>(e => { _canvas.ReleasePointer(e.pointerId); _g.CanvasUp(e.pointerId, e.localPosition); });
            _canvas.RegisterCallback<PointerCancelEvent>(e => { _canvas.ReleasePointer(e.pointerId); _g.CanvasUp(e.pointerId, e.localPosition); });
            _canvas.RegisterCallback<WheelEvent>(e => _g.CanvasWheel(e.localMousePosition, e.delta.y * 33));

            BuildBubble();

            // ---- top ----
            _top = new VisualElement().Abs(0, 0, 0).Pad(14 + safeTop, 12, 0, 12).Col().NoPick().In(Root);
            var row1 = new VisualElement().Row(Align.Center, Justify.SpaceBetween).NoPick().In(_top);
            var who = Modal(Chip(row1, 16).Pad(5, 12, 6, 12), "home");
            who.style.flexShrink = 0;
            _name = Label(who, "Pinky", "Gluten", 700, 18);
            _sub = Label(who, "Day 3 · Mini", "Figtree", 600, 11, Muted);
            Tap(who, () => _g.OnSquishies());
            var edittag = Modal(new Frame().Set(C("#D9A64A"), 14, new Shadow(0, 3, 0, 0, C("#A07324"))).Pad(7, 14, 7, 14).In(row1), "edit");
            Label(edittag, "Arrange room", "Gluten", 800, 17, Cream);
            var back = Modal(Chip(row1, 14).Row().Pad(0, 14, 0, 10).Size(null, 38), "unbox");
            back.Add(Icons.Make("back", 18));
            Label(back, "Home", "Gluten", 700, 16).Margin(0, 0, 0, 6);
            Tap(back, () => _g.GoHome());
            new VisualElement { style = { flexGrow = 1 } }.NoPick().In(row1);
            var pill = Chip(row1, -1).Row().Pad(4, 10, 4, 5);
            pill.style.flexShrink = 0;
            pill.Add(Icons.Make("coin", 20));
            _coins = Label(pill, "248", "Gluten", 700, 15).Margin(0, 0, 0, 5);
            // Tap: shop. Hold for a second: the hidden admin test panel.
            bool held = false;
            IVisualElementScheduledItem hold = null;
            pill.RegisterCallback<PointerDownEvent>(e => { held = false; hold = pill.schedule.Execute(() => { held = true; _g.OnAdmin(); }).StartingIn(1000); });
            pill.RegisterCallback<PointerUpEvent>(e => hold?.Pause());
            pill.RegisterCallback<PointerLeaveEvent>(e => hold?.Pause());
            Tap(pill, () => { if (!held) _g.OnCoinPill(); });
            var odds = Modal(Chip(row1, 12).Row().Pad(0, 12, 0, 12).Size(null, 36), "unbox");
            odds.style.flexShrink = 0;
            _oddsBtnLbl = Label(odds, "Odds", "Figtree", 700, 13);
            Tap(odds, () => _g.OnOdds());
            // Gift steamer: ready every few hours.
            _gift = Chip(row1, -1).Row().Pad(4, 10, 4, 6);
            _gift.style.flexShrink = 0;
            _gift.Add(Icons.Make("gift", 20));
            _giftLbl = Label(_gift, "", "Gluten", 700, 13).Margin(0, 0, 0, 4);
            Tap(_gift, () => _g.OnGift());
            Modal(_gift, "home");
            // Sound lives in Settings; the gear takes the sound button's place so the row fits the screen.
            _snd = IconBtn(null, "sound_off", 36, 18, -1, ToggleSound, out _sndIcon);
            IconBtn(row1, "gear", 36, 18, -1, () => _g.OnSettings(), out _);
            row1.Gap(6);

            var needs = Modal(Chip(_top, 16).Row().Pad(7, 9, 7, 9), "home");
            needs.pickingMode = PickingMode.Position;
            string[] icons = { "hunger", "play", "rest", "clean" }, cols = { "#C8674E", "#6E9C9A", "#8C7BB0", "#7FB0C9" };
            for (int i = 0; i < 4; i++)
            {
                var need = new VisualElement().Row().NoPick().In(needs);
                need.style.flexGrow = 1;
                need.style.flexBasis = 0;
                need.Add(Icons.Make(icons[i], 16));
                var bar = new Frame().Set(C("#E4D6C1"), 99).Size(null, 8).Margin(0, 0, 0, 5).In(need);
                bar.style.flexGrow = 1;
                bar.style.overflow = Overflow.Hidden;
                _bars[i] = bar;
                _barFill[i] = new Frame().Set(C(cols[i]), 99).Abs(0, 0, null, 0).In(bar);
            }
            needs.Gap(6);

            var row3 = Modal(new VisualElement().Row(Align.Center, Justify.SpaceBetween).NoPick().In(_top), "home");
            var cond = Chip(row3, -1).Row().Pad(4, 11, 4, 8);
            cond.style.flexShrink = 0;
            _condDot = new Frame().Set(C("#6F9A74"), -1).Size(10, 10).In(cond);
            _condTx = Label(cond, "Happy", "Figtree", 700, 13).Margin(0, 0, 0, 6);
            // Prestige at a glance; tap for this life's outlook.
            _prestige = Chip(row3, -1).Row().Pad(4, 10, 4, 7);
            _prestige.style.flexShrink = 0;
            _prestige.Add(Icons.Make("star", 14, "#D9A64A"));
            _prestigeLbl = Label(_prestige, "0", "Gluten", 700, 13).Margin(0, 0, 0, 4);
            Tap(_prestige, () => _g.OnPrestige());
            _comfort = Chip(row3, -1).Pad(4, 10, 4, 10);
            _comfort.style.flexGrow = 1;
            _comfort.style.flexShrink = 1;
            _comfort.style.overflow = Overflow.Hidden;
            var comfortLbl = Label(_comfort, "Comfort 0", "Figtree", 700, 12, Muted);
            comfortLbl.style.textOverflow = TextOverflow.Ellipsis;
            comfortLbl.style.overflow = Overflow.Hidden;
            Tap(_comfort, () => _g.OnComfort());
            // Zoom is pinch-only now (close-up, follow, whole room); this slot opens Friends.
            IconBtn(row3, "friends", 36, 20, -1, () => _g.OnFriends(), out _viewIcon);
            row3.Gap(6);
            var fpsBox = Chip(_top, 8).Pad(3, 8, 3, 8).Shown(false);
            fpsBox.style.alignSelf = Align.FlexStart;
            _fps = Label(fpsBox, "– fps", "Figtree", 600, 11);
            _top.Gap(7);

            // ---- bottom ----
            _bottom = new VisualElement().Abs(0, null, 0, 0).Pad(0, 12, 20 + safeBottom, 12).Col(Align.Center).NoPick().In(Root);
            BuildTray(_bottom);
            _hint = Label(_bottom, "Tap furniture to send Pinky there", "Gluten", 700, 16, "#FFF9EF");
            _hint.style.textShadow = Shadow(2, "rgba(70,40,25,.5)");
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint.style.minHeight = 24;
            _hint.style.maxWidth = 300;
            _hint.Wrap();
            var controls = new VisualElement().Row(Align.Center, Justify.Center).NoPick().In(_bottom);
            controls.style.width = Length.Percent(100);
            Side(controls, "tasks", () => _g.OnTasks(), "home", out _);
            _taskDot = new Frame().Set(C("#C8412F"), -1, new Shadow(0, 0, 0, 2, C(Cream))).Size(14, 14).Abs(null, -3, -3).Shown(false);
            controls[0].Add(_taskDot);
            Side(controls, "shop", () => _g.OnShop(), "home", out _);
            _undo = Side(controls, "undo", () => _g.Undo(), "edit", out _);
            Spacer(controls);
            BuildMain(controls);
            Side(controls, "catalogue", () => _g.OnCatalogue(), "home", out _);
            var editBtn = Side(controls, "edit", () => _g.EnterEdit(), "home", out _);
            _expandDot = new Frame().Set(C("#6F9A74"), -1, new Shadow(0, 0, 0, 2, C(Cream))).Size(14, 14).Abs(null, -3, -3).Shown(false);
            editBtn.Add(_expandDot);
            _rot = Side(controls, "rotate", () => _g.RotateSelected(), "edit", out _);
            Spacer(controls);
            _bottom.Gap(9);

            BuildZoomBar();
            BuildPanels(safeBottom);
            BuildCards(safeBottom);
            BuildSheet(safeTop, safeBottom);
            BuildIntro(safeTop, safeBottom);
            _floaters = new VisualElement().Abs(0, 0, 0, 0).NoPick().In(Root);
            _wipe = new VisualElement().Abs(0, 0, 0, 0).NoPick().In(Root);
            _wipe.generateVisualContent += m =>
            {
                if (_wipeR <= 0) return;
                float w = _wipe.layout.width, h = _wipe.layout.height, rad = _wipeR * 1.5f * Mathf.Sqrt(w * w + h * h) / Mathf.Sqrt(2);
                m.painter2D.fillColor = C("#E9C99A");
                m.painter2D.BeginPath();
                m.painter2D.Arc(new Vector2(w * .5f, h * .88f), rad, Angle.Degrees(0), Angle.Degrees(360));
                m.painter2D.Fill();
            };
            SetUndoEnabled(false);
            SetRotEnabled(false);
        }

        private Frame Side(VisualElement parent, string icon, Action onClick, string mode, out Glyph glyph)
        {
            var b = Modal(IconBtn(parent, icon, 48, 23, 16, onClick, out glyph), mode);
            b.Margin(0, 5, 0, 5);
            return b;
        }

        private void Spacer(VisualElement parent)
        {
            var s = Modal(new VisualElement().Size(48, 48).NoPick().In(parent), "unbox");
            s.Margin(0, 5, 0, 5);
            s.style.visibility = Visibility.Hidden;
        }

        private void BuildMain(VisualElement parent)
        {
            _main = new Frame().Size(88, 88).In(parent);
            _main.Margin(0, 5, 0, 5);
            _main.style.alignItems = Align.Center;
            _main.style.justifyContent = Justify.Center;
            _main.style.flexShrink = 0;
            _ring = new Glyph(104, (p, k) =>
            {
                p.lineWidth = 6 * k;
                p.strokeColor = C("rgba(255,247,236,.35)");
                p.BeginPath();
                p.Arc(new Vector2(50, 50) * k, 46 * k, Angle.Degrees(0), Angle.Degrees(360));
                p.Stroke();
                if (_charge <= .001f) return;
                p.strokeColor = C(Cream);
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(new Vector2(50, 50) * k, 46 * k, Angle.Degrees(-90), Angle.Degrees(-90 + 360 * Mathf.Min(_charge, .9999f)));
                p.Stroke();
            }, 100).Abs(-8, -8).In(_main);
            _mainLbl = Label(_main, "Unbox", "Gluten", 800, 19, Cream);
            _badge = Label(null, "3", "Gluten", 800, 14, Cream);
            var badge = new Frame().Set(C(Ink), -1, new Shadow(0, 0, 0, 2, C(Cream))).Abs(null, -2, -4).Pad(0, 6, 0, 6).Size(null, 26).In(_main);
            badge.style.minWidth = 26;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            badge.Add(_badge);
            _badge.userData = badge;
            _main.pickingMode = PickingMode.Position;
            _main.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                if (_mode == "home") { _g.OnMainHome(); return; }
                if (_mode == "edit") { _g.OnMainDone(); return; }
                _main.CapturePointer(e.pointerId);
                _g.HoldStart();
            });
            _main.RegisterCallback<PointerUpEvent>(e => { _main.ReleasePointer(e.pointerId); _g.HoldEnd(); });
            _main.RegisterCallback<PointerCancelEvent>(e => { _main.ReleasePointer(e.pointerId); _g.HoldEnd(); });
            StyleMain();
        }

        private void StyleMain()
        {
            string bg = _mode == "unbox" ? "#D9A64A" : _mode == "edit" ? "#6F9A74" : "#C8674E";
            string sh = _mode == "unbox" ? "#A07324" : _mode == "edit" ? "#4C7552" : "#8E4332";
            string soft = _mode == "unbox" ? "rgba(80,50,20,.5)" : _mode == "edit" ? "rgba(30,60,30,.45)" : "rgba(80,30,20,.5)";
            if (_mainDown) _main.Set(C(bg), -1, new Shadow(0, 2, 0, 0, C(sh)));
            else _main.Set(C(bg), -1, new Shadow(0, 10, 20, -6, C(soft)), new Shadow(0, 6, 0, 0, C(sh)));
            _main.style.translate = new Translate(0, _mainDown ? 4 : 0);
        }

        public void MainDown(bool down)
        {
            if (_mainDown == down) return;
            _mainDown = down;
            StyleMain();
        }

        public void SetRing(float charge) { _charge = charge; _ring.MarkDirtyRepaint(); }

        private void BuildTray(VisualElement parent)
        {
            var tray = Modal(Chip(parent, 18).Pad(8, 10, 8, 10).Col(), "edit");
            tray.pickingMode = PickingMode.Position;
            tray.style.width = Length.Percent(100);
            var head = new VisualElement().Row(Align.Center, Justify.SpaceBetween).In(tray);
            _trayTitle = Label(head, "Storage", "Figtree", 700, 13);
            _trayTitle.style.flexShrink = 1;
            _trayTitle.style.overflow = Overflow.Hidden;
            _trayTitle.style.textOverflow = TextOverflow.Ellipsis;
            var hb = new VisualElement().Row().In(head);
            hb.style.flexShrink = 0;
            SmallBtn(hb, "Expand room", () => _g.OnExpand());
            _putAway = SmallBtn(hb, "Put away", () => _g.PutAway());
            hb.Gap(6);
            head.Gap(8);
            _trayList = new ScrollView(ScrollViewMode.Horizontal).In(tray);
            _trayList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _trayList.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _trayList.style.minHeight = 56;
            _trayList.contentContainer.style.flexDirection = FlexDirection.Row;
            tray.Gap(6);
            SetPutAwayEnabled(false);
        }

        private Frame SmallBtn(VisualElement parent, string text, Action onClick)
        {
            var b = new Frame().Set(C("#EADCC6"), 10).Pad(5, 10, 5, 10).In(parent);
            Label(b, text, "Figtree", 700, 12);
            return Tap(b, onClick);
        }

        public void DrawTray(string title, List<(string key, bool owned, bool on, string label)> list, string emptyText, Action<string> onClick)
        {
            _trayTitle.text = title;
            _trayList.Clear();
            if (list.Count == 0)
            {
                var e = Label(_trayList, emptyText ?? "", "Figtree", 400, 12, Muted);
                e.style.alignSelf = Align.Center;
                e.style.marginTop = 20;
                return;
            }
            foreach (var entry in list)
            {
                var b = new Frame().Set(C("#EFE4D2"), 12, entry.on ? new[] { new Shadow(0, 0, 0, 3, C(Ink)) } : new Shadow[0]).Size(56, 56).Pad(2, 2, 2, 2).In(_trayList);
                b.style.flexShrink = 0;
                b.style.marginRight = 6;
                if (!entry.owned) b.style.opacity = .4f;
                string thumbKey = entry.key.Contains("#") ? entry.key.Substring(0, entry.key.IndexOf('#')) : entry.key;
                var img = new Image { scaleMode = ScaleMode.ScaleToFit, image = Thumbs.Get(thumbKey) }.In(b);
                img.style.flexGrow = 1;
                img.pickingMode = PickingMode.Ignore;
                string key = entry.key;
                Tap(b, () => onClick?.Invoke(key));
            }
        }

        private void BuildZoomBar()
        {
            var zb = Modal(new VisualElement().Abs(null, null, 12).Col(Align.Center).NoPick().In(Root), "edit");
            zb.style.top = Length.Percent(42);
            zb.style.translate = new Translate(0, Length.Percent(-50));
            IconBtn(zb, "plus", 44, 22, 14, () => _g.ZoomIn(), out _);
            IconBtn(zb, "minus", 44, 22, 14, () => _g.ZoomOut(), out _);
            IconBtn(zb, "tilt", 44, 22, 14, () => _g.CycleTilt(), out _);
            _tiltLbl = Label(zb, "Angled", "Figtree", 700, 10, "#FFF9EF");
            _tiltLbl.style.textShadow = Shadow(1, "rgba(70,40,25,.6)");
            zb.Gap(8);
        }

        private void BuildBubble()
        {
            _bubble = new Frame().Set(C("#FFF9EF"), 14, new Shadow(0, 3, 0, 0, C("rgba(90,60,40,.2)"))).Abs(0, 0).Row().Pad(4, 10, 4, 6).In(Root);
            _bubble.Paint = (r, p) =>
            {
                p.fillColor = _bubble.Fill;
                p.BeginPath();
                p.MoveTo(new Vector2(r.width / 2 - 6, r.height - .5f));
                p.LineTo(new Vector2(r.width / 2 + 6, r.height - .5f));
                p.LineTo(new Vector2(r.width / 2, r.height + 6));
                p.ClosePath();
                p.Fill();
                return false;
            };
            _bIcon = Icons.Make("idle", 16).In(_bubble);
            _bText = Label(_bubble, "", "Figtree", 700, 13).Margin(0, 0, 0, 5);
            _bubble.style.opacity = 0;
        }

        // ---------------- state setters ----------------

        public void SetMode(string mode)
        {
            _mode = mode;
            foreach (var (el, modes) in _modal) el.Shown(Array.IndexOf(modes, mode) >= 0);
            _mainLbl.text = mode == "unbox" ? "Hold" : mode == "edit" ? "Done" : "Unbox";
            _charge = 0;
            _ring.MarkDirtyRepaint();
            StyleMain();
        }

        public void SetHint(string text, bool pulse = false)
        {
            if (_hint.text != text) _hint.text = text;
            Tw.Stop(_hint);
            _hint.style.scale = Vector2.one;
            if (pulse) Tw.Run(_hint, .5f, u => _hint.style.scale = Vector2.one * (1 + .08f * Tweens.EaseInOut(u)), null, true, true);
        }

        public void ShowHud(bool show)
        {
            if (_hudShown == show) return;
            _hudShown = show;
            foreach (var el in new[] { _top, _bottom })
            {
                var e = el;
                float from = e.resolvedStyle.opacity;
                Tw.Run(e, .3f, u => e.style.opacity = Mathf.Lerp(from, show ? 1 : 0, Tweens.Ease(u)));
                e.SetEnabled(show);
            }
        }

        public void SetCoins(int n) { _coins.text = n.ToString(); }

        public void SetSteamers(int n, bool visible)
        {
            _badge.text = n.ToString();
            ((VisualElement)_badge.userData).Shown(visible);
        }

        public void SetGift(string text, bool ready)
        {
            _giftLbl.text = text;
            _gift.Fill = ready ? C("#FBE3DA") : ChipFill;
            _gift.MarkDirtyRepaint();
        }

        public void ExpandDot(bool on) { _expandDot.Shown(on); }

        public void SetPrestige(int n) { _prestigeLbl.text = n.ToString(); }

        public void SetSoundIcon(bool on)
        {
            _soundOn = on;
            _snd.Set(on ? C(Ink) : ChipFill, -1, ChipShadow);
            Icons.Set(_sndIcon, on ? "sound_on" : "sound_off", on ? "#FFF7EC" : Ink);
        }

        public void TaskDot(bool on) { _taskDot.Shown(on); }
        public void SetFps(string t) { _fps.text = t; }
        public void SetName(string n) { _name.text = n; }
        public void SetSub(string s) { _sub.text = s; }
        public void SetComfort(string s) { ((Label)_comfort[0]).text = s; }
        public void SetTiltLabel(string s) { _tiltLbl.text = s; }
        public void SetUndoEnabled(bool on) { Enable(_undo, on); }
        public void SetRotEnabled(bool on) { Enable(_rot, on); }
        public void SetPutAwayEnabled(bool on) { Enable(_putAway, on); }

        public void SetCond(string text, string color)
        {
            if (_condTx.text == text) return;
            _condTx.text = text;
            _condDot.Fill = C(color);
            _condDot.MarkDirtyRepaint();
        }

        public void DrawNeeds(float[] needs)
        {
            for (int i = 0; i < 4; i++)
            {
                float target = Mathf.Round(needs[i] * 100);
                if (_barW[i] < 0) { _barW[i] = target; _barFill[i].style.width = Length.Percent(target); }
                else if (!Mathf.Approximately(_barW[i], target))
                {
                    float from = _barW[i];
                    int k = i;
                    _barW[i] = target;
                    Tw.Run(_barFill[k], .4f, u => _barFill[k].style.width = Length.Percent(Mathf.Lerp(from, target, Tweens.Ease(u))));
                }
                bool low = needs[i] < .2f;
                var bar = _bars[i];
                if (low && bar.userData == null)
                {
                    bar.userData = true;
                    Tw.Run(bar, .8f, u => { bar.Fill = Color.Lerp(C("#E4D6C1"), C("#F2B9A8"), Tweens.EaseInOut(u)); bar.MarkDirtyRepaint(); }, null, true, true);
                }
                else if (!low && bar.userData != null)
                {
                    bar.userData = null;
                    Tw.Stop(bar);
                    bar.Fill = C("#E4D6C1");
                    bar.MarkDirtyRepaint();
                }
            }
        }

        private void ToggleSound()
        {
            _soundOn = !_soundOn;
            _snd.Set(_soundOn ? C(Ink) : ChipFill, -1, ChipShadow);
            Icons.Set(_sndIcon, _soundOn ? "sound_on" : "sound_off", _soundOn ? "#FFF7EC" : Ink);
            _g.OnSound(_soundOn);
        }

        public void ShowBubble(string icon, string text, bool warn)
        {
            string name = icon == "hunger" ? "hunger_bubble" : icon == "play" ? "play_bubble" : icon == "rest" || icon == "clean" || icon == "coin" ? icon : "idle";
            Icons.Set(_bIcon, name);
            _bText.text = text;
            _bubble.Fill = C(warn ? "#FBE3DA" : "#FFF9EF");
            _bText.style.color = C(warn ? "#8E3322" : Ink);
            _bubble.MarkDirtyRepaint();
            _bubbleOn = true;
            Fade(_bubble, 1, .25f);
        }

        public void HideBubble()
        {
            _bubbleOn = false;
            Fade(_bubble, 0, .25f);
        }

        private void Fade(VisualElement el, float to, float dur)
        {
            float from = el.resolvedStyle.opacity;
            Tw.Run(el, dur, u => el.style.opacity = Mathf.Lerp(from, to, Tweens.Ease(u)));
        }

        public void PlaceBubble(Vector2 p)
        {
            float w = _bubble.layout.width, h = _bubble.layout.height;
            if (float.IsNaN(w)) return;
            _bubble.style.translate = new Translate(p.x - w / 2, p.y - h - 6);
        }

        /// <summary>A rising text popup (the prototype's .floater, 1.1s).</summary>
        public void FloatAt(Vector2 p, string text, string cls = null)
        {
            var l = Label(_floaters, text, "Gluten", 800, cls == "z" ? 20 : 17, Cream);
            l.style.position = Position.Absolute;
            l.style.left = p.x;
            l.style.top = p.y;
            l.style.textShadow = Shadow(2, cls == "z" ? "#6A5A91" : cls == "bad" ? "#6B5A4E" : "#B85C45");
            Tw.Run(l, 1.1f, u =>
            {
                float op, ty, sc;
                if (u < .2f) { float k = Tweens.EaseOut(u / .2f); op = k; ty = Mathf.Lerp(-30, -70, k); sc = Mathf.Lerp(.6f, 1.1f, k); }
                else { float k = Tweens.EaseOut((u - .2f) / .8f); op = 1 - k; ty = Mathf.Lerp(-70, -230, k); sc = Mathf.Lerp(1.1f, 1, k); }
                l.style.opacity = op;
                l.style.translate = new Translate(Length.Percent(-50), Length.Percent(ty));
                l.style.scale = Vector2.one * sc;
            }, () => l.RemoveFromHierarchy());
        }

        public void Wipe(Action fn, float delay)
        {
            _wipe.pickingMode = PickingMode.Position;
            Tw.Run(_wipe, .38f, u => { _wipeR = Tweens.Bezier(.6f, 0, .4f, 1, u); _wipe.MarkDirtyRepaint(); }, null);
            _g.StartCoroutine(WipeRoutine(fn, delay));
        }

        private System.Collections.IEnumerator WipeRoutine(Action fn, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            fn();
            yield return null;
            Tw.Run(_wipe, .38f, u => { _wipeR = 1 - Tweens.Bezier(.6f, 0, .4f, 1, u); _wipe.MarkDirtyRepaint(); }, () => _wipe.pickingMode = PickingMode.Ignore);
        }

        public void Update(float dt)
        {
            Tw.Update(dt);
            StepIntro(dt);
        }
    }
}
