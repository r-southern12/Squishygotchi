using System;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Turning in for the night (user request, 28 Sep 2026). In the evening the game asks once whether you're going
    /// to bed (and a moon button offers it any time at night). Asleep, it sleeps in its bed in the room (user request,
    /// 30 Sep 2026: the game stays playable, you can squish it, arrange and open steamers), Rest fills up by morning,
    /// the other needs drain slowly and the free steamers are collected for you. The moon wakes it early; in the
    /// morning (or opening the game) a wake-up screen says how it slept.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private float nightAsk = -1; // seconds in the game tonight before the gentle prompt

        /// <summary>Called from the once-a-second HUD tick.</summary>
        private void StepNight(float dt)
        {
            var local = DateTime.Now;
            bool can = mode == "home" && !visiting && Rules.CanSleep(local);
            bool asleepHere = S.asleep && mode == "home" && !visiting && !S.dead;
            ui.SetMoon((can || asleepHere) && !ui.IntroOn && !ui.NightOn);
            if (asleepHere && !ui.IntroOn && !ui.NightOn)
            {
                if (DateTime.UtcNow.Ticks >= S.sleepUntil) { ShowWakeScreen(null); return; } // morning came while playing
                if ((ai.act == null || !ai.act.night) && drag == null && !pet.Held) SleepNow(); // back to bed after being moved or interrupted
            }
            if (!can || ui.IntroOn || ui.NightOn || S.overnight == "off" || S.lastNightPrompt == GameRules.NightKey(local) || DateTime.UtcNow.Ticks < S.nightSnoozeUntil) { nightAsk = -1; return; }
            if (nightAsk < 0) nightAsk = 0;
            nightAsk += dt;
            // After a little while in the game, and never over a panel, a dialog or a drag.
            if (nightAsk > C.rules.nightAskSeconds && drag == null && !ui.AnyPanelOpen) { nightAsk = -1; AskNight(); }
        }

        public void OnMoon() { if (S.asleep) ShowWakeScreen(null); else AskNight(); }
        public void OnNightTap() { if (S.asleep) ShowWakeScreen(null); }

        private void AskNight()
        {
            if (!Rules.CanSleep(DateTime.Now)) return;
            S.lastNightPrompt = GameRules.NightKey(DateTime.Now);
            WriteSave();
            string name = Rules.Fav.name;
            ui.ShowDialog("Turning in for the night?",
                name + " goes to bed and sleeps until morning: Rest fills up overnight, the other needs drain slowly, and your free steamers are collected for you. You can keep playing while it sleeps.",
                "icon:rest",
                ("Good night", "#8C7BB0", "#6A5A8E", Hud.Cream, (Action)(() => { ui.HideMemo(); GoToBed(); })),
                ("Snooze…", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => { ui.HideMemo(); AskSnooze(); })),
                ("Not tonight", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => ui.HideMemo())));
        }

        /// <summary>Snooze the bedtime prompt: the player picks how long, then it asks again.</summary>
        private void AskSnooze()
        {
            var mins = C.rules.nightSnoozeMinutes != null && C.rules.nightSnoozeMinutes.Length > 0 ? C.rules.nightSnoozeMinutes : new[] { 15, 30, 60 };
            var buttons = new System.Collections.Generic.List<(string, string, string, string, Action)>();
            foreach (int m in mins)
            {
                int mm = m;
                buttons.Add((mm >= 60 && mm % 60 == 0 ? (mm / 60) + (mm == 60 ? " hour" : " hours") : mm + " minutes", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() =>
                {
                    ui.HideMemo();
                    S.nightSnoozeUntil = DateTime.UtcNow.AddMinutes(mm).Ticks;
                    S.lastNightPrompt = 0; // ask again once the snooze is up
                    WriteSave();
                    ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), "I’ll ask again in " + (mm >= 60 && mm % 60 == 0 ? (mm / 60) + (mm == 60 ? " hour" : " hours") : mm + " minutes"));
                })));
            }
            ui.ShowDialog("Snooze for how long?", "I’ll ask about bedtime again after that.", "icon:rest", buttons.ToArray());
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
            int given = Rules.GoToSleep(DateTime.UtcNow, DateTime.Now);
            WriteSave();
            sfx.Chime();
            Floater("Sweet dreams, " + Rules.Fav.name);
            if (given > 0) ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), "+1 free steamer");
            SleepNow();
        }

        private bool sleepStarting;

        /// <summary>Off to bed (or, with no bed, curled up where it is), asleep until it wakes.</summary>
        /// <param name="inBed">Straight into bed without walking there (opening the game while it's tucked in).</param>
        private void SleepNow(bool inBed = false)
        {
            if (!S.asleep || S.dead) return;
            if (inBed && ai.act != null && ai.act.night && ai.mode == "walk") { PutInBed(); return; }
            if (ai.act != null && ai.act.night && (ai.mode == "act" || ai.mode == "walk")) { ui.SetHint(Rules.Fav.name + " is asleep till morning · keep playing, or tap the moon to wake it"); return; } // already in bed (or on the way)
            var bed = NearestRole("bed");
            sleepStarting = true;
            if (bed != null) UseItem(bed, true);
            sleepStarting = false;
            var bedAct = C.Activity("bed");
            var night = new ActivityData { role = "bed", label = "Asleep for the night", need = "rest", dur = 1e7f, rate = bedAct != null ? bedAct.rate : .1f, perch = bedAct != null ? bedAct.perch : 0, sleep = true };
            if (bed != null && ai.act != null && ai.act.it == bed) { ai.act.act = night; ai.act.night = true; }
            else
            {
                ai.act = new Activity { role = "makeDo", act = new ActivityData { label = "Asleep for the night", need = "rest", dur = 1e7f, rate = .02f, sleep = true }, night = true };
                ai.mode = "act";
                ai.actT = 0;
                ai.self = false;
            }
            if (inBed) PutInBed();
            ui.ShowBubble("rest", "Asleep for the night", false);
            ui.SetHint(Rules.Fav.name + " is asleep till morning · keep playing, or tap the moon to wake it");
        }

        /// <summary>Already lying in its bed, asleep (no walk across the room).</summary>
        private void PutInBed()
        {
            if (ai.act == null || !ai.act.night || ai.spot == null || ai.mode != "walk") return;
            ai.x = ai.spot.stand.x;
            ai.z = ai.spot.stand.y;
            ai.y = ai.spot.y;
            ai.path.Clear();
            ai.seg = null;
            if (ai.spot.approach.HasValue) ai.perch = ai.spot.approach;
            if (ai.spot.face.HasValue) petYawY = Mathf.Atan2(ai.spot.face.Value.x - ai.x, ai.spot.face.Value.y - ai.z);
            ai.mode = "act";
            ai.actT = 0;
            StartAct();
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
                if (ai.act != null && ai.act.night) FinishActivity();
                ui.SetHint("");
                ui.HideWake();
                ui.HideNight();
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
                    ("Back to sleep", "#8C7BB0", "#6A5A8E", Hud.Cream, (Action)(() => { ui.HideWake(); SleepNow(); then?.Invoke(); })));
        }
    }
}
