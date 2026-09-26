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
            bool canExpand = Rules.CanExpand();
            ui.ExpandDot(canExpand && mode == "home");
            if (canExpand && !toldExpand && mode == "home") { toldExpand = true; ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .35f), "Your room can grow! Open Arrange to expand"); sfx.Chime(); }
            if (!canExpand) toldExpand = false;
            if (Rules.GiftReady()) ui.SetGift("Gift!", true);
            else { var w = Rules.GiftWait(); ui.SetGift((int)w.TotalHours + ":" + w.Minutes.ToString("00"), false); }
            if (mode == "home" && !S.dead && !_dying && Rules.ReachedOldAge()) OldAge();
            if (mode == "home" && !paywallShown && Rules.TrialOver() && !S.dead && !_dying) ShowPaywall();
        }

        /// <summary>Applies the life stage and worn accessories to the squishy.</summary>
        private void ApplyLook()
        {
            var st = Rules.LifeStage();
            if (st == shownStage) return;
            shownStage = st;
            pet.SetStage(st);
            pet.SetCosmetics(Rules.Cosmetic(S.hat), Rules.Cosmetic(S.face), Rules.Cosmetic(S.neck));
            UpdateSub();
            Notifier.RefreshPictures(Rules); // widget and notification pictures follow the squishy
        }

        private void RefreshCosmetics() { pet.SetCosmetics(Rules.Cosmetic(S.hat), Rules.Cosmetic(S.face), Rules.Cosmetic(S.neck)); Notifier.RefreshPictures(Rules); }

        /// <summary>Prestige explained: total, this life's outlook and how to earn more.</summary>
        public void OnPrestige()
        {
            ui.SetPanelTitle("info", "Prestige");
            var body = ui.PanelBody("info");
            Big(body, S.prestige + " prestige", "Spend it on accessories in the shop.");
            Hud.Para(body, Rules.Fav.name + "'s life so far: day " + S.age + " of about " + Mathf.RoundToInt(Rules.ExpectedLifespanDays()) + ", quality of life " + Mathf.RoundToInt(Rules.QualityOfLife() * 100) + "%. If it keeps living like this, it will earn about " + Rules.ProjectedPrestige() + " prestige at old age. Better care means a longer life and more prestige.");
            Hud.Para(body, "Also earned by: growing a size (+" + C.rules.sizePrestige + "), a 7-day care streak (+" + C.rules.streakWeekPrestige + "), and completing a tier in your squishy tree.");
            ui.OpenPanel("info");
            sfx.Tap();
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
                ? rec.days + " days · quality of life " + Mathf.RoundToInt(rec.qol * 100) + "% · +" + rec.prestige + " prestige. Its keepsake stays in the room, and all your things carry over to your next squishy."
                : rec.cause + " for too long. Its tombstone stays in the room, and all your things carry over to your next squishy.";
            ui.ShowHud(false);
            ui.ShowDialog(title, meta, old ? "#D9B45A" : "#A9A39C", ("Choose next squishy", "#6F9A74", "#4C7552", Hud.Cream, (Action)NextSquishy));
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
                ("Restore purchase", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => store.Restore())));
        }

        // ---------------- gift steamer ----------------

        public void OnGift()
        {
            if (!Rules.GiftReady())
            {
                var w = Rules.GiftWait();
                ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), "Next gift in " + (int)w.TotalHours + "h " + w.Minutes.ToString("00") + "m");
                sfx.Bonk();
                return;
            }
            Action claim = () => { Rules.ClaimGift(); ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .3f), "+1 steamer!"); sfx.Chime(); WriteSave(); };
            if (S.premium) { claim(); return; }
            // Free players watch a short, optional video; the gift waits if they skip.
            ui.ShowDialog("A gift steamer!", "Watch a short video to open your free steamer. You can skip: the gift waits for you.", "#D9A64A",
                ("Watch video", "#6F9A74", "#4C7552", Hud.Cream, (Action)(() => { ui.HideMemo(); Ads.ShowRewarded(ok => { if (ok) claim(); }); })),
                ("Not now", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => ui.HideMemo())));
        }

        // ---------------- settings ----------------

        public void OnSettings()
        {
            ui.SetPanelTitle("info", "Settings");
            var body = ui.PanelBody("info");
            Toggle(body, "Sound", S.soundOn, v => { S.soundOn = v; sfx.SoundOn = v; ui.SetSoundIcon(v); if (v) sfx.Tap(); else sfx.Hum(0); });
            Toggle(body, "Music", S.musicOn, v => { S.musicOn = v; sfx.MusicOn = v; });
            Toggle(body, "Vibration", S.hapticsOn, v => { S.hapticsOn = v; Haptics.Enabled = v; if (v) Buzz(20); });
            Toggle(body, "Reminders", S.notificationsOn, v => { S.notificationsOn = v; Notifier.Enabled = v; if (!v) Notifier.Clear(); });
            Hud.Para(body, S.premium ? "Full game unlocked. Thank you!" : "Free trial · " + (Rules.TrialOver() ? "ended" : Mathf.CeilToInt((float)Rules.TrialLeft().TotalDays) + " days left, or until your first squishy's life ends."));
            if (!S.premium) Hud.Button(body, "Unlock full game" + (store.Price != null ? " · " + store.Price : ""), "#6F9A74", "#4C7552", Hud.Cream, 14, 44, 16, () => store.Buy(), false, 4);
            Hud.Button(body, "Restore purchase", "#EADCC6", "#CDB999", Hud.Ink, 14, 40, 15, () => store.Restore(), false, 3).Margin(8, 0, 0, 0);
            Hud.Para(body, "Odds are always shown on the steamer screen. No chat, no personal data collected.").Margin(10, 0, 0, 0);
            ui.OpenPanel("info");
            sfx.Tap();
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
            Hud.Sec(body, "Accessories · " + S.prestige + " prestige");
            Hud.Para(body, "Prestige comes from squishies that live a full life. The better their life, the more you earn.", 12, "#6F5F52");
            foreach (var c in C.cosmetics)
            {
                bool own = Rules.HasCosmetic(c.id), worn = S.hat == c.id || S.face == c.id || S.neck == c.id;
                var id = c.id;
                ui.Rec(body, null, c.name, (c.slot == "hat" ? "Hat" : c.slot == "neck" ? "Neck" : "Face") + (own ? worn ? " · wearing" : " · owned" : " · " + c.price + " prestige"),
                    own ? (worn ? "Take off" : "Wear") : c.price.ToString(),
                    () =>
                    {
                        if (own) Rules.Equip(id);
                        else if (!Rules.BuyCosmetic(id)) { sfx.Bonk(); return; }
                        RefreshCosmetics();
                        sfx.Snap();
                        WriteSave();
                        OpenShop();
                    }, !own && S.prestige < c.price, null, true);
            }
        }

        // ---------------- collection tree (Squishies screen) ----------------

        /// <summary>The squishy tree: each finish tier is a level; complete one to claim its reward.</summary>
        private void DrawTree(VisualElement grid)
        {
            var head = Css.Label(grid, Rules.SquishKinds + " of " + C.finishes.Length + " found · " + S.lives.Count + " lives · average quality of life " + Mathf.RoundToInt(Rules.LifetimeQol() * 100) + "% · " + S.prestige + " prestige", "Figtree", 700, 12, Hud.Muted);
            head.Wrap();
            head.style.marginBottom = 8;
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
                    Hud.Button(bar, done ? "Claimed" : full ? "Claim +" + rw.steamers + " steamers" : "Complete: +" + rw.steamers + " steamers", "#6F9A74", "#4C7552", Hud.Cream, 10, 30, 12, () =>
                    {
                        if (Rules.ClaimTier(t)) { sfx.Chime(); ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .4f), t + " set complete!"); WriteSave(); DrawCatalogue(); }
                    }, done || !full, 3).Pad(0, 10, 0, 10);
                }
                VisualElement row = null;
                int n = 0;
                for (int i = 0; i < C.finishes.Length; i++)
                {
                    if (C.finishes[i].tier != tier) continue;
                    if (n % 3 == 0) { row = new VisualElement().Row(Align.Stretch).In(grid); row.style.marginBottom = 8; }
                    int idx = i;
                    var e = new Entry { key = "sq:" + i, name = C.finishes[i].name, rarity = C.FinishRarity(C.finishes[i]), own = Rules.SquishCount(i) > 0, i = i };
                    var cell = ui.Cell(row, e.key, e.name, e.rarity, e.own, e.key == selKey, null, () => { selKey = e.key; ShowDetail(e); sfx.Tap(); });
                    if (n % 3 != 0) cell.style.marginLeft = 8;
                    n++;
                }
                if (row != null) for (int k = n % 3; k > 0 && k < 3; k++) { var f = new VisualElement().In(row); f.style.flexGrow = 1; f.style.flexBasis = 0; f.style.marginLeft = 8; }
            }
        }
    }
}
