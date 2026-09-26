using System;
using System.Collections.Generic;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Friends (docs/spec.md): find a friend by code and visit their steamer. While visiting, their room and squishy
    /// replace yours on screen (your own game state is set aside untouched, and time away is caught up on return).
    /// You can pet their squishy, give it one of your snacks and water their plants: each earns you a few coins and
    /// earns them some too, paid when they next open the game. No chat and no personal details, ever.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private GameRules ownRules;
        private bool visiting;
        private string visitId;
        private int visitActs;
        private readonly HashSet<string> visitDone = new HashSet<string>();
        private DateTime visitStart;
        private float publishT = 30;

        private GameRules Own { get { return visiting ? ownRules : Rules; } }

        // ---------------- online upkeep ----------------

        private async void GoOnline()
        {
            await Online.Init();
            if (!Online.Ready) return;
            await Online.Publish(Own.Snapshot());
            var visitors = await Online.Visitors();
            int total = 0;
            foreach (var v in visitors) total += Own.CreditVisit(v.id, v.at, v.acts);
            if (total > 0)
            {
                WriteSave();
                if (!visiting) Later(2f, () => Floater("A friend visited and looked after " + Own.Fav.name + "! +" + total + " coins"));
            }
        }

        /// <summary>Keeps your shared room fresh (called from the game loop).</summary>
        private void StepOnline(float dt)
        {
            publishT -= dt;
            if (publishT > 0 || visiting) return;
            publishT = 300;
            if (Online.Ready) _ = Online.Publish(Own.Snapshot());
            else GoOnline();
        }

        // ---------------- friends panel ----------------

        public void OnFriends()
        {
            if (visiting) return;
            ui.SetPanelTitle("info", "Friends");
            var body = ui.PanelBody("info");

            Hud.Sec(body, "Your friend code");
            var chip = new Frame().Set(Css.C("#EADCC6"), 14).Row(Align.Center, Justify.Center).Pad(12, 16, 12, 16).In(body);
            Css.Label(chip, Rules.EnsureFriendCode(), "Gluten", 800, 26, Hud.Ink).style.letterSpacing = 3;
            Hud.Para(body, "Share it with a friend so they can visit your steamer.", 12, "#6F5F52").Margin(6, 0, 12, 0);

            Hud.Sec(body, "Visit a friend");
            var field = new TextField { maxLength = 9, value = "" };
            field.Text("Figtree", 700, 18, Hud.Ink);
            field.SetEnabled(Online.Ready);
            body.Add(field);
            Hud.Button(body, "Visit", "#6F9A74", "#4C7552", Hud.Cream, 14, 44, 16, () => VisitByCode(field.value), !Online.Ready, 3).Margin(8, 0, 0, 0);
            if (!Online.Ready) Hud.Para(body, Online.Status, 12, "#8E3322").Margin(8, 0, 0, 0);

            if (S.friends.Count > 0)
            {
                Hud.Sec(body, "Your friends").Margin(12, 0, 0, 0);
                foreach (var f in S.friends)
                {
                    var fr = f;
                    string name = C.finishes[Mathf.Clamp(f.finish, 0, C.finishes.Length - 1)].name;
                    ui.Rec(body, "sq:" + Mathf.Clamp(f.finish, 0, C.finishes.Length - 1), name + "'s steamer", "Code " + f.code, "Visit", () => VisitFriend(fr), !Online.Ready);
                }
            }
            Hud.Para(body, "On a visit you can pet their squishy, give it a snack and water their plants: a few coins for you both. No chat, ever.", 12, "#6F5F52").Margin(12, 0, 0, 0);
            ui.OpenPanel("info");
            sfx.Tap();
            if (!Online.Ready) GoOnline();
        }

        private async void VisitByCode(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 8) { Floater("Friend codes look like ABCD-2345", "bad"); sfx.Bonk(); return; }
            if (code == Rules.EnsureFriendCode()) { Floater("That's your own code!", "bad"); sfx.Bonk(); return; }
            Floater("Looking for your friend…");
            var found = await Online.Find(code);
            if (!found.HasValue) { Floater("No steamer found with that code", "bad"); sfx.Bonk(); return; }
            Rules.RememberFriend(found.Value.id, found.Value.room);
            WriteSave();
            StartVisit(found.Value.id, found.Value.room);
        }

        private async void VisitFriend(FriendData f)
        {
            Floater("Knocking on the steamer…");
            var room = await Online.Load(f.id);
            if (room == null) { Floater("Couldn't reach your friend's steamer", "bad"); sfx.Bonk(); return; }
            Rules.RememberFriend(f.id, room);
            StartVisit(f.id, room);
        }

        // ---------------- the visit ----------------

        /// <summary>Visit a room. With a null id it's a local test visit (admin): nothing is sent or paid.</summary>
        private void StartVisit(string friendId, RoomSnapshot room)
        {
            if (visiting || mode != "home" || S.dead) return;
            ui.ClosePanels();
            WriteSave();
            ownRules = Rules;
            visitStart = DateTime.UtcNow;
            visitId = friendId;
            visitActs = 0;
            visitDone.Clear();
            energyT = 0;
            var guest = new GameRules(C, GameRules.GuestState(C, room));
            WipeTo(() =>
            {
                Rules = guest;
                visiting = true;
                RebuildHome();
                SetMode("visit");
                ShowVisitCard();
                ui.SetCoins(Own.S.coins);
            });
        }

        private void ShowVisitCard()
        {
            ui.ShowVisit("Visiting " + Rules.Fav.name + "'s steamer", VisitSub(),
                ("Give a snack", "#D9A64A", "#B0822F", Hud.Cream, (Action)VisitSnack),
                ("Go home", "#6F9A74", "#4C7552", Hud.Cream, (Action)(() => EndVisit(false))));
        }

        private string VisitSub()
        {
            string Tick(string k, string label) { return (visitDone.Contains(k) ? "✓ " : "") + label; }
            return Tick("pet", "Squish them") + " · " + Tick("feed", "give a snack") + " · " + Tick("water", "water a plant");
        }

        /// <summary>One caring thing done on a visit: coins for you (once per kind), and a note for your friend.</summary>
        private void VisitAct(string kind)
        {
            if (!visiting || !visitDone.Add(kind)) return;
            visitActs++;
            ui.SetVisitSub(VisitSub());
            if (visitId == null) { Floater("Test visit: " + kind); return; }
            ownRules.AddCoins(C.rules.visitCoins);
            ui.SetCoins(ownRules.S.coins);
            sfx.Coin();
            Floater("+" + C.rules.visitCoins + " coins · your friend gets some too");
            _ = Online.RecordVisit(visitId, visitActs);
        }

        private void VisitSnack()
        {
            var own = ownRules.S;
            int k = Array.FindIndex(own.snacks, n => n > 0);
            if (k < 0) { Floater("You have no snacks · buy some in the shop", "bad"); sfx.Bonk(); return; }
            if (visitDone.Contains("feed")) { Floater("They're full, thank you!"); return; }
            own.snacks[k]--;
            pet.Chewing = true;
            pet.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Grin, 2.5f);
            Floater(C.snacks[k].name + " · yum!");
            VisitAct("feed");
        }

        private void EndVisit(bool now)
        {
            if (!visiting) return;
            Action back = () =>
            {
                visiting = false;
                Rules = ownRules;
                ownRules = null;
                ui.HideVisit();
                RebuildHome();
                SetMode("home");
                CatchUp(visitStart, DateTime.UtcNow);
                DrawNeeds();
                UpdateSub();
                ui.SetCoins(S.coins);
                ui.TaskDot(Rules.AnyTaskDone());
                Notifier.RefreshPictures(Rules);
                WriteSave();
            };
            if (now) back(); else WipeTo(back);
        }

        /// <summary>Clears the room and builds it again from the current state (yours, or a friend's while visiting).</summary>
        private void RebuildHome()
        {
            CleanupCook();
            EndToys();
            ai.act = null;
            ai.mode = "idle";
            ai.target = null;
            ai.idleT = 1;
            drag = null;
            ptrs.Clear();
            var old = new List<Transform>();
            foreach (Transform ch in home) old.Add(ch);
            foreach (var ch in old) { ch.gameObject.SetActive(false); Destroy(ch.gameObject); }
            items.Clear();
            BuildHome();
            shownStage = (GameRules.Life)(-1);
            SetPet(S.favIdx);
            ApplyLook();
            pet.SetCosmetics(Rules.Cosmetic(S.hat), Rules.Cosmetic(S.face), Rules.Cosmetic(S.neck));
            RebuildObstacles();
            ComputeComfort();
            DrawNeeds();
        }

        /// <summary>Admin: visit your own room as if it were a friend's, to try the visit screen offline.</summary>
        private void TestVisit()
        {
            ui.ClosePanels();
            var snap = Rules.Snapshot();
            StartVisit(null, snap);
        }
    }
}
