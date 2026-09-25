using System;
using System.Collections.Generic;
using Squishy.Runtime.Game;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    public sealed partial class Hud
    {
        public static readonly string[] PanelIds = { "cook", "odds", "shop", "tasks", "info" };
        private readonly Dictionary<string, Frame> _panels = new Dictionary<string, Frame>();
        private readonly Dictionary<string, Label> _panelTitle = new Dictionary<string, Label>(), _panelSub = new Dictionary<string, Label>();
        private readonly Dictionary<string, ScrollView> _panelBody = new Dictionary<string, ScrollView>();
        private Frame _card, _memo, _cNew, _cDot;
        private Label _cNewTx, _cName, _cTier, _cMeta, _cAgain, _mName, _mMeta;
        private bool _sheetOpen;
        private readonly List<(string key, Image img)> _thumbQ = new List<(string, Image)>();

        // Catalogue sheet
        public VisualElement Sheet, Tabs, Chips, PvBar, Detail, DetailThumb;
        public ScrollView Grid, ChipScroll;
        public Label CatTitle, CatCount, PvNote, DName, DMeta, DNote, PvBtnLbl;
        public Frame PvBtn, DAct;
        public Label DActLbl;

        public bool SheetOpen { get { return _sheetOpen; } }

        // ---------------- panels ----------------

        private void BuildPanels(float safeBottom)
        {
            foreach (var id in PanelIds)
            {
                var pn = new Frame().Set(C("#FFF9EF"), 24, new Shadow(0, 18, 36, -10, C("rgba(60,30,15,.4)")), new Shadow(0, 5, 0, 0, C("rgba(90,60,40,.2)")))
                    .Abs(10, null, 10, 10 + safeBottom).Col().In(Root);
                pn.pickingMode = PickingMode.Position;
                pn.style.maxHeight = Length.Percent(72);
                var head = new VisualElement().Row(Align.FlexStart, Justify.SpaceBetween).Pad(14, 14, 8, 14).In(pn);
                head.style.flexShrink = 0;
                var hx = new VisualElement().In(head);
                hx.style.flexShrink = 1;
                _panelTitle[id] = Label(hx, "", "Gluten", 800, 22);
                _panelSub[id] = Label(hx, "", "Figtree", 600, 12, Muted);
                _panelSub[id].Wrap();
                string pid = id;
                IconBtn(head, "close", 36, 18, -1, () => { ClosePanel(pid); _g.OnPanelClosed(pid); }, out _);
                head.Gap(8);
                var body = new ScrollView(ScrollViewMode.Vertical).In(pn);
                body.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                body.contentContainer.Pad(0, 12, 14, 12);
                body.style.flexShrink = 1;
                _panelBody[id] = body;
                _panels[id] = pn;
                pn.Shown(false);
            }
            _panelTitle["shop"].text = "Shop";
            _panelTitle["tasks"].text = "Care tasks";
            _panelTitle["cook"].text = "Cook";
            _panelTitle["odds"].text = "Steamer odds";
            _panelTitle["info"].text = "Comfort";
            // Odds (static text, like the prototype's table).
            var ob = _panelBody["odds"];
            foreach (var (tier, pct) in new[] { ("Common", "76%"), ("Rare", "18%"), ("Epic", "5%"), ("Legendary", "1%") })
            {
                var tr = new VisualElement().Row(Align.Center, Justify.SpaceBetween).Pad(6, 4, 6, 4).In(ob);
                tr.style.borderBottomWidth = 1;
                tr.style.borderBottomColor = C("#EADCC6");
                Label(tr, tier, "Figtree", 400, 14);
                Label(tr, pct, "Figtree", 700, 14);
            }
            Para(ob, "Rare or better is guaranteed at least once every 10 steamers, and Epic or better once every 50.", 12.5f, "#6F5F52").Margin(4, 0, 0, 0);
            Para(ob, "Inside: furniture about 55%, ingredients 23%, squishies 14%, kitchen tools 6%, steamer skins 2%. A squishy has a 40% chance to be a copy of your favourite, which helps it grow.", 12.5f, "#6F5F52").Margin(4, 0, 0, 0);
        }

        public VisualElement PanelBody(string id) { var b = _panelBody[id]; b.Clear(); return b.contentContainer; }
        public void SetPanelTitle(string id, string title) { _panelTitle[id].text = title; }
        public void SetPanelSub(string id, string sub) { _panelSub[id].text = sub; }
        public bool PanelOpen(string id) { return _panels[id].style.display != DisplayStyle.None; }

        public void OpenPanel(string id)
        {
            foreach (var k in PanelIds) if (k != id) _panels[k].Shown(false);
            var pn = _panels[id];
            pn.Shown(true);
            Tw.Run(pn, .3f, u =>
            {
                float e = Tweens.Bezier(.2f, 1.2f, .4f, 1, u);
                pn.style.opacity = Mathf.Clamp01(e);
                pn.style.translate = new Translate(0, 40 * (1 - e));
            });
        }

        public void ClosePanel(string id) { _panels[id].Shown(false); }
        public void ClosePanels() { foreach (var k in PanelIds) _panels[k].Shown(false); }

        public void SetPity(string text, string oddsBtn) { _panelSub["odds"].text = text; _oddsBtnLbl.text = oddsBtn; }

        public static Label Para(VisualElement parent, string text, float size = 13.5f, string color = "#4F4036")
        {
            var l = Label(parent, text, "Figtree", 400, size, color);
            l.Wrap();
            l.Margin(0, 0, 8, 0);
            return l;
        }

        public static Label Sec(VisualElement parent, string text)
        {
            var l = Label(parent, text, "Gluten", 800, 16);
            l.Margin(6, 2, 0, 2);
            return l;
        }

        /// <summary>Primary button (.card .go / .explain .go / .rec button styles).</summary>
        public static Frame Button(VisualElement parent, string text, string bg, string shadow, string color, float radius, float height, float fontSize, Action onClick, bool disabled = false, float shadowY = 3)
        {
            var b = new Frame().In(parent);
            if (height > 0) b.style.height = height;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            var lbl = Label(b, text, "Gluten", 800, fontSize, color);
            b.userData = lbl;
            StyleButton(b, bg, shadow, color, radius, disabled, shadowY);
            b.pickingMode = PickingMode.Position;
            b.AddManipulator(new Clickable(() => { if (!(b.userData is Label) || b.enabledSelf) onClick?.Invoke(); }));
            return b;
        }

        public static void StyleButton(Frame b, string bg, string shadow, string color, float radius, bool disabled, float shadowY = 3)
        {
            b.Set(C(disabled ? "#D9CDBB" : bg), radius, new Shadow(0, shadowY, 0, 0, C(disabled ? "#BFAF97" : shadow)));
            ((Label)b.userData).style.color = C(disabled ? "#7A6656" : color);
            b.SetEnabled(!disabled);
        }

        /// <summary>A .rec row: 52px thumbnail, title/sub/extra, action button.</summary>
        public VisualElement Rec(VisualElement parent, string thumbKey, string title, string sub, string btn, Action onClick, bool disabled, VisualElement extra = null, bool noImage = false)
        {
            var r = new Frame().Set(C("#F4EBDD"), 16).Row().Pad(10, 10, 10, 10).In(parent);
            r.pickingMode = PickingMode.Position;
            if (!noImage)
            {
                var img = new Image { scaleMode = ScaleMode.ScaleToFit }.Size(52, 52).In(r);
                img.style.flexShrink = 0;
                if (thumbKey != null) img.image = Thumbs.Get(thumbKey);
            }
            var tx = new VisualElement().In(r);
            tx.style.flexGrow = 1;
            tx.style.flexShrink = 1;
            var b = Label(tx, title, "Figtree", 700, 14);
            b.Wrap();
            if (!string.IsNullOrEmpty(sub)) { var fx = Label(tx, sub, "Figtree", 600, 11, "#5F7F62"); fx.Wrap(); }
            if (extra != null) tx.Add(extra);
            if (btn != null)
            {
                var bt = Button(r, btn, "#C8674E", "#8E4332", Cream, 12, 0, 14, onClick, disabled).Pad(8, 12, 8, 12);
                bt.style.flexShrink = 0;
            }
            r.Gap(10);
            return r;
        }

        public static VisualElement Bar(float fraction, float height, string fill, float maxWidth, float marginTop)
        {
            var bar = new Frame().Set(C("#E4D6C1"), 99).Size(null, height).Margin(marginTop, 0, 0, 0);
            if (maxWidth > 0) bar.style.maxWidth = maxWidth;
            bar.style.overflow = Overflow.Hidden;
            var i = new Frame().Set(C(fill), 99).Abs(0, 0, null, 0).In(bar);
            i.style.width = Length.Percent(Mathf.Round(fraction * 100));
            return bar;
        }

        public static Label ReqChip(VisualElement parent, string text, bool miss)
        {
            var chip = new Frame().Set(C(miss ? "#F7DDD5" : "#E6F0E2"), 99).Pad(1, 7, 1, 7).In(parent);
            chip.style.marginRight = 4;
            chip.style.marginTop = 4;
            return Label(chip, text, "Figtree", 700, 10.5f, miss ? "#8E3322" : "#3F5F43");
        }

        // ---------------- cards ----------------

        private void BuildCards(float safeBottom)
        {
            _card = CardShell(safeBottom);
            _cNew = new Frame().Set(C("#C8674E"), 99).Pad(4, 10, 4, 10).In(_card);
            _cNew.style.alignSelf = Align.Center;
            _cNew.style.rotate = new Rotate(-3);
            _cNewTx = Label(_cNew, "NEW!", "Figtree", 700, 12, Cream);
            _cNewTx.style.letterSpacing = 1.2f;
            _cName = Label(_card, "Nebula", "Gluten", 800, 30).Margin(8, 0, 6, 0);
            _cName.style.unityTextAlign = TextAnchor.MiddleCenter;
            _cName.Wrap();
            var tier = new Frame().Set(C("#EADCC6"), 99).Row().Pad(4, 12, 4, 12).In(_card);
            tier.style.alignSelf = Align.Center;
            _cDot = new Frame().Set(C("#D8C7AE"), -1).Size(10, 10).In(tier);
            _cTier = Label(tier, "Galaxy tier", "Figtree", 700, 13).Margin(0, 0, 0, 6);
            _cMeta = Label(_card, "Added to your collection", "Figtree", 400, 13, Muted).Margin(8, 0, 12, 0);
            _cMeta.style.unityTextAlign = TextAnchor.MiddleCenter;
            _cMeta.Wrap();
            var btns = new VisualElement().Row().In(_card);
            var again = Button(btns, "Unbox again", "#EADCC6", "#CDB999", Ink, 16, 50, 17, () => _g.CardAgain(), false, 5);
            again.style.flexGrow = 1;
            again.style.flexBasis = 0;
            _cAgain = (Label)again.userData;
            var home = Button(btns, "Home", "#6F9A74", "#4C7552", Cream, 16, 50, 17, () => _g.GoHome(), false, 5);
            home.style.flexGrow = 1;
            home.style.flexBasis = 0;
            btns.Gap(10);

            _memo = CardShell(safeBottom);
            var tomb = new Frame { Fill = C("#A9A39C"), Corners = new Vector4(22, 22, 6, 6) }.Size(46, 56).Margin(0, 0, 4, 0).In(_memo);
            tomb.Shadows.Add(new Shadow(0, -6, 0, 0, C("#8F8983"), true));
            tomb.style.alignSelf = Align.Center;
            _mName = Label(_memo, "", "Gluten", 800, 26).Margin(8, 0, 6, 0);
            _mName.style.unityTextAlign = TextAnchor.MiddleCenter;
            _mName.Wrap();
            _mMeta = Label(_memo, "", "Figtree", 400, 13, Muted).Margin(8, 0, 12, 0);
            _mMeta.style.unityTextAlign = TextAnchor.MiddleCenter;
            _mMeta.Wrap();
            Button(_memo, "Choose next squishy", "#6F9A74", "#4C7552", Cream, 16, 50, 17, () => _g.NextSquishy(), false, 5);
        }

        private Frame CardShell(float safeBottom)
        {
            var c = new Frame().Set(C("#F7F0E4"), 26, new Shadow(0, 20, 40, -10, C("rgba(60,30,15,.45)")), new Shadow(0, 6, 0, 0, C("rgba(90,60,40,.25)")))
                .Abs(16, null, 16, 24 + safeBottom).Col().Pad(18, 18, 16, 18).In(Root);
            c.pickingMode = PickingMode.Position;
            c.Shown(false);
            return c;
        }

        private void PopIn(VisualElement el, float dur, float x1, float y1, float x2, float y2, float fromY, float fromScale)
        {
            el.Shown(true);
            Tw.Run(el, dur, u =>
            {
                float e = Tweens.Bezier(x1, y1, x2, y2, u);
                el.style.opacity = Mathf.Clamp01(e);
                el.style.translate = new Translate(0, fromY * (1 - e));
                el.style.scale = Vector2.one * Mathf.LerpUnclamped(fromScale, 1, e);
            });
        }

        public void ShowCard(bool isNew, string name, string tier, string dot, string meta, string again)
        {
            _cNewTx.text = isNew ? "NEW!" : "DUPLICATE";
            _cNew.Fill = C(isNew ? "#C8674E" : "#8C7BB0");
            _cNew.MarkDirtyRepaint();
            _cName.text = name;
            _cTier.text = tier;
            _cDot.Fill = string.IsNullOrEmpty(dot) ? C("#D8C7AE") : C(dot);
            _cDot.MarkDirtyRepaint();
            _cMeta.text = meta;
            _cAgain.text = again;
            PopIn(_card, .55f, .2f, 1.6f, .4f, 1, 60, .7f);
        }

        public void HideCard() { _card.Shown(false); }

        public void ShowMemo(string name, string meta)
        {
            _mName.text = name;
            _mMeta.text = meta;
            PopIn(_memo, .55f, .2f, 1.6f, .4f, 1, 60, .7f);
        }

        public void HideMemo() { _memo.Shown(false); }

        // ---------------- catalogue sheet ----------------

        private void BuildSheet(float safeTop, float safeBottom)
        {
            Sheet = new Frame().Set(C("#F4EBDD"), 0).Abs(0, 0, 0, 0).Col().In(Root);
            Sheet.pickingMode = PickingMode.Position;
            var head = new VisualElement().Row(Align.Center, Justify.SpaceBetween).Pad(16 + safeTop, 14, 8, 14).In(Sheet);
            var hx = new VisualElement().In(head);
            CatTitle = Label(hx, "Catalogue", "Gluten", 800, 24);
            CatCount = Label(hx, "", "Figtree", 700, 12, Muted);
            IconBtn(head, "close", 36, 18, -1, () => _g.OnCatClose(), out _);
            Tabs = new VisualElement().Row().Pad(4, 14, 8, 14).In(Sheet);
            Tabs.style.flexWrap = Wrap.Wrap;
            ChipScroll = new ScrollView(ScrollViewMode.Horizontal).In(Sheet);
            ChipScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            ChipScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            ChipScroll.contentContainer.Row().Pad(0, 14, 8, 14);
            ChipScroll.style.flexShrink = 0;
            Chips = ChipScroll.contentContainer;
            PvBar = new VisualElement().Row().Pad(0, 14, 8, 14).In(Sheet);
            PvBtn = new Frame().Set(C("#D9A64A"), 12, new Shadow(0, 3, 0, 0, C("#A07324"))).Pad(8, 12, 8, 12).In(PvBar);
            PvBtnLbl = Label(PvBtn, "Preview in my room", "Figtree", 700, 13, Cream);
            Tap(PvBtn, () => _g.OnPreview());
            PvNote = Label(PvBar, "", "Figtree", 400, 12, Muted).Margin(0, 0, 0, 8);
            Grid = new ScrollView(ScrollViewMode.Vertical).In(Sheet);
            Grid.style.flexGrow = 1;
            Grid.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            Grid.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Grid.contentContainer.Pad(4, 14, 150, 14);

            Detail = new Frame().Set(C("#FFF9EF"), 22, new Shadow(0, 16, 30, -10, C("rgba(60,30,15,.35)")), new Shadow(0, 5, 0, 0, C("rgba(90,60,40,.2)")))
                .Abs(10, null, 10, 10 + safeBottom).Row().Pad(12, 12, 12, 12).In(Sheet);
            Detail.pickingMode = PickingMode.Position;
            DetailThumb = new Frame().Set(C("#F0E5D3"), 14).Size(86, 86).In(Detail);
            DetailThumb.style.flexShrink = 0;
            DetailThumb.style.alignItems = Align.Center;
            DetailThumb.style.justifyContent = Justify.Center;
            DetailThumb.style.overflow = Overflow.Hidden;
            var tx = new VisualElement().Col().In(Detail);
            tx.style.flexGrow = 1;
            tx.style.flexShrink = 1;
            DName = Label(tx, "", "Gluten", 800, 19);
            DName.Wrap();
            DMeta = Label(tx, "", "Figtree", 400, 12, "#6F5F52");
            DMeta.Wrap();
            DNote = Label(tx, "", "Figtree", 400, 12, "#6F5F52");
            DNote.Wrap();
            DAct = Button(tx, "", "#6F9A74", "#4C7552", Cream, 12, 0, 14, () => _g.OnDetailAct()).Pad(7, 12, 7, 12);
            DAct.style.alignSelf = Align.FlexStart;
            DAct.style.marginTop = 4;
            DActLbl = (Label)DAct.userData;
            tx.Gap(3);
            Detail.Gap(12);
            Detail.Shown(false);
            Sheet.Shown(false);
        }

        public void OpenSheet()
        {
            _sheetOpen = true;
            Sheet.Shown(true);
            Tw.Run(Sheet, .35f, u =>
            {
                float e = Tweens.Bezier(.2f, 1.2f, .4f, 1, u);
                Sheet.style.opacity = Mathf.Clamp01(e);
                Sheet.style.translate = new Translate(0, 40 * (1 - e));
            });
        }

        public void CloseSheet()
        {
            _sheetOpen = false;
            Sheet.Shown(false);
            Detail.Shown(false);
            _thumbQ.Clear();
        }

        /// <summary>A catalogue tab button.</summary>
        public Frame Tab(string label, bool selected, Action onClick)
        {
            var b = new Frame().Set(C(selected ? Ink : "#EADCC6"), 12).Pad(7, 12, 7, 12).In(Tabs);
            b.style.marginRight = 6;
            b.style.marginBottom = 6;
            Label(b, label, "Figtree", 700, 13, selected ? Cream : Ink);
            return Tap(b, onClick);
        }

        /// <summary>A style chip with its diagonal two-colour swatch.</summary>
        public Frame StyleChip(string label, string col, string col2, bool pressed, Action onClick)
        {
            var b = new Frame().Set(C(pressed ? Ink : "#FBF6EE"), -1, new Shadow(0, 0, 0, 1, C(pressed ? Ink : "#DCCBB2"))).Row().Pad(4, 10, 4, col != null ? 5 : 10).In(Chips);
            b.style.marginRight = 6;
            b.style.flexShrink = 0;
            if (col != null)
            {
                var sw = new Glyph(16, (p, k) =>
                {
                    float r = 8;
                    var c = new Vector2(8, 8);
                    p.fillColor = C(col2);
                    p.BeginPath(); p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    p.fillColor = C(col);
                    p.BeginPath(); p.Arc(c, r, Angle.Degrees(135), Angle.Degrees(315)); p.ClosePath(); p.Fill();
                }, 16).In(b);
                sw.style.marginRight = 6;
            }
            Label(b, label, "Figtree", 700, 12, pressed ? Cream : Ink);
            return Tap(b, onClick);
        }

        /// <summary>A catalogue grid cell (.it).</summary>
        public Frame Cell(VisualElement row, string key, string name, string rarity, bool own, bool sel, (string a, string b, string t)? swatch, Action onClick)
        {
            var b = new Frame().Set(C("#FBF6EE"), 16, sel ? new[] { new Shadow(0, 0, 0, 3, C(Ink)) } : new[] { new Shadow(0, 2, 0, 0, C("#E2D3BC")) }).Col(Align.Center).Pad(6, 6, 8, 6).In(row);
            b.style.flexGrow = 1;
            b.style.flexBasis = 0;
            b.style.minWidth = 0;
            var th = new Frame().Set(C("#F0E5D3"), 12).In(b);
            th.style.width = Length.Percent(100);
            th.style.alignItems = Align.Center;
            th.style.justifyContent = Justify.Center;
            th.style.overflow = Overflow.Hidden;
            th.RegisterCallback<GeometryChangedEvent>(e => th.style.height = th.layout.width);
            if (!own) th.style.opacity = .8f;
            if (swatch.HasValue) Swatch(th, swatch.Value.a, swatch.Value.b, swatch.Value.t, .62f);
            else
            {
                var img = new Image { scaleMode = ScaleMode.ScaleToFit }.In(th);
                img.style.width = img.style.height = Length.Percent(100);
                var cached = Thumbs.Cached(key);
                if (cached != null) img.image = cached; else _thumbQ.Add((key, img));
            }
            var nm = Label(b, name, "Figtree", 700, 11.5f);
            nm.Wrap();
            nm.style.minHeight = 28;
            nm.style.unityTextAlign = TextAnchor.MiddleCenter;
            string rc = rarity == "Common" || rarity == "Rare" || rarity == "Epic" || rarity == "Legendary" ? rarity : "Rare";
            string rbg = rc == "Common" ? "#E7DCCB" : rc == "Rare" ? "#CFE0F2" : rc == "Epic" ? "#E5D4F3" : "#F6E1A6";
            var sm = new Frame().Set(C(rbg), 99).Pad(1, 7, 1, 7).In(b);
            Label(sm, rarity, "Figtree", 700, 10);
            var badge = new Frame().Set(C(own ? "#6F9A74" : "#CDBA9E"), -1).Size(18, 18).Abs(null, 8, 8).In(b);
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            badge.Add(own ? Icons.Make("check", 11, "#FFFFFF") : Icons.Make("lock", 10));
            b.Gap(2);
            return Tap(b, onClick);
        }

        public static void SetCellSelected(Frame b, bool sel)
        {
            b.Shadows.Clear();
            b.Shadows.Add(sel ? new Shadow(0, 0, 0, 3, C(Ink)) : new Shadow(0, 2, 0, 0, C("#E2D3BC")));
            b.MarkDirtyRepaint();
        }

        /// <summary>Steamer skin swatch: vertical stripes in a circle with a trim ring.</summary>
        public static void Swatch(VisualElement parent, string a, string b, string t, float fraction)
        {
            var sw = new VisualElement().In(parent);
            sw.style.width = Length.Percent(fraction * 100);
            sw.RegisterCallback<GeometryChangedEvent>(e => sw.style.height = sw.layout.width);
            sw.generateVisualContent += m =>
            {
                var p = m.painter2D;
                float w = sw.layout.width, r = w / 2;
                if (w <= 0) return;
                var c = new Vector2(r, r);
                for (float x = 0; x < w; x += 8)
                {
                    // Clip each 8px stripe to the circle by filling a thin polygon along its chord.
                    float x0 = x, x1 = Mathf.Min(w, x + 8);
                    p.fillColor = C(((int)(x / 8)) % 2 == 0 ? a : b);
                    p.BeginPath();
                    int steps = 6;
                    for (int s = 0; s <= steps; s++) { float xx = Mathf.Lerp(x0, x1, s / (float)steps), dy = Mathf.Sqrt(Mathf.Max(0, r * r - (xx - r) * (xx - r))); if (s == 0) p.MoveTo(new Vector2(xx, r - dy)); else p.LineTo(new Vector2(xx, r - dy)); }
                    for (int s = steps; s >= 0; s--) { float xx = Mathf.Lerp(x0, x1, s / (float)steps), dy = Mathf.Sqrt(Mathf.Max(0, r * r - (xx - r) * (xx - r))); p.LineTo(new Vector2(xx, r + dy)); }
                    p.ClosePath();
                    p.Fill();
                }
                p.strokeColor = C(t);
                p.lineWidth = 4;
                p.BeginPath();
                p.Arc(c, r - 2, Angle.Degrees(0), Angle.Degrees(360));
                p.Stroke();
            };
        }

        public void PumpThumbs(int n)
        {
            for (int i = 0; i < n && _thumbQ.Count > 0; i++)
            {
                var q = _thumbQ[0];
                _thumbQ.RemoveAt(0);
                if (q.img.panel != null && q.img.image == null) q.img.image = Thumbs.Get(q.key);
            }
        }

        public void ClearThumbQueue() { _thumbQ.Clear(); }
    }
}
