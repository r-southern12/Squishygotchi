using System.Collections.Generic;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Open 10 at once: ten steamers (each with its own layers) roll with exactly the same odds and pity as opening
    /// them one by one, then every prize is listed, best first. For players with a pile of steamers.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private const int OpenMany = 10;

        public void OpenTen()
        {
            if (mode != "unbox" || !(ustate == "closed" || ustate == "card")) return;
            if (S.steamers < OpenMany) { Floater("You need " + OpenMany + " steamers (you have " + S.steamers + ")", "bad"); sfx.Bonk(); return; }
            // Finish the steamer on the table first: its unopened layers are yours.
            if (ustate == "card")
            {
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

            var cards = new List<RewardCard>();
            int coins = 0;
            bool kitchen = false;
            for (int n = 0; n < OpenMany; n++)
            {
                Rules.SetSteamers(S.steamers - 1);
                int layersHere = Rules.RollLayers();
                for (int l = 0; l < layersHere; l++)
                {
                    var c = Rules.Claim(Rules.RollReward());
                    cards.Add(c);
                    coins += c.delayedCoins;
                    if (c.grewTo >= 0) grewTo = c.grewTo;
                    kitchen |= c.kitchenChanged;
                }
            }
            if (coins > 0) AddCoins(coins);
            if (kitchen) DecorateStoves();
            WriteSave();
            UpdatePity();
            flash = .7f;
            shake = .6f;
            Buzz(30, 30, 60);
            sfx.Pop();
            Later(.35f, () => sfx.Chime());
            ShowManyResults(cards, coins);
        }

        private void ShowManyResults(List<RewardCard> cards, int coins)
        {
            // Best first: Legendary, Epic, Rare, then Common; new before duplicates.
            cards.Sort((a, b) =>
            {
                int ra = GameContent.RarityRank(a.tier.Split(' ')[0]), rb = GameContent.RarityRank(b.tier.Split(' ')[0]);
                if (ra != rb) return rb.CompareTo(ra);
                return b.isNew.CompareTo(a.isNew);
            });
            int fresh = cards.FindAll(c => c.isNew).Count;
            ui.SetPanelTitle("info", OpenMany + " steamers opened");
            ui.SetPanelSub("info", cards.Count + " prizes · " + fresh + " new" + (coins > 0 ? " · +" + coins + " coins from duplicates" : ""));
            var body = ui.PanelBody("info");
            foreach (var c in cards)
            {
                var row = new VisualElement().Row(Align.Center).Pad(6, 2, 6, 2).In(body);
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = Css.C("#EADCC6");
                var dot = new Frame().Set(Css.C(string.IsNullOrEmpty(c.dot) ? "#D8C7AE" : c.dot), -1).Size(22, 22).In(row);
                dot.style.flexShrink = 0;
                var col = new VisualElement().In(row);
                col.style.flexGrow = 1;
                col.style.flexShrink = 1;
                col.Margin(0, 0, 0, 10);
                Css.Label(col, c.name, "Figtree", 700, 14).Wrap();
                Css.Label(col, c.tier + (string.IsNullOrEmpty(c.meta) ? "" : " · " + c.meta), "Figtree", 600, 11.5f, Hud.Muted).Wrap();
                if (c.isNew) Hud.Button(row, "NEW", "#6F9A74", "#4C7552", Hud.Cream, 8, 22, 11, null, false, 2).Size(46, null);
            }
            Hud.Button(body, S.steamers >= OpenMany ? "Open " + OpenMany + " more (" + S.steamers + ")" : "Done", "#D9A64A", "#A07324", Hud.Cream, 14, 44, 16, () =>
            {
                ui.ClosePanels();
                if (S.steamers >= OpenMany) OpenTen();
            }, false, 4).Margin(12, 0, 0, 0);
            ui.OpenPanel("info");
        }
    }
}
