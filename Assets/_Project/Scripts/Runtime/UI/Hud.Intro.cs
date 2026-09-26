using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// The title screen (every launch: the live room slowly turning behind a bobbing logo and rising steam)
    /// and the welcome card (first launch, or "Edit profile": your name, a colour and your friend code).
    /// </summary>
    public sealed partial class Hud
    {
        public static readonly string[] AvatarColors = { "#C8674E", "#6F9A74", "#6E9C9A", "#D9A64A", "#8C7BB0", "#E08BA3" };

        private VisualElement _intro, _logo, _tapPill;
        private readonly List<Frame> _puffs = new List<Frame>();
        private float _introT, _introOut = -1;
        private Action _introDone;
        private Frame _welcome;
        private TextField _nameField;
        private Label _codeLbl, _welcomeTitle;
        private VisualElement _swatchRow;
        private string _pickedColor;
        private Action<string, string> _welcomeDone;

        public bool IntroOn { get { return _intro != null && _intro.style.display != DisplayStyle.None; } }

        private void BuildIntro(float safeTop, float safeBottom)
        {
            _intro = new VisualElement().Abs(0, 0, 0, 0).In(Root);
            _intro.pickingMode = PickingMode.Position;
            _intro.style.backgroundImage = new StyleBackground(Veil());
            _intro.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _intro.RegisterCallback<PointerUpEvent>(e => { if (_introOut < 0) _introOut = 0; });

            _logo = new VisualElement().Col(Align.Center).Abs(0, 70 + safeTop, 0).NoPick().In(_intro);
            var steam = new VisualElement().Row(Align.FlexEnd, Justify.Center).Size(null, 44).NoPick().In(_logo);
            for (int i = 0; i < 3; i++)
            {
                var p = new Frame().Set(C("#FFFFFF", .85f), -1).Size(14, 14).Margin(0, 6, 0, 6).NoPick().In(steam);
                _puffs.Add(p);
            }
            var a = Label(_logo, "Squishiotchi", "Gluten", 800, 50, "#C8674E");
            a.style.textShadow = Shadow(4, "#FFF7EC");
            a.style.rotate = new Rotate(-3);
            Label(_logo, "a tiny home in a bamboo steamer", "Figtree", 700, 14, Muted).Margin(10, 0, 0, 0);

            _tapPill = new Frame().Set(C("#FFF7EC"), 99, new Shadow(0, 4, 0, 0, C("rgba(90,60,40,.22)"))).Pad(12, 26, 12, 26)
                .Abs(null, null, null, 64 + safeBottom).NoPick().In(_intro);
            _tapPill.style.alignSelf = Align.Center;
            _intro.style.alignItems = Align.Center;
            Label(_tapPill, "Tap to start", "Gluten", 700, 20, "#C8674E");
            _intro.Shown(false);

            BuildWelcome(safeBottom);
        }

        /// <summary>Cream at the top and bottom, clear in the middle so the room shows through.</summary>
        private static Texture2D Veil()
        {
            var t = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var cream = C("#F7F0E4");
            for (int y = 0; y < 256; y++)
            {
                float v = y / 255f; // 0 = bottom
                float alpha = v > .55f ? Mathf.SmoothStep(0, .96f, (v - .55f) / .35f) : v < .28f ? Mathf.SmoothStep(0, .92f, (.28f - v) / .22f) : 0;
                t.SetPixel(0, y, new Color(cream.r, cream.g, cream.b, alpha));
            }
            t.Apply();
            return t;
        }

        public void ShowIntro(Action done)
        {
            _introDone = done;
            _introT = 0;
            _introOut = -1;
            _intro.style.opacity = 1;
            _intro.Shown(true);
            _intro.BringToFront();
        }

        private void StepIntro(float dt)
        {
            if (!IntroOn) return;
            _introT += dt;
            _logo.style.translate = new Translate(0, Mathf.Sin(_introT * 1.6f) * 5);
            _logo.style.scale = Vector2.one * (1 + .015f * Mathf.Sin(_introT * 3.2f));
            _tapPill.style.opacity = .65f + .35f * Mathf.Sin(_introT * 3);
            for (int i = 0; i < _puffs.Count; i++)
            {
                float u = Mathf.Repeat(_introT * .45f + i * .33f, 1);
                _puffs[i].style.translate = new Translate(Mathf.Sin(u * 6 + i) * 5, -u * 36);
                _puffs[i].style.scale = Vector2.one * (.6f + u * .9f);
                _puffs[i].style.opacity = Mathf.Sin(u * Mathf.PI);
            }
            if (_introOut < 0) return;
            _introOut += dt;
            float k = Mathf.Clamp01(_introOut / .5f);
            _intro.style.opacity = 1 - k;
            _logo.style.translate = new Translate(0, -40 * k * k);
            if (k >= 1)
            {
                _intro.Shown(false);
                var d = _introDone;
                _introDone = null;
                d?.Invoke();
            }
        }

        // ---------------- welcome / profile card ----------------

        private void BuildWelcome(float safeBottom)
        {
            _welcome = CardShell(safeBottom);
            var wave = new Frame().Set(C("#6E9C9A"), 99).Pad(4, 12, 4, 12).In(_welcome);
            wave.style.alignSelf = Align.Center;
            wave.style.rotate = new Rotate(-3);
            Label(wave, "HELLO!", "Figtree", 700, 12, Cream).style.letterSpacing = 1.2f;
            _welcomeTitle = Label(_welcome, "Welcome to the steamer", "Gluten", 800, 26).Margin(8, 0, 4, 0);
            _welcomeTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _welcomeTitle.Wrap();
            var q = Label(_welcome, "What should your squishy call you?", "Figtree", 400, 13, Muted).Margin(0, 0, 10, 0);
            q.style.unityTextAlign = TextAnchor.MiddleCenter;

            _nameField = new TextField { maxLength = 16 };
            _nameField.Text("Figtree", 700, 18, Ink);
            _nameField.style.marginLeft = _nameField.style.marginRight = 0;
            var input = _nameField.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = C("#FFF7EC");
                input.style.height = 46;
                input.style.borderTopLeftRadius = input.style.borderTopRightRadius = input.style.borderBottomLeftRadius = input.style.borderBottomRightRadius = 14;
                input.style.borderTopWidth = input.style.borderBottomWidth = input.style.borderLeftWidth = input.style.borderRightWidth = 2;
                input.style.borderTopColor = input.style.borderBottomColor = input.style.borderLeftColor = input.style.borderRightColor = C("#E6D4B8");
                input.style.paddingLeft = 14;
                input.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            _welcome.Add(_nameField);

            Label(_welcome, "Pick your colour", "Figtree", 700, 13, Muted).Margin(12, 0, 6, 0).style.alignSelf = Align.Center;
            _swatchRow = new VisualElement().Row(Align.Center, Justify.Center).In(_welcome);

            var codeChip = new Frame().Set(C("#EADCC6"), 99).Row().Pad(6, 14, 6, 14).Margin(14, 0, 0, 0).In(_welcome);
            codeChip.style.alignSelf = Align.Center;
            Label(codeChip, "Friend code ", "Figtree", 400, 13, Muted);
            _codeLbl = Label(codeChip, "", "Figtree", 700, 14, Ink);
            _codeLbl.style.letterSpacing = 1.5f;
            var fine = Label(_welcome, "Saved on this phone: no email, no password. Visiting friends with your code is coming soon.", "Figtree", 400, 11.5f, Muted).Margin(8, 0, 12, 0);
            fine.style.unityTextAlign = TextAnchor.MiddleCenter;
            fine.Wrap();
            Button(_welcome, "Let's go!", "#6F9A74", "#4C7552", Cream, 16, 50, 17, () =>
            {
                _welcome.Shown(false);
                var d = _welcomeDone;
                _welcomeDone = null;
                d?.Invoke(_nameField.value, _pickedColor);
            }, false, 5);
        }

        private void DrawSwatches()
        {
            _swatchRow.Clear();
            foreach (var col in AvatarColors)
            {
                string c = col;
                bool on = c == _pickedColor;
                var s = new Frame().Set(C(c), -1, on ? new[] { new Shadow(0, 0, 0, 3, C("#FFF7EC")), new Shadow(0, 0, 0, 6, C(c)) } : new Shadow[0])
                    .Size(34, 34).Margin(0, 7, 0, 7).In(_swatchRow);
                s.pickingMode = PickingMode.Position;
                Tap(s, () => { _pickedColor = c; DrawSwatches(); });
            }
        }

        /// <summary>Shows the welcome card (first launch) or the profile editor.</summary>
        public void ShowWelcome(bool edit, string name, string color, string code, Action<string, string> done)
        {
            _welcomeDone = done;
            _welcomeTitle.text = edit ? "Your profile" : "Welcome to the steamer";
            _nameField.value = name ?? "";
            _pickedColor = string.IsNullOrEmpty(color) ? AvatarColors[0] : color;
            _codeLbl.text = code;
            DrawSwatches();
            _welcome.BringToFront();
            PopIn(_welcome, .55f, .2f, 1.6f, .4f, 1, 60, .7f);
        }

        /// <summary>A round avatar: the player's colour with their initial.</summary>
        public static Frame Avatar(VisualElement parent, string name, string color, float size)
        {
            var f = new Frame().Set(C(string.IsNullOrEmpty(color) ? AvatarColors[0] : color), -1, new Shadow(0, 2, 0, 0, C("rgba(90,60,40,.2)"))).Size(size, size).In(parent);
            f.style.alignItems = Align.Center;
            f.style.justifyContent = Justify.Center;
            string initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            Label(f, initial, "Gluten", 800, size * .5f, Cream);
            return f;
        }
    }
}
