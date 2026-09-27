using System;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Turning in for the night (user request, 28 Sep 2026). In the evening the game asks once whether you're going
    /// to bed (and a moon button offers it any time at night). Asleep, needs drain slowly until morning and the free
    /// steamers are collected for you; the next time you open the game a wake-up screen replaces the title screen.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private float nightAsk = -1; // seconds in the game tonight before the gentle prompt
        private bool nightScreen;

        /// <summary>Called from the once-a-second HUD tick.</summary>
        private void StepNight(float dt)
        {
            var local = DateTime.Now;
            bool can = mode == "home" && !visiting && Rules.CanSleep(local);
            ui.SetMoon(can && !ui.IntroOn && !ui.NightOn);
            if (!can || ui.IntroOn || ui.NightOn || S.lastNightPrompt == GameRules.NightKey(local)) { nightAsk = -1; return; }
            if (nightAsk < 0) nightAsk = 0;
            nightAsk += dt;
            // After a little while in the game, and never over a panel, a dialog or a drag.
            if (nightAsk > C.rules.nightAskSeconds && drag == null && !ui.AnyPanelOpen) { nightAsk = -1; AskNight(); }
        }

        public void OnMoon() { AskNight(); }
        public void OnNightTap() { if (nightScreen && S.asleep) ShowWakeScreen(null); }

        private void AskNight()
        {
            if (!Rules.CanSleep(DateTime.Now)) return;
            S.lastNightPrompt = GameRules.NightKey(DateTime.Now);
            WriteSave();
            string name = Rules.Fav.name;
            ui.ShowDialog("Turning in for the night?",
                name + " will sleep until morning. While you’re both asleep its needs drain slowly, and your free steamers are collected for you. See you at breakfast!",
                "#8C7BB0",
                ("Good night", "#8C7BB0", "#6A5A8E", Hud.Cream, (Action)(() => { ui.HideMemo(); GoToBed(); })),
                ("Not yet", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => ui.HideMemo())));
        }

        private void GoToBed()
        {
            CleanupCook();
            ui.ClosePanels();
            ai.act = null;
            ai.target = null;
            ai.path.Clear();
            ai.seg = null;
            ai.mode = "idle";
            ui.HideBubble();
            Rules.GoToSleep(DateTime.UtcNow, DateTime.Now);
            WriteSave();
            sfx.Chime();
            ShowNightScreen();
        }

        private void ShowNightScreen()
        {
            nightScreen = true;
            var art = SquishyArt.Paint(Rules.Fav, SquishyArt.Mood.Asleep, Rules.LifeStage(), 256);
            ui.ShowNight("Sweet dreams, " + Rules.Fav.name, "Needs drain slowly overnight and free steamers are saved for the morning. Close the game whenever you like, or tap to wake up early.", art);
            ui.ShowHud(false);
        }

        /// <summary>Opening the game while asleep: the wake-up screen instead of the title screen.</summary>
        private void ShowWakeScreen(Action then)
        {
            var local = DateTime.Now;
            bool morning = !Rules.IsNight(local);
            string name = Rules.Fav.name;
            var art = SquishyArt.Paint(Rules.Fav, SquishyArt.Mood.Happy, Rules.LifeStage(), 256);
            var slept = TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - S.sleepAt));
            string sleptTx = (int)slept.TotalHours + "h " + slept.Minutes.ToString("00") + "m";
            Action wake = () =>
            {
                var r = Rules.WakeUp(DateTime.UtcNow);
                ui.HideWake();
                ui.HideNight();
                nightScreen = false;
                ui.ShowHud(true);
                ai.idleT = 1.5f;
                pet.V += 3;
                pet.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Grin, 2);
                if (r != null && r.steamers > 0) Later(.4f, () => ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), "+" + r.steamers + " steamer" + (r.steamers == 1 ? "" : "s") + " from the night"));
                sfx.Chime();
                DrawNeeds();
                WriteSave();
                then?.Invoke();
            };
            int coming = Rules.NightSteamersSoFar(DateTime.UtcNow);
            string gift = coming > 0 ? coming + " steamer" + (coming == 1 ? "" : "s") + " collected overnight" : "";
            if (morning)
                ui.ShowWake("Good morning!", name + " slept " + sleptTx + " and woke up smiling.", gift, art,
                    ("Wake up", "#E0924A", "#B06A2E", Hud.Cream, wake));
            else
                ui.ShowWake("Up already?", "It’s still night. " + name + " has slept " + sleptTx + ".", gift, art,
                    ("Wake up", "#E0924A", "#B06A2E", Hud.Cream, wake),
                    ("Back to sleep", "#8C7BB0", "#6A5A8E", Hud.Cream, (Action)(() => { ui.HideWake(); ShowNightScreen(); then?.Invoke(); })));
        }
    }
}
