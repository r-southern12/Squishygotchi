using System;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Hidden test tools (hold the coin counter for a second): set needs, skip time, speed up, kill, grow, age,
    /// add coins and steamers, reset. For playtesting life and death; remove or gate before release.
    /// </summary>
    public sealed partial class SteamerGame
    {
        public void OnAdmin()
        {
            if (mode != "home") return;
            if (!C.rules.adminTools && !Debug.isDebugBuild) return; // switched off for release in game_content.json
            ui.SetPanelTitle("info", "Admin (testing)");
            var body = ui.PanelBody("info");
            Hud.Para(body, "Hidden test tools. Hold the coin counter to open. Speed: " + timeScale + "x · Day " + S.age + " · death clock " + Mathf.RoundToInt(S.deathClock / 60) + " min.");
            Row(body, "Needs", ("Full", () => SetNeeds(1)), ("Half", () => SetNeeds(.5f)), ("Low", () => SetNeeds(.15f)), ("Empty", () => SetNeeds(0)));
            Row(body, "Skip time", ("+1 hour", () => Skip(1)), ("+6 hours", () => Skip(6)), ("+1 day", () => Skip(24)));
            Row(body, "Speed", (timeScale > 1 ? "Normal" : "60x", () => { timeScale = timeScale > 1 ? 1 : 60; OnAdmin(); }), ("Kill now", () => { ui.ClosePanels(); if (!S.dead) Die(); }));
            Row(body, "Wallet", ("+500 coins", () => { Rules.AddCoins(500); OnAdmin(); }), ("+5 steamers", () => { Rules.SetSteamers(S.steamers + 5); OnAdmin(); }));
            Row(body, "Squishy", ("Grow", Grow), ("Age +1 day", () => { S.age++; UpdateSub(); OnAdmin(); }));
            Row(body, "Friends", ("Test visit (my room)", TestVisit), ("Visit credit +1", () => { Rules.CreditVisit("test", DateTime.UtcNow.Ticks, 1); OnAdmin(); }));
            Row(body, "Life", ("Old age now", () => { ui.ClosePanels(); S.age = Mathf.CeilToInt(Rules.ExpectedLifespanDays()); }), ("+50 prestige", () => { S.prestige += 50; OnAdmin(); }));
            Row(body, "Unlock", (S.premium ? "Premium: on" : "Premium: off", () => { S.premium = !S.premium; paywallShown = false; OnAdmin(); }), ("Gift ready", () => { S.giftReadyAt = 0; OnAdmin(); }), ("End trial", () => { S.trialStart = 1; ui.ClosePanels(); }));
            Row(body, "Save", ("Reset game", ResetGame));
            ui.OpenPanel("info");
        }

        private static void Row(VisualElement body, string label, params (string text, Action act)[] buttons)
        {
            Css.Label(body, label, "Gluten", 800, 15).Margin(4, 0, 2, 0);
            var row = new VisualElement().Row().In(body);
            foreach (var (text, act) in buttons)
            {
                var b = Hud.Button(row, text, "#EADCC6", "#CDB999", Hud.Ink, 12, 38, 14, act);
                b.style.flexGrow = 1;
                b.style.flexBasis = 0;
            }
            row.Gap(6);
        }

        private void SetNeeds(float v)
        {
            for (int k = 0; k < 4; k++) S.needs[k] = v;
            DrawNeeds();
            OnAdmin();
        }

        /// <summary>Applies hours away exactly as if the app had been closed.</summary>
        private void Skip(float hours)
        {
            var now = DateTime.UtcNow;
            bool wasDead = S.dead;
            CatchUp(now.AddHours(-hours), now);
            DrawNeeds();
            UpdateSub();
            ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .4f), "+" + hours + "h");
            if (S.dead && !wasDead) { ui.ClosePanels(); S.dead = false; Die(); return; }
            OnAdmin();
        }

        private void Grow()
        {
            int si = Rules.FavSizeIdx;
            if (si + 1 >= C.sizes.Length) { ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .4f), "Already fully grown"); return; }
            Rules.SetSquish(S.favIdx, C.sizes[si + 1].at);
            growAnim = (pet.Scale, C.sizes[si + 1].s, 0);
            Floater("Grew to " + C.sizes[si + 1].name + "!");
            UpdateSub();
            OnAdmin();
        }

        private void ResetGame()
        {
            _save.state = GameRules.NewState(C, (ulong)DateTime.UtcNow.Ticks);
            _saves.Save(_save);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
