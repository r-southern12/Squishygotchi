using System;
using System.Collections.Generic;
using Squishy.Runtime.Game;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// A big celebration for the moments that matter (user request, 29 Sep 2026): the squishy growing a size, a recipe
    /// levelling up. The screen dims, a card bursts in with its picture, title and stars, confetti falls, and a Yay!
    /// button closes it. Celebrations queue up if two land together.
    /// </summary>
    public sealed partial class Hud
    {
        private VisualElement _party;
        private readonly Queue<Action> _partyQueue = new Queue<Action>();
        private static readonly string[] PartyColours = { "#F2C94C", "#E8828F", "#6FC3C9", "#8C7BB0", "#6F9A74", "#F29B4C" };

        public bool CelebrationOn { get { return _party != null; } }

        /// <summary>Shows a celebration: picture (thumbnail key), title, stars filled (0 for none) and a line saying what it means.</summary>
        public void Celebrate(string thumbKey, string title, int stars, string big, string line)
        {
            Action show = () => BuildParty(thumbKey, title, stars, big, line);
            if (_party != null) { _partyQueue.Enqueue(show); return; }
            show();
        }

        private void BuildParty(string thumbKey, string title, int stars, string big, string line)
        {
            _party = new VisualElement().Abs(0, 0, 0, 0).Col(Align.Center).In(Root);
            _party.style.justifyContent = Justify.Center;
            _party.style.backgroundColor = C("rgba(40,25,15,.5)");
            _party.pickingMode = PickingMode.Position;
            _party.BringToFront();
            var party = _party;

            // Confetti raining down behind the card.
            var rnd = new System.Random();
            for (int i = 0; i < 46; i++)
            {
                var bit = new Frame().Set(C(PartyColours[i % PartyColours.Length]), 3).Size(8 + rnd.Next(6), 12 + rnd.Next(8)).NoPick().In(party);
                bit.style.position = Position.Absolute;
                bit.style.left = Length.Percent(rnd.Next(100));
                bit.style.top = -30;
                float delay = (float)rnd.NextDouble() * .7f, spin = rnd.Next(-720, 720), sway = rnd.Next(-40, 40);
                float h = Height + 60;
                Tw.Run(bit, 2.2f + delay, u =>
                {
                    float t = Mathf.Clamp01((u * (2.2f + delay) - delay) / 2.2f);
                    bit.style.translate = new Translate(sway * Mathf.Sin(t * 6), t * h);
                    bit.style.rotate = new Rotate(spin * t);
                    bit.style.opacity = t < .85f ? 1 : (1 - t) / .15f;
                });
            }

            var card = new Frame().Set(C("#FFF9EF"), 28, new Shadow(0, 18, 40, -8, C("rgba(60,30,15,.45)"))).Col(Align.Center).Pad(20, 22, 18, 22).In(party);
            card.style.width = Length.Percent(84);
            card.style.maxWidth = 340;
            if (thumbKey != null)
            {
                var img = new Image { scaleMode = ScaleMode.ScaleToFit, image = Thumbs.Get(thumbKey) }.Size(150, 150).In(card);
                img.pickingMode = PickingMode.Ignore;
                Tw.Run(img, 1.2f, u => img.style.scale = new Scale(Vector3.one * (1 + .08f * Mathf.Sin(u * Mathf.PI * 3) * (1 - u))));
            }
            var t1 = Label(card, title, "Gluten", 800, 24, "#C8674E");
            t1.Wrap();
            t1.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (!string.IsNullOrEmpty(big))
            {
                var b = Label(card, big, "Gluten", 800, 34, Ink).Margin(4, 0, 0, 0);
                b.style.unityTextAlign = TextAnchor.MiddleCenter;
                Tw.Run(b, .6f, u => b.style.scale = new Scale(Vector3.one * (u < 1 ? .6f + .55f * Mathf.Sin(u * Mathf.PI * .5f) - .15f * u : 1)));
            }
            if (stars > 0)
            {
                // Stars pop in one after another.
                var row = new VisualElement().Row(Align.Center, Justify.Center).Margin(8, 0, 2, 0).In(card);
                for (int s = 0; s < 5; s++)
                {
                    var st = Icons.Make(s < stars ? "star" : "star_empty", 34, "#D9A64A");
                    st.Margin(0, 3, 0, 3);
                    row.Add(st);
                    if (s < stars)
                    {
                        float d = .15f + s * .18f;
                        st.style.scale = new Scale(Vector3.zero);
                        Tw.Run(st, d + .35f, u =>
                        {
                            float t = Mathf.Clamp01((u * (d + .35f) - d) / .35f);
                            st.style.scale = new Scale(Vector3.one * (t < 1 ? Mathf.Sin(t * Mathf.PI * .75f) * 1.35f : 1));
                        });
                    }
                }
            }
            var ln = Label(card, line, "Figtree", 600, 14, Muted).Margin(8, 0, 14, 0);
            ln.Wrap();
            ln.style.unityTextAlign = TextAnchor.MiddleCenter;
            var yay = Button(card, "Yay!", "#6F9A74", "#4C7552", Cream, 16, 50, 19, () => CloseParty(), false, 5);
            yay.style.alignSelf = Align.Stretch;
            PopIn(card, .55f, .2f, 1.6f, .4f, 1, 60, .7f);
        }

        private void CloseParty()
        {
            if (_party == null) return;
            var p = _party;
            _party = null;
            Tw.Run(p, .2f, u => p.style.opacity = 1 - u);
            p.schedule.Execute(() => p.RemoveFromHierarchy()).StartingIn(220);
            if (_partyQueue.Count > 0) { var next = _partyQueue.Dequeue(); p.schedule.Execute(() => next()).StartingIn(300); }
        }
    }
}
