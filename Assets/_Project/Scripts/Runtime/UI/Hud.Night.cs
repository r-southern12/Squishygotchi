using System;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// Bedtime screens. The night screen: a starry sky, the squishy asleep, "sweet dreams". The wake-up screen, shown
    /// instead of the title screen after a night's sleep: a sunrise, how long it slept and the steamers collected.
    /// </summary>
    public sealed partial class Hud
    {
        private VisualElement _night, _wake, _nightArt, _wakeArt, _wakeSun;
        private Label _nightTitle, _nightSub, _wakeTitle, _wakeSub, _wakeGift;
        private VisualElement _wakeBtns;
        private float _nightT;

        public bool NightOn { get { return (_night != null && _night.style.display != DisplayStyle.None) || (_wake != null && _wake.style.display != DisplayStyle.None); } }

        private static Texture2D Sky(string top, string mid, string bottom)
        {
            var t = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color a = C(bottom), b = C(mid), c = C(top);
            for (int y = 0; y < 256; y++)
            {
                float v = y / 255f; // 0 = bottom
                t.SetPixel(0, y, v < .5f ? Color.Lerp(a, b, v * 2) : Color.Lerp(b, c, (v - .5f) * 2));
            }
            t.Apply();
            return t;
        }

        private void BuildNight(float safeTop, float safeBottom)
        {
            // Night: deep blue sky, a moon, stars, the squishy asleep.
            _night = new VisualElement().Abs(0, 0, 0, 0).Col(Align.Center).In(Root);
            _night.pickingMode = PickingMode.Position;
            _night.RegisterCallback<PointerUpEvent>(e => _g.OnNightTap());
            _night.style.backgroundImage = new StyleBackground(Sky("#1B1F4A", "#2E3470", "#5B4C84"));
            _night.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            var rnd = new System.Random(7);
            for (int i = 0; i < 40; i++)
            {
                float s = 2 + (float)rnd.NextDouble() * 3;
                var st = new Frame().Set(C("#FFF7D6", .5f + .5f * (float)rnd.NextDouble()), -1).Size(s, s).NoPick().In(_night);
                st.style.position = Position.Absolute;
                st.style.left = Length.Percent((float)rnd.NextDouble() * 100);
                st.style.top = Length.Percent((float)rnd.NextDouble() * 55);
            }
            var moon = new Frame().Set(C("#FFF1C2"), -1, new Shadow(0, 0, 40, 6, C("rgba(255,241,194,.45)"))).Size(70, 70).Abs(null, 70 + safeTop, 36).NoPick().In(_night);
            new Frame().Set(C("#2A2F66"), -1).Size(58, 58).Abs(18, -8).NoPick().In(moon); // crescent
            _nightArt = new VisualElement().Size(190, 190).Margin(170 + safeTop, 0, 0, 0).NoPick().In(_night);
            _nightArt.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _nightTitle = Label(_night, "", "Gluten", 800, 30, "#FFF7EC").Margin(10, 0, 0, 0);
            _nightTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _nightSub = Label(_night, "", "Figtree", 600, 14, "#D9D6F2").Margin(8, 28, 0, 28);
            _nightSub.style.unityTextAlign = TextAnchor.MiddleCenter;
            _nightSub.Wrap();
            _night.Shown(false);

            // Morning: a sunrise, the squishy waking up, what the night brought.
            _wake = new VisualElement().Abs(0, 0, 0, 0).Col(Align.Center).In(Root);
            _wake.pickingMode = PickingMode.Position;
            _wake.style.backgroundImage = new StyleBackground(Sky("#9FD2F0", "#FFD7A8", "#FFF3E0"));
            _wake.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _wakeSun = new Frame().Set(C("#FFD25E"), -1, new Shadow(0, 0, 60, 14, C("rgba(255,210,94,.5)"))).Size(120, 120).Abs(null, 110 + safeTop).NoPick().In(_wake);
            _wakeSun.style.left = Length.Percent(50);
            _wakeSun.style.marginLeft = -60;
            _wakeArt = new VisualElement().Size(200, 200).Margin(150 + safeTop, 0, 0, 0).NoPick().In(_wake);
            _wakeArt.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _wakeTitle = Label(_wake, "", "Gluten", 800, 34, "#C8674E").Margin(6, 0, 0, 0);
            _wakeTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _wakeTitle.style.textShadow = Shadow(3, "#FFF7EC");
            _wakeSub = Label(_wake, "", "Figtree", 700, 15, "#6F5F52").Margin(6, 28, 0, 28);
            _wakeSub.style.unityTextAlign = TextAnchor.MiddleCenter;
            _wakeSub.Wrap();
            var giftRow = new Frame().Set(C("#FFF7EC"), 99, new Shadow(0, 4, 0, 0, C("rgba(90,60,40,.18)"))).Row(Align.Center).Pad(8, 16, 8, 12).Margin(16, 0, 0, 0).NoPick().In(_wake);
            giftRow.Add(Icons.Make("gift", 22));
            _wakeGift = Label(giftRow, "", "Gluten", 700, 16, "#C8674E").Margin(0, 0, 0, 8);
            _wakeBtns = new VisualElement().Col(Align.Stretch).Abs(28, null, 28, 40 + safeBottom).In(_wake);
            _wake.Shown(false);
        }

        public void ShowNight(string title, string sub, Texture2D art)
        {
            _nightTitle.text = title;
            _nightSub.text = sub;
            _nightArt.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
            _night.Shown(true);
            _night.BringToFront();
            _nightT = 0;
        }

        public void HideNight() { _night.Shown(false); }

        public void ShowWake(string title, string sub, string gift, Texture2D art, params (string label, string bg, string shadow, string color, Action act)[] buttons)
        {
            _wakeTitle.text = title;
            _wakeSub.text = sub;
            _wakeGift.text = gift;
            _wakeGift.parent.Shown(!string.IsNullOrEmpty(gift));
            _wakeArt.style.backgroundImage = art != null ? new StyleBackground(art) : new StyleBackground(StyleKeyword.None);
            _wakeBtns.Clear();
            foreach (var b in buttons) Button(_wakeBtns, b.label, b.bg, b.shadow, b.color, 16, 52, 18, b.act, false, 5).Margin(8, 0, 0, 0);
            _wake.Shown(true);
            _wake.BringToFront();
            _nightT = 0;
        }

        public void HideWake() { _wake.Shown(false); }

        /// <summary>Gentle breathing for the sleeping squishy; the sun rises on the wake screen.</summary>
        private void StepNight(float dt)
        {
            if (!NightOn) return;
            _nightT += dt;
            float b = 1 + .025f * Mathf.Sin(_nightT * 1.6f);
            if (_night.style.display != DisplayStyle.None) _nightArt.style.scale = new Scale(new Vector3(b, 2 - b, 1));
            if (_wake.style.display != DisplayStyle.None)
            {
                float u = Mathf.Clamp01(_nightT / 2.5f);
                _wakeSun.style.translate = new Translate(0, 60 * (1 - Mathf.SmoothStep(0, 1, u)));
                _wakeArt.style.scale = new Scale(Vector3.one * (1 + .02f * Mathf.Sin(_nightT * 2.4f)));
            }
        }
    }
}
