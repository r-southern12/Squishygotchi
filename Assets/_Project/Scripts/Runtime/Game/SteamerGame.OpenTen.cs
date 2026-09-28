using System.Collections;
using System.Collections.Generic;
using Squishy.Runtime.Three;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Open 10 at once: ten steamers (each with its own layers) roll with exactly the same odds and pity as opening
    /// them one by one. They burst open one after another (confetti, pops, a bigger bang for Epic and Legendary),
    /// then every prize is listed best first, with a spinning 3D showcase of whichever you tap (tap it again to
    /// enlarge) and a coin to collect for each duplicate.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private const int OpenMany = 10;
        private bool tenLive;
        private readonly List<(RewardCard card, bool paid)> tenCards = new List<(RewardCard, bool)>();

        public void OpenTen()
        {
            if (mode != "unbox" || !(ustate == "closed" || ustate == "card")) return;
            if (S.steamers < OpenMany) { Floater("You need " + OpenMany + " steamers (you have " + S.steamers + ")", "bad"); sfx.Bonk(); return; }
            // Finish the steamer on the table first: its unopened layers are yours.
            if (ustate == "card")
            {
                CollectCardCoins();
                if (layer > 0) ClaimRemainingLayers();
                ui.HideCard();
                raysOn = 0;
                UMove("closed", reduce ? .3f : .8f);
                StartSwap(); // a fresh, closed steamer back on the table
            }
            ClearPrize();
            plate.gameObject.SetActive(false);
            newbie.Pivot.gameObject.SetActive(false);
            prize.gameObject.SetActive(false);

            // Roll everything up front (same rules as one at a time); the show below just reveals it.
            var perSteamer = new List<List<RewardCard>>();
            bool kitchen = false;
            for (int n = 0; n < OpenMany; n++)
            {
                Rules.SetSteamers(S.steamers - 1);
                var these = new List<RewardCard>();
                int layersHere = Rules.RollLayers();
                for (int l = 0; l < layersHere; l++)
                {
                    var c = Rules.Claim(Rules.RollReward());
                    these.Add(c);
                    if (c.grewTo >= 0) grewTo = c.grewTo;
                    kitchen |= c.kitchenChanged;
                }
                perSteamer.Add(these);
            }
            if (kitchen) DecorateStoves();
            TaskEvent("unbox", OpenMany);
            WriteSave();
            UpdatePity();
            StartCoroutine(OpenTenShow(perSteamer));
        }

        /// <summary>The ten bursting open in turn, then the results.</summary>
        private IEnumerator OpenTenShow(List<List<RewardCard>> perSteamer)
        {
            ustate = "openten";
            ui.ShowHud(false);
            yield return new WaitForSecondsRealtime(.25f);
            for (int n = 0; n < perSteamer.Count; n++)
            {
                int best = 0;
                foreach (var c in perSteamer[n]) best = Mathf.Max(best, GameContent.RarityRank(c.tier.Split(' ')[0]));
                shake = best >= 2 ? .8f : .35f;
                flash = best >= 2 ? .8f : .3f;
                sfx.Pop();
                if (best >= 2) Later(.08f, () => sfx.Chime());
                Buzz(best >= 2 ? 40 : 12);
                TenBurst(best >= 3 ? 70 : best >= 2 ? 45 : 22);
                ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), (n + 1) + " / " + OpenMany + (best >= 3 ? "  Legendary!" : best >= 2 ? "  Epic!" : ""));
                yield return new WaitForSecondsRealtime(best >= 2 ? .5f : .26f);
            }
            yield return new WaitForSecondsRealtime(.35f);
            var cards = new List<RewardCard>();
            foreach (var s in perSteamer) cards.AddRange(s);
            ShowManyResults(cards);
        }

        private void TenBurst(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rnd(0, Mathf.PI * 2), sp = Rnd(1.5f, 4.5f);
                confetti.Spawn(new Vector3(Rnd(-.2f, .2f), (H * layers + .5f) * US, Rnd(-.2f, .2f)), new Vector3(Mathf.Cos(a) * sp * .55f, Rnd(3.2f, 6), Mathf.Sin(a) * sp * .55f), 1, Rnd(1.8f, 2.6f), 1.1f, -5, .01f,
                    new Vector3(Rnd(0, 6), Rnd(0, 6), Rnd(0, 6)), new Vector3(Rnd(-10, 10), Rnd(-10, 10), Rnd(-10, 10)), ThreeMat.Lin(Conf[i % Conf.Length]));
            }
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                UPuffL(new Vector3(Mathf.Cos(a) * R * .6f, H * layers + .3f, Mathf.Sin(a) * R * .6f), new Vector3(Mathf.Cos(a) * 2, 3, Mathf.Sin(a) * 2), Rnd(.25f, .4f), Rnd(.7f, 1.1f), 1.8f, .6f);
            }
        }

        private void ShowManyResults(List<RewardCard> cards)
        {
            ustate = "closed";
            ui.ShowHud(true);
            // Best first: Legendary, Epic, Rare, then Common; new before duplicates.
            cards.Sort((a, b) =>
            {
                int ra = GameContent.RarityRank(a.tier.Split(' ')[0]), rb = GameContent.RarityRank(b.tier.Split(' ')[0]);
                if (ra != rb) return rb.CompareTo(ra);
                return b.isNew.CompareTo(a.isNew);
            });
            tenCards.Clear();
            foreach (var c in cards) tenCards.Add((c, c.delayedCoins <= 0));
            int fresh = cards.FindAll(c => c.isNew).Count, owed = 0;
            foreach (var c in cards) owed += c.delayedCoins;
            ui.SetPanelTitle("info", OpenMany + " steamers opened");
            ui.SetPanelSub("info", cards.Count + " prizes · " + fresh + " new" + (owed > 0 ? " · " + owed + " coins to collect" : ""));
            var body = ui.PanelBody("info");

            // The showcase: whichever prize is picked, turning slowly in 3D. Tap it to enlarge.
            var show = new Frame().Set(Css.C("#F4EBDD"), 20).Col(Align.Center).Pad(6, 6, 8, 6).In(body);
            var img = new Image { scaleMode = ScaleMode.ScaleToFit }.Size(null, 190).In(show);
            img.style.width = Length.Percent(100);
            var capName = Css.Label(show, "", "Gluten", 800, 17);
            var capTier = Css.Label(show, "", "Figtree", 600, 12, Hud.Muted);
            capName.style.unityTextAlign = TextAnchor.MiddleCenter;
            capTier.style.unityTextAlign = TextAnchor.MiddleCenter;
            bool big = false;
            show.pickingMode = PickingMode.Position;
            show.RegisterCallback<PointerUpEvent>(e => { big = !big; img.style.height = big ? 380 : 190; sfx.Tap(); });
            void Pick(RewardCard c)
            {
                img.image = Thumbs.LiveBegin(c.key);
                capName.text = c.name;
                capTier.text = c.tier + (c.isNew ? " · NEW" : " · duplicate") + " · tap to " + (big ? "shrink" : "enlarge");
                tenLive = img.image != null;
            }

            // Collect every duplicate coin in one go.
            if (owed > 0)
            {
                var all = Hud.Button(body, "Collect all " + owed + " coins", "#F2C94C", "#C99426", Hud.Ink, 99, 42, 15, null, false, 4);
                all.Margin(10, 0, 4, 0);
                all.Insert(0, Icons.Make("coin", 20));
                all.Row(Align.Center, Justify.Center);
                ((Label)all.userData).Margin(0, 0, 0, 8);
                all.AddManipulator(new Clickable(() => { CollectTen(-1); DrawTenList(body, Pick); }));
            }
            DrawTenList(body, Pick);
            if (cards.Count > 0) Pick(cards[0]);
            ui.OpenPanel("info");
        }

        private VisualElement tenList;

        private void DrawTenList(VisualElement body, System.Action<RewardCard> pick)
        {
            if (tenList == null || tenList.parent != body) { tenList = new VisualElement().In(body); }
            tenList.Clear();
            for (int i = 0; i < tenCards.Count; i++)
            {
                int idx = i;
                var c = tenCards[i].card;
                var row = new Frame().Set(Css.C("#FFF9EF"), 12).Row(Align.Center).Pad(4, 6, 4, 4).In(tenList);
                row.style.marginTop = 6;
                row.pickingMode = PickingMode.Position;
                row.AddManipulator(new Clickable(() => { pick(c); sfx.Tap(); }));
                var th = new Image { scaleMode = ScaleMode.ScaleToFit, image = Thumbs.Get(c.key) }.Size(46, 46).In(row);
                th.style.flexShrink = 0;
                th.pickingMode = PickingMode.Ignore;
                var col = new VisualElement().In(row);
                col.style.flexGrow = 1;
                col.style.flexShrink = 1;
                col.Margin(0, 0, 0, 8);
                col.pickingMode = PickingMode.Ignore;
                Css.Label(col, c.name, "Figtree", 700, 14).Wrap();
                Css.Label(col, c.tier, "Figtree", 600, 11.5f, Hud.Muted).Wrap();
                if (c.isNew) Hud.Button(row, "NEW", "#6F9A74", "#4C7552", Hud.Cream, 8, 24, 11, null, false, 2).Size(46, null);
                else if (c.delayedCoins > 0)
                {
                    bool paid = tenCards[i].paid;
                    var coin = Hud.Button(row, paid ? "Got it" : "+" + c.delayedCoins, paid ? "#EADCC6" : "#F2C94C", paid ? "#CDB999" : "#C99426", Hud.Ink, 99, 32, 13, null, false, 3);
                    coin.Row(Align.Center);
                    coin.Pad(0, 10, 0, 6);
                    if (!paid) coin.Insert(0, Icons.Make("coin", 18));
                    ((Label)coin.userData).Margin(0, 0, 0, paid ? 0 : 4);
                    if (!paid) coin.AddManipulator(new Clickable(() => { CollectTen(idx); DrawTenList(body, pick); }));
                }
            }
            var again = Hud.Button(tenList, S.steamers >= OpenMany ? "Open " + OpenMany + " more (" + S.steamers + ")" : "Done", "#D9A64A", "#A07324", Hud.Cream, 14, 44, 16, () =>
            {
                EndTenView();
                ui.ClosePanels();
                if (S.steamers >= OpenMany) OpenTen();
            }, false, 4);
            again.Margin(12, 0, 0, 0);
        }

        /// <summary>Pays one duplicate's coins (or all of them with -1), with a clink and a floater.</summary>
        private void CollectTen(int idx)
        {
            int total = 0;
            for (int i = 0; i < tenCards.Count; i++)
            {
                if ((idx >= 0 && i != idx) || tenCards[i].paid) continue;
                total += tenCards[i].card.delayedCoins;
                tenCards[i] = (tenCards[i].card, true);
            }
            if (total <= 0) return;
            AddCoins(total);
            ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .35f), "+" + total + " coins");
            Buzz(10, 20, 10);
            WriteSave();
        }

        /// <summary>Leaving the results: the 3D view stops and any coins not yet tapped are collected anyway.</summary>
        private void EndTenView()
        {
            if (tenLive) { Thumbs.LiveEnd(); tenLive = false; }
            CollectTen(-1);
            tenCards.Clear();
        }
    }
}
