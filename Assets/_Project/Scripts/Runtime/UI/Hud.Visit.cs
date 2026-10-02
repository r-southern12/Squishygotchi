using System;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>The card shown while visiting a friend's steamer: who you're visiting, the things to do, and buttons.</summary>
    public sealed partial class Hud
    {
        /// <summary>One thing to do on a visit: what it is, how (for the ones without a button), and whether it's done.</summary>
        public struct VisitTodo
        {
            public string text, hint;
            public bool done;
            public VisitTodo(string text, string hint, bool done) { this.text = text; this.hint = hint; this.done = done; }
        }

        private Frame _visit;
        private Label _vTitle, _vCount;
        private VisualElement _vBtns, _vList, _vChev;
        private bool _vOpen = true, _visitUnderMemo;
        private const string VisitListKey = "visit_list_open";

        private void BuildVisit(float safeBottom)
        {
            _visit = CardShell(safeBottom);
            var tag = new Frame().Set(C("#6E9C9A"), 99).Pad(4, 12, 4, 12).In(_visit);
            tag.style.alignSelf = Align.Center;
            Label(tag, "VISITING", "Figtree", 700, 12, Cream).style.letterSpacing = 1.2f;
            _vTitle = Label(_visit, "", "Gluten", 800, 22).Margin(6, 0, 10, 0);
            _vTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _vTitle.Wrap();

            // The things to do (user request, 2 Oct 2026): a list with tick discs, folded away with a tap on its header.
            var head = new Frame().Set(C("#EFE4D3"), 14).Row().Pad(8, 12, 8, 12).In(_visit);
            head.style.alignItems = Align.Center;
            head.pickingMode = PickingMode.Position;
            var ht = Label(head, "Things to do", "Gluten", 800, 15);
            ht.style.flexGrow = 1;
            _vCount = Label(head, "", "Figtree", 700, 13, Muted).Margin(0, 8, 0, 0);
            _vChev = Icons.Make("chevron", 18);
            head.Add(_vChev);
            head.AddManipulator(new Clickable(() =>
            {
                _vOpen = !_vOpen;
                try { PlayerPrefs.SetInt(VisitListKey, _vOpen ? 1 : 0); } catch { }
                ApplyVisitList();
            }));
            _vList = new VisualElement().Col().Margin(6, 6, 0, 6).In(_visit);
            try { _vOpen = PlayerPrefs.GetInt(VisitListKey, 1) == 1; } catch { }

            _vBtns = new VisualElement().Row().Margin(12, 0, 0, 0).In(_visit);
            _vBtns.style.flexWrap = Wrap.Wrap; // more things to do than fit one row
            _vBtns.style.justifyContent = Justify.Center;
        }

        public void ShowVisit(string title, VisitTodo[] todos, params (string label, string bg, string shadow, string color, Action act)[] buttons)
        {
            _vTitle.text = title;
            SetVisitTodos(todos);
            _vBtns.Clear();
            foreach (var b in buttons)
            {
                var btn = Button(_vBtns, b.label, b.bg, b.shadow, b.color, 16, 44, 15, b.act, false, 4);
                btn.style.flexGrow = 1;
                btn.style.flexBasis = Length.Percent(30);
                btn.style.marginLeft = btn.style.marginRight = 4;
                btn.style.marginBottom = 8;
            }
            _visitUnderMemo = false;
            _visit.BringToFront();
            PopIn(_visit, .45f, .2f, 1.6f, .4f, 1, 50, .8f);
        }

        /// <summary>Redraws the list: a green disc with a tick for each thing done, an empty ring for the rest.</summary>
        public void SetVisitTodos(VisitTodo[] todos)
        {
            if (_vList == null) return;
            _vList.Clear();
            int done = 0;
            foreach (var t in todos)
            {
                if (t.done) done++;
                var r = new VisualElement().Row().In(_vList);
                r.style.alignItems = Align.Center;
                r.style.minHeight = 30;
                var disc = new Frame().Size(22, 22).In(r);
                disc.style.flexShrink = 0;
                disc.style.alignItems = Align.Center;
                disc.style.justifyContent = Justify.Center;
                if (t.done) { disc.Set(C("#6F9A74"), 11); disc.Add(Icons.Make("check", 15, "#FFFFFF")); }
                else
                {
                    disc.style.borderTopWidth = disc.style.borderBottomWidth = disc.style.borderLeftWidth = disc.style.borderRightWidth = 2;
                    disc.style.borderTopColor = disc.style.borderBottomColor = disc.style.borderLeftColor = disc.style.borderRightColor = C("#CDB999");
                    disc.style.borderTopLeftRadius = disc.style.borderTopRightRadius = disc.style.borderBottomLeftRadius = disc.style.borderBottomRightRadius = 11;
                }
                var l = Label(r, t.text, "Figtree", t.done ? 600 : 700, 14, t.done ? "#4C7552" : Ink).Margin(0, 8, 0, 10);
                l.style.flexGrow = 1;
                l.style.flexShrink = 1;
                if (!t.done && !string.IsNullOrEmpty(t.hint))
                {
                    var h = Label(r, t.hint, "Figtree", 400, 12, Muted);
                    h.style.flexShrink = 0;
                }
            }
            _vCount.text = done == todos.Length ? "All done!" : done + " of " + todos.Length + " done";
            ApplyVisitList();
        }

        private void ApplyVisitList()
        {
            _vList.Shown(_vOpen);
            _vChev.style.rotate = new Rotate(_vOpen ? 0 : -90);
        }

        public void HideVisit() { _visitUnderMemo = false; _visit?.Shown(false); }
    }
}
