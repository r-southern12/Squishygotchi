using System;
using System.Linq;
using Squishy.Runtime.Models;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Life cycle and meta: old age and prestige, life stages, prestige accessories, the free trial and unlock,
    /// the gift steamer, settings, the collection tree and streaks.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private Store store;
        private float lifeTick;
        private bool paywallShown;
        private int shownPrestige = -1;
        private bool toldExpand;
        private GameRules.Life shownStage = (GameRules.Life)(-1);

        private void InitMeta()
        {
            store = new Store(C.rules.fullUnlockProductId, () => { S.premium = true; WriteSave(); ui.HideMemo(); paywallShown = false; ui.ShowHud(true); Floater("Full game unlocked! Thank you!"); sfx.Chime(); });
            Ads.Init(C.rules.adsGameIdAndroid, C.rules.adsGameIdIos);
            sfx.SoundOn = S.soundOn;
            sfx.MusicOn = S.musicOn;
            ApplySoundPicks();
            ui.SetSoundIcon(S.soundOn);
            Haptics.Enabled = S.hapticsOn;
            Notifier.Enabled = S.notificationsOn;
            ApplyLook();
        }

        /// <summary>Once a second: life stage, old age, the gift chip and the paywall.</summary>
        private void StepMeta(float dt)
        {
            lifeTick -= dt;
            if (lifeTick > 0) return;
            lifeTick = 1;
            ApplyLook();
            if (S.prestige != shownPrestige)
            {
                if (shownPrestige >= 0 && S.prestige > shownPrestige && mode == "home") ui.FloatAt(new Vector2(ui.Width * .3f, 120), "+" + (S.prestige - shownPrestige) + " prestige", null);
                shownPrestige = S.prestige;
                ui.SetPrestige(S.prestige);
            }
            // Reached a bigger size: the steamer grows wider round it (back home, not mid-unbox or visiting).
            if (mode == "home" && !visiting && !S.dead && Rules.ReachRoomSize() && Rules.RoomRadius() > HR + .01f) { GrowRoom(); }
            bool canExpand = Rules.CanExpand();
            ui.ExpandDot(canExpand && mode == "home");
            if (canExpand && !toldExpand && mode == "home") { toldExpand = true; ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .35f), "Your room can level up: space for one more piece! Open Arrange"); sfx.Chime(); }
            if (!canExpand) toldExpand = false;
            StepNight(1);
            if (Rules.OnlineReady()) ui.SetGift("Free!", true);
            else if (Rules.BonusReady()) ui.SetGift("Bonus", true);
            else { var w = Rules.OnlineWait(); ui.SetGift((int)w.TotalHours + ":" + w.Minutes.ToString("00"), false); }
            if (mode == "home" && !S.dead && !_dying && Rules.ReachedOldAge()) OldAge();
            if (mode == "home" && !paywallShown && Rules.TrialOver() && !S.dead && !_dying) ShowPaywall();
        }

        /// <summary>Applies the life stage and worn accessories to the squishy.</summary>
        private void ApplyLook()
        {
            var st = Rules.LifeStage();
            if (st == shownStage) return;
            shownStage = st;
            int grewUp = visiting ? 0 : Rules.AwardStagePrestige();
            if (grewUp > 0) Later(1.2f, () => Floater(Rules.Fav.name + " is growing up well · +" + grewUp + " prestige"));
            pet.SetStage(st);
            pet.SetCosmetics(Rules.Cosmetic(S.hat), Rules.Cosmetic(S.face), Rules.Cosmetic(S.neck));
            UpdateSub();
            if (!visiting) Notifier.RefreshPictures(Rules); // widget and notification pictures follow your squishy
        }

        private void RefreshCosmetics() { pet.SetCosmetics(Rules.Cosmetic(S.hat), Rules.Cosmetic(S.face), Rules.Cosmetic(S.neck)); Notifier.RefreshPictures(Rules); }

        /// <summary>The Prestige store (tap the prestige star): accessories bought with prestige, what it is and how to earn more.</summary>
        public void OnPrestige()
        {
            bool fresh = !ui.PanelOpen("info");
            ui.SetPanelTitle("info", "Prestige store");
            var body = ui.PanelBody("info");
            Big(body, S.prestige + " prestige", "Earned mostly when a squishy lives a full life, and a little each time it grows up well. Spend it on accessories here.");
            Hud.Para(body, Rules.Fav.name + "'s life so far: day " + S.age + " of about " + Mathf.RoundToInt(Rules.ExpectedLifespanDays()) + ", quality of life " + Mathf.RoundToInt(Rules.QualityOfLife() * 100) + "%. If it keeps living like this, it will earn about " + Rules.ProjectedPrestige() + " prestige at old age. Better care means a longer life and more prestige.");
            Hud.Para(body, "A little is also earned each time it grows into a new life stage (young, adult, elder), more the better it has been looked after.");
            ShopCosmetics(body);
            body.Gap(8);
            if (fresh) { ui.OpenPanel("info"); sfx.Tap(); }
        }

        // ---------------- old age ----------------

        /// <summary>A full life: it drifts off to sleep for good; a golden keepsake and prestige for how well it lived.</summary>
        private void OldAge()
        {
            CleanupCook();
            EndToys();
            ai.mode = "dead";
            ai.act = null;
            ui.HideBubble();
            var rec = Rules.EndLife(true, "Old age");
            sfx.Chime();
            _dying = true;
            Later(2.4f, () =>
            {
                _dying = false;
                pet.Pivot.gameObject.SetActive(false);
                var t = AddItem("tomb:gold", ai.x, ai.z, 0);
                t.g.localScale = Vector3.one * .01f;
                t.grow = 0;
                RebuildObstacles();
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2;
                    HPuff(new Vector3(ai.x + Mathf.Cos(a) * .15f, Y0 + .1f, ai.z + Mathf.Sin(a) * .15f), new Vector3(Mathf.Cos(a) * .5f, .9f, Mathf.Sin(a) * .5f), .08f, 1.2f, 2, .5f);
                }
                ShowLifeCard(rec);
            });
        }

        private void ShowLifeCard(LifeRecord rec)
        {
            bool old = rec.cause == "Old age";
            string title = old ? rec.name + " lived a full life" : rec.name + " has passed away";
            string meta = old
                ? rec.days + " days · quality of life " + Mathf.RoundToInt(rec.qol * 100) + "% · +" + rec.prestige + " prestige. Its keepsake stays in the room, and a baby " + rec.name + " is ready to start a new life."
                : rec.cause + " for too long. Its tombstone stays in the room, and all your things carry over to your next squishy.";
            ui.ShowHud(false);
            if (old) ui.ShowDialog(title, meta, "#D9B45A", ("Meet the new baby", "#6F9A74", "#4C7552", Hud.Cream, (Action)NewBaby));
            else ui.ShowDialog(title, meta, "#A9A39C", ("Choose next squishy", "#6F9A74", "#4C7552", Hud.Cream, (Action)NextSquishy));
            WriteSave();
        }

        // ---------------- free trial and unlock ----------------

        private void ShowPaywall()
        {
            paywallShown = true;
            CleanupCook();
            ui.ClosePanels();
            ui.ShowHud(false);
            string price = store.Price ?? "";
            ui.ShowDialog("Your free trial has ended",
                "Thanks for looking after " + Rules.Fav.name + "! Unlock the full game to keep caring for squishies: no ads, no limits, every future squishy.",
                null,
                ("Unlock full game" + (price.Length > 0 ? " · " + price : ""), "#6F9A74", "#4C7552", Hud.Cream, (Action)(() => store.Buy())),
                ("Restore purchase", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => store.Restore())),
                // Test builds only: never lock the tester out (switched off with adminTools for release).
                (C.rules.adminTools || Debug.isDebugBuild ? "Tester: unlock (admin)" : null, "#8C7BB0", "#6A5A8E", Hud.Cream, (Action)(() => { S.premium = true; WriteSave(); ui.HideMemo(); paywallShown = false; ui.ShowHud(true); })));
        }

        // ---------------- gift steamer ----------------

        /// <summary>
        /// The gift chip: collects the free steamers stacked up (one every few hours), and offers the bonus steamer:
        /// an optional short video for free players, simply included with the full game. Never required.
        /// </summary>
        public void OnGift()
        {
            int got = Rules.ClaimOnline() ? 1 : 0;
            if (got > 0) { ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), "+1 free steamer!"); sfx.Chime(); WriteSave(); }
            if (!Rules.BonusReady())
            {
                if (got > 0) return;
                var w = Rules.OnlineWait();
                ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), "Next free steamer in " + (int)w.TotalHours + "h " + w.Minutes.ToString("00") + "m");
                return;
            }
            Action bonus = () => { if (Rules.ClaimBonus()) { ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .36f), "+1 bonus steamer!"); sfx.Chime(); WriteSave(); } };
            if (S.premium) { bonus(); return; }
            ui.ShowDialog("Bonus steamer", (got > 0 ? "Your free steamer is in. " : "") + "Want one more? Watch a short video for a bonus steamer. Totally optional: steamers keep arriving every " + C.rules.giftHours + " hours either way.", "#D9A64A",
                ("Watch video", "#6F9A74", "#4C7552", Hud.Cream, (Action)(() => { ui.HideMemo(); Ads.ShowRewarded(ok => { if (ok) bonus(); }); })),
                ("No thanks", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => ui.HideMemo())));
        }

        // ---------------- settings ----------------

        public void OnSettings()
        {
            ui.SetPanelTitle("info", "Settings");
            var body = ui.PanelBody("info");
            // At the top: at the bottom of a long panel it sat outside the touchable area and taps never reached it.
            if (C.rules.adminTools || Debug.isDebugBuild)
                Hud.Button(body, "Admin tools (testing)", "#8C7BB0", "#6A5A8E", Hud.Cream, 14, 40, 15, () => OnAdmin(), false, 3).Margin(0, 0, 8, 0);
            ProfileRow(body);
            Toggle(body, "Sound", S.soundOn, v => { S.soundOn = v; sfx.SoundOn = v; ui.SetSoundIcon(v); if (v) sfx.Tap(); else sfx.Hum(0); });
            Toggle(body, "Music", S.musicOn, v => { S.musicOn = v; sfx.MusicOn = v; });
            if (S.musicOn && S.soundOn) TrackRow(body);
            Toggle(body, "Vibration", S.hapticsOn, v => { S.hapticsOn = v; Haptics.Enabled = v; if (v) Buzz(20); });
            Toggle(body, "Reminders", S.notificationsOn, v => { S.notificationsOn = v; Notifier.Enabled = v; if (!v) Notifier.Clear(); });
            // Bedtime: the evening question to tuck it in (tucking in is always your choice; the moon button works either way).
            Toggle(body, "Bedtime reminder (asks each evening)", S.overnight != "off", v => { S.overnight = v ? "ask" : "off"; WriteSave(); });
            // Holiday pause: only here, never one tap away in the game.
            if (!S.dead)
                Toggle(body, "Holiday pause (needs pause while you're away)", S.tucked, v =>
                {
                    if (v && !Rules.CanTuck()) { Floater("Too weak to pause now: look after " + Rules.Fav.name + " first", "bad"); OnSettings(); return; }
                    if (v) Tuck(); else Wake();
                    OnSettings();
                });
            if (S.notificationsOn && Notifier.PermissionState() == "blocked")
            {
                // The phone is blocking them (permission refused): one tap to the setting that allows them.
                Hud.Para(body, "Your phone is blocking reminders from " + Rules.Fav.name + ".").Margin(0, 0, 4, 0);
                Hud.Button(body, "Allow in phone settings", "#8C7BB0", "#6A5A8E", Hud.Cream, 14, 40, 15, Notifier.OpenSettings, false, 3).Margin(0, 0, 10, 0);
            }
            Hud.Para(body, S.premium ? "Full game unlocked. Thank you!" : "Free trial · " + (Rules.TrialOver() ? "ended" : Mathf.CeilToInt((float)Rules.TrialLeft().TotalDays) + " days left, or until your first squishy's life ends."));
            if (!S.premium) Hud.Button(body, "Unlock full game" + (store.Price != null ? " · " + store.Price : ""), "#6F9A74", "#4C7552", Hud.Cream, 14, 44, 16, () => store.Buy(), false, 4);
            Hud.Button(body, "Restore purchase", "#EADCC6", "#CDB999", Hud.Ink, 14, 40, 15, () => store.Restore(), false, 3).Margin(8, 0, 0, 0);
            Hud.Para(body, "Odds are always shown on the steamer screen. No chat, no personal data collected." + (string.IsNullOrEmpty(C.rules.musicCredit) ? "" : " " + C.rules.musicCredit + ".")).Margin(10, 0, 0, 0);
            ui.OpenPanel("info");
            sfx.Tap();
        }

        // ---------------- title screen and profile ----------------

        private void AfterIntro()
        {
            sfx.Chime();
            if (!S.welcomed) EditProfile(false);
        }

        private void EditProfile(bool edit)
        {
            ui.ClosePanels();
            ui.ShowWelcome(edit, S.playerName, S.avatar, Rules.EnsureFriendCode(), (name, color) =>
            {
                string n = Rules.SetProfile(name, color);
                WriteSave();
                sfx.Chime();
                ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), edit ? "Saved!" : "Hi, " + n + "!");
            });
        }

        /// <summary>Settings header: avatar, name and friend code, with Edit.</summary>
        private void ProfileRow(VisualElement body)
        {
            var row = new VisualElement().Row(Align.Center).In(body);
            row.style.marginBottom = 12;
            Hud.Avatar(row, S.playerName, S.avatar, 44);
            var col = new VisualElement().Col(Align.FlexStart).Margin(0, 0, 0, 10).In(row);
            col.style.flexGrow = 1;
            Css.Label(col, string.IsNullOrEmpty(S.playerName) ? "Keeper" : S.playerName, "Gluten", 800, 18);
            Css.Label(col, "Friend code " + Rules.EnsureFriendCode() + " 00b7 saved on this phone", "Figtree", 400, 12, Hud.Muted);
            Hud.Button(row, "Edit", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 14, () => EditProfile(true)).Size(64, null);
        }

        private void Toggle(VisualElement body, string label, bool on, Action<bool> set)
        {
            var row = new VisualElement().Row(Align.Center, Justify.SpaceBetween).In(body);
            row.style.marginBottom = 8;
            Css.Label(row, label, "Figtree", 700, 15);
            Hud.Button(row, on ? "On" : "Off", on ? "#6F9A74" : "#EADCC6", on ? "#4C7552" : "#CDB999", on ? Hud.Cream : Hud.Ink, 12, 36, 14, () => { set(!on); WriteSave(); OnSettings(); }).Size(72, null);
        }

        // ---------------- prestige accessories (shop section) ----------------

        private void ShopCosmetics(VisualElement body)
        {
            Hud.Sec(body, "Accessories");
            foreach (var c in C.cosmetics)
            {
                bool own = Rules.HasCosmetic(c.id), worn = S.hat == c.id || S.face == c.id || S.neck == c.id;
                var id = c.id;
                ui.Rec(body, null, c.name, (c.slot == "hat" ? "Hat" : c.slot == "neck" ? "Neck" : "Face") + (own ? worn ? " · wearing" : " · owned" : " · " + c.price + " prestige"),
                    own ? (worn ? "Take off" : "Wear") : c.price.ToString(),
                    () =>
                    {
                        if (own) Rules.Equip(id);
                        else if (!Rules.BuyCosmetic(id)) { sfx.Bonk(); return; } else ui.MarkBought();
                        RefreshCosmetics();
                        sfx.Snap();
                        WriteSave();
                        OnPrestige();
                    }, !own && S.prestige < c.price, null, true);
            }
        }

        // ---------------- collection tree (Squishies screen) ----------------

        /// <summary>The squishy tree: each finish tier is a level; complete one to claim its reward.</summary>
        /// <summary>An owned squishy's size and age for its card: "Jumbo · Day 12" (its own life, or not started yet).</summary>
        private string SquishLine(int i)
        {
            string size = C.sizes[C.SizeIdxFor(Rules.SquishCount(i))].name;
            int age = i == S.favIdx ? S.age : -1;
            if (age < 0) { var life = S.lifeOf.Find(l => l.i == i); age = life != null ? life.age : -1; }
            return size + " · " + (age >= 0 ? "Day " + age : "not raised yet");
        }

        private void DrawTree(VisualElement grid)
        {
            var head = Css.Label(grid, Rules.SquishKinds + " of " + C.finishes.Length + " found · " + S.lives.Count + " lives · average quality of life " + Mathf.RoundToInt(Rules.LifetimeQol() * 100) + "% · " + S.prestige + " prestige", "Figtree", 700, 12, Hud.Muted);
            head.Wrap();
            head.style.marginBottom = 8;
            var fbar = new VisualElement().Row(Align.Center, Justify.FlexEnd).In(grid);
            fbar.style.marginBottom = 4;
            Hud.Button(fbar, ownedOnly ? "Owned only · on" : "Owned only · off", ownedOnly ? "#6F9A74" : "#EADCC6", ownedOnly ? "#4C7552" : "#CDB999",
                ownedOnly ? Hud.Cream : Hud.Ink, 99, 32, 13, () => { ownedOnly = !ownedOnly; selKey = null; DrawCatalogue(); sfx.Tap(); }).Size(150, null);
            foreach (var tier in GameRules.TreeTiers)
            {
                int total, own = Rules.TierOwned(tier, out total);
                var rw = Rules.TierReward(tier);
                var bar = new VisualElement().Row(Align.Center, Justify.SpaceBetween).In(grid);
                bar.style.marginTop = 10;
                bar.style.marginBottom = 6;
                Css.Label(bar, tier + " · " + own + " of " + total, "Gluten", 800, 16);
                if (rw != null)
                {
                    bool done = Rules.TierClaimed(tier), full = own >= total;
                    var t = tier;
                    Hud.Button(bar, done ? "Claimed" : full ? "Claim +" + rw.prestige + " prestige" : "Complete: +" + rw.prestige + " prestige", "#6F9A74", "#4C7552", Hud.Cream, 10, 30, 12, () =>
                    {
                        if (Rules.ClaimTier(t)) { sfx.Chime(); ui.SetPrestige(S.prestige); ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .4f), t + " set complete! +" + rw.prestige + " prestige"); WriteSave(); DrawCatalogue(); }
                    }, done || !full, 3).Pad(0, 10, 0, 10);
                }
                VisualElement row = null;
                int n = 0;
                for (int i = 0; i < C.finishes.Length; i++)
                {
                    if (C.finishes[i].tier != tier) continue;
                    if (ownedOnly && Rules.SquishCount(i) <= 0) continue;
                    if (n % 3 == 0) { row = new VisualElement().Row(Align.Stretch).In(grid); row.style.marginBottom = 8; }
                    int idx = i;
                    var e = new Entry { key = "sq:" + i, name = C.finishes[i].name, rarity = C.FinishRarity(C.finishes[i]), own = Rules.SquishCount(i) > 0, i = i };
                    var cell = ui.Cell(row, e.key, e.own ? e.name + " · " + SquishLine(i) : e.name, e.rarity, e.own, e.key == selKey, null, () => { selKey = e.key; ShowDetail(e); sfx.Tap(); });
                    if (n % 3 != 0) cell.style.marginLeft = 8;
                    n++;
                }
                if (row != null) for (int k = n % 3; k > 0 && k < 3; k++) { var f = new VisualElement().In(row); f.style.flexGrow = 1; f.style.flexBasis = 0; f.style.marginLeft = 8; }
            }
        }
    }
}
