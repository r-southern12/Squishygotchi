using System;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>The card shown while visiting a friend's steamer: who you're visiting, what you've done, and buttons.</summary>
    public sealed partial class Hud
    {
        private Frame _visit;
        private Label _vTitle, _vSub;
        private VisualElement _vBtns;

        private void BuildVisit(float safeBottom)
        {
            _visit = CardShell(safeBottom);
            var tag = new Frame().Set(C("#6E9C9A"), 99).Pad(4, 12, 4, 12).In(_visit);
            tag.style.alignSelf = Align.Center;
            Label(tag, "VISITING", "Figtree", 700, 12, Cream).style.letterSpacing = 1.2f;
            _vTitle = Label(_visit, "", "Gluten", 800, 22).Margin(6, 0, 2, 0);
            _vTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _vTitle.Wrap();
            _vSub = Label(_visit, "", "Figtree", 400, 13, Muted).Margin(0, 0, 10, 0);
            _vSub.style.unityTextAlign = TextAnchor.MiddleCenter;
            _vSub.Wrap();
            _vBtns = new VisualElement().Row().In(_visit);
        }

        public void ShowVisit(string title, string sub, params (string label, string bg, string shadow, string color, Action act)[] buttons)
        {
            _vTitle.text = title;
            _vSub.text = sub;
            _vBtns.Clear();
            foreach (var b in buttons)
            {
                var btn = Button(_vBtns, b.label, b.bg, b.shadow, b.color, 16, 46, 16, b.act, false, 4);
                btn.style.flexGrow = 1;
                btn.style.flexBasis = 0;
            }
            _vBtns.Gap(10);
            _visit.BringToFront();
            PopIn(_visit, .45f, .2f, 1.6f, .4f, 1, 50, .8f);
        }

        public void SetVisitSub(string sub) { if (_vSub != null) _vSub.text = sub; }

        public void HideVisit() { _visit?.Shown(false); }
    }
}
