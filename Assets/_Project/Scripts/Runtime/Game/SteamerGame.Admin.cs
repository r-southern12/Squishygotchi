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
            Hud.Para(body, "Hidden test tools. Hold the coin counter to open. Speed: " + timeScale + "x · " + (Rules.Protected ? "can't die" : "can die") + " · Day " + S.age + " · death clock " + Mathf.RoundToInt(S.deathClock / 60) + " min.");
            Row(body, "Sound", ("Sound Lab", OnSoundLab));
            Row(body, "Notifications (" + (Notifier.PermissionState() == "" ? "not a phone" : Notifier.PermissionState()) + ")", ("Test in 10 s", () => { Notifier.SendTest(Rules); ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .4f), "Sent: watch for it in 10 s"); }), ("Phone settings", Notifier.OpenSettings));
            Row(body, "Wallet", ("+500 coins", () => { Rules.AddCoins(500); UpdateSub(); OnAdmin(); }), ("+5000", () => { Rules.AddCoins(5000); UpdateSub(); OnAdmin(); }), ("+5 steamers", () => { Rules.SetSteamers(S.steamers + 5); OnAdmin(); }));
            Row(body, "Skip time", ("+1 hour", () => Skip(1)), ("+6 hours", () => Skip(6)), ("+1 day", () => Skip(24)), ("+1 week", () => Skip(168)));
            Row(body, "Needs", ("Full", () => SetNeeds(1)), ("Half", () => SetNeeds(.5f)), ("Low", () => SetNeeds(.15f)), ("Empty", () => SetNeeds(0)));
            Row(body, "Speed", (timeScale > 1 ? "Back to normal" : "Go 60x", () => { timeScale = timeScale > 1 ? 1 : 60; ui.SetHint(timeScale > 1 ? "Admin: 60x speed (1 min = 1 hour)" : ""); OnAdmin(); }));
            Row(body, "Death", (Rules.Protected ? "Protected: on" : "Protected: off", () => { Rules.Protected = !Rules.Protected; OnAdmin(); }), ("Kill now…", ConfirmKill));
            Row(body, "Squishy", ("Grow", Grow), ("Age +1 day", () => { S.age++; UpdateSub(); OnAdmin(); }));
            Row(body, "Friends", ("Test visit (my room)", TestVisit), ("Visit credit +1", () => { Rules.CreditVisit("test", DateTime.UtcNow.Ticks, 1); OnAdmin(); }));
            Row(body, "Life", ("Old age now", () => { ui.ClosePanels(); S.age = Mathf.CeilToInt(Rules.ExpectedLifespanDays()); }), ("+50 prestige", () => { S.prestige += 50; OnAdmin(); }));
            Row(body, "Unlock", (S.premium ? "Premium: on" : "Premium: off", () => { S.premium = !S.premium; paywallShown = false; OnAdmin(); }), ("Gifts ready", () => { S.onlineReadyAt = 0; S.bonusReadyAt = 0; OnAdmin(); }), ("End trial", () => { S.trialStart = 1; ui.ClosePanels(); }));
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

        private void ConfirmKill()
        {
            ui.ClosePanels();
            ui.ShowDialog("Kill " + Rules.Fav.name + "?", "Admin test: it dies now and leaves a tombstone. This can't be undone.", null,
                ("Kill it", "#C8412F", "#8E2C1F", Hud.Cream, (Action)(() => { ui.HideMemo(); if (!S.dead) Die(); })),
                ("Cancel", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => ui.HideMemo())));
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
            Later(.9f, () => CelebrateGrowth(si + 1));
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
