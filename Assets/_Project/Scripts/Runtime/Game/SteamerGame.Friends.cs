using System;
using System.Collections.Generic;
using System.Linq;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Friends (docs/spec.md): find a friend by code and visit their steamer. While visiting, their room and squishy
    /// replace yours on screen (your own game state is set aside untouched, and time away is caught up on return).
    /// You can pet their squishy, give it one of your snacks and water their plants: each earns you a few coins and
    /// earns them some too, paid when they next open the game. No chat and no personal details, ever.
    /// Neighbours (user request, 1 Oct 2026): besides friends by code (no limit), up to 20 random players who
    /// played in the last 3 days, picked from a fresh random sample (no ranking). They visit and care just like friends.
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
        private List<(string id, RoomSnapshot room)> neighbourPicks = new List<(string, RoomSnapshot)>();
        private bool findingNeighbours;

        private GameRules Own { get { return visiting ? ownRules : Rules; } }

        // ---------------- online upkeep ----------------

        private async void GoOnline()
        {
            await Online.Init();
            if (!Online.Ready) return;
            await Online.Publish(Own.Snapshot(), !Own.S.hideRoom);
            var visitors = await Online.Visitors();
            var gifts = new List<GameRules.VisitGift>();
            foreach (var v in visitors) { var g = Own.CreditVisit(v.id, v.at, v.acts, v.what, v.name); if (g != null) gifts.Add(g); }
            if (gifts.Count > 0)
            {
                WriteSave();
                DrawNeeds();
                StartCoroutine(ShowVisitGifts(gifts));
            }
        }

        /// <summary>Keeps your shared room fresh (called from the game loop).</summary>
        private void StepOnline(float dt)
        {
            publishT -= dt;
            if (publishT > 0 || visiting) return;
            publishT = 300;
            if (Online.Ready) _ = Online.Publish(Own.Snapshot(), !Own.S.hideRoom);
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

            var real = S.friends.FindAll(f => !f.neighbour);
            if (real.Count > 0)
            {
                Hud.Sec(body, "Your friends").Margin(12, 0, 0, 0);
                foreach (var f in real) FriendRow(body, f, "Code " + f.code);
            }

            // Neighbours: random players who've played lately.
            Hud.Sec(body, "Neighbours · " + Rules.NeighbourCount() + " of " + C.rules.neighbourMax).Margin(12, 0, 0, 0);
            Hud.Para(body, "Players who've played in the last " + C.rules.neighbourActiveDays + " days.", 12, "#6F5F52").Margin(0, 0, 6, 0);
            foreach (var f in S.friends.FindAll(x => x.neighbour)) FriendRow(body, f, null);
            if (neighbourPicks.Count > 0)
            {
                Hud.Sec(body, "Say hello").Margin(8, 0, 0, 0);
                foreach (var p in neighbourPicks)
                {
                    var pick = p;
                    int fi = Mathf.Clamp(p.room.favIdx, 0, C.finishes.Length - 1);
                    ui.Rec(body, "sq:" + fi, C.finishes[fi].name + "'s steamer", "Room level " + (p.room.roomLv + 1), "Add", () => AddNeighbour(pick), !Online.Ready || Rules.NeighboursFull());
                }
            }
            if (Rules.NeighboursFull()) Hud.Para(body, "That's " + C.rules.neighbourMax + " neighbours. Remove one to add another.", 12, "#6F5F52");
            else Hud.Button(body, findingNeighbours ? "Looking…" : "Find neighbours", "#8C7BB0", "#6A5A8E", Hud.Cream, 14, 44, 16, FindNeighbours, !Online.Ready || findingNeighbours, 3).Margin(6, 0, 0, 0);
            Hud.Para(body, "On a visit you can squish them, give a snack and water a plant: a few coins for you both. No chat, ever.", 12, "#6F5F52").Margin(12, 0, 0, 0);
            ui.OpenPanel("info");
            sfx.Tap();
            if (!Online.Ready) GoOnline();
        }

        /// <summary>One friend or neighbour: visit, or remove.</summary>
        private void FriendRow(VisualElement body, FriendData f, string sub)
        {
            var fr = f;
            int fi = Mathf.Clamp(f.finish, 0, C.finishes.Length - 1);
            var remove = Hud.Button(null, "Remove", "#EADCC6", "#CDB999", Hud.Ink, 10, 26, 12, () => AskRemoveFriend(fr), false, 2).Margin(4, 0, 0, 0);
            remove.style.alignSelf = Align.FlexStart;
            var wait = Rules.VisitWait(f.id); // one visit each every few hours: the button counts down
            ui.Rec(body, "sq:" + fi, C.finishes[fi].name + "'s steamer", sub, wait > TimeSpan.Zero ? WaitText(wait) : "Visit", () => VisitFriend(fr), !Online.Ready || wait > TimeSpan.Zero, remove);
        }

        private static string WaitText(TimeSpan w) { return w.TotalHours >= 1 ? (int)w.TotalHours + "h " + w.Minutes + "m" : Math.Max(1, (int)Math.Ceiling(w.TotalMinutes)) + "m"; }

        private bool VisitedRecently(string id)
        {
            var w = Rules.VisitWait(id);
            if (w <= TimeSpan.Zero) return false;
            Floater("Back in " + WaitText(w));
            sfx.Bonk();
            return true;
        }

        private void AskRemoveFriend(FriendData f)
        {
            string name = C.finishes[Mathf.Clamp(f.finish, 0, C.finishes.Length - 1)].name;
            ui.ShowDialog("Remove " + name + "?", f.neighbour ? "You can find new neighbours any time." : "You can add them again with their code.", "sq:" + Mathf.Clamp(f.finish, 0, C.finishes.Length - 1),
                ("Remove", "#C8674E", "#8E4332", Hud.Cream, (Action)(() => { ui.HideMemo(); Rules.RemoveFriend(f.id); WriteSave(); OnFriends(); })),
                ("Keep", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => ui.HideMemo())));
        }

        /// <summary>A fresh random handful of recently active players (never you or anyone you already have).</summary>
        private async void FindNeighbours()
        {
            if (findingNeighbours || !Online.Ready) return;
            findingNeighbours = true;
            OnFriends();
            var skip = new HashSet<string>();
            foreach (var f in S.friends) skip.Add(f.id);
            neighbourPicks = await Online.Strangers(C.rules.neighbourOffer, C.rules.neighbourActiveDays, skip);
            findingNeighbours = false;
            if (visiting) return;
            if (neighbourPicks.Count == 0) { Floater("No one new nearby"); sfx.Bonk(); }
            OnFriends();
        }

        private void AddNeighbour((string id, RoomSnapshot room) pick)
        {
            if (!Rules.AddNeighbour(pick.id, pick.room)) { Floater("Neighbours full", "bad"); sfx.Bonk(); return; }
            neighbourPicks.RemoveAll(p => p.id == pick.id);
            WriteSave();
            sfx.Chime();
            Floater("New neighbour!");
            OnFriends();
        }

        private async void VisitByCode(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 8) { Floater("Check the code", "bad"); sfx.Bonk(); return; }
            if (code == Rules.EnsureFriendCode()) { Floater("That's your own code!", "bad"); sfx.Bonk(); return; }
            Floater("Looking…");
            var found = await Online.Find(code);
            if (!found.HasValue) { Floater("Code not found", "bad"); sfx.Bonk(); return; }
            Rules.RememberFriend(found.Value.id, found.Value.room, true);
            WriteSave();
            if (VisitedRecently(found.Value.id)) { OnFriends(); return; }
            StartVisit(found.Value.id, found.Value.room);
        }

        private async void VisitFriend(FriendData f)
        {
            if (VisitedRecently(f.id)) return;
            Floater("Knocking…");
            var room = await Online.Load(f.id);
            if (room == null) { Floater("Couldn't connect", "bad"); sfx.Bonk(); return; }
            Rules.RememberFriend(f.id, room);
            StartVisit(f.id, room);
        }

        // ---------------- the visit ----------------

        /// <summary>Visit a room. With a null id it's a local test visit (admin): nothing is sent or paid.</summary>
        private void StartVisit(string friendId, RoomSnapshot room)
        {
            if (visiting || mode != "home" || S.dead) return;
            ui.ClosePanels();
            if (friendId != null) Rules.MarkVisited(friendId);
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
                visitSticker = null;
                BringBuddy(); // your squishy comes along
                ShowVisitCard();
                ui.SetCoins(Own.S.coins);
            });
        }

        private void ShowVisitCard()
        {
            ui.ShowVisit("Visiting " + Rules.Fav.name + "'s steamer", VisitTodos(),
                ("Play", "#6E9C9A", "#4F7876", Hud.Cream, (Action)VisitPlay),
                ("Pamper", "#E86A92", "#B84A70", Hud.Cream, (Action)VisitPamper),
                ("Sticker", "#8C7BB0", "#6A5A8E", Hud.Cream, (Action)VisitSticker),
                ("Give a snack", "#D9A64A", "#B0822F", Hud.Cream, (Action)VisitSnack),
                ("Go home", "#6F9A74", "#4C7552", Hud.Cream, (Action)(() => EndVisit(false))));
        }

        /// <summary>The visit's things to do, ticked off as they're done (the ones without a button say how).</summary>
        private Hud.VisitTodo[] VisitTodos()
        {
            string n = Rules.Fav.name;
            bool plants = items.Any(it => it.a.role == "plant");
            Hud.VisitTodo T(string k, string text, string hint) { return new Hud.VisitTodo(text, hint, visitDone.Contains(k)); }
            return new[]
            {
                T("pet", "Give " + n + " a squish", "tap them"),
                T("feed", "Give " + n + " a snack", null),
                T("water", "Water a plant", plants ? "tap a plant" : "no plants here"),
                T("play", "Play together", null),
                T("pamper", "Pamper " + n, null),
                T("sticker", "Leave a sticker", null),
            };
        }

        // ---------------- more to do on a visit (user request, 2 Oct 2026) ----------------

        private SquishyModel buddy; // your own squishy, along for the visit
        private float bx, bz, byaw, bhop, bLift, bExtra, bPlayT = -1, bBumped;
        private Item bBall;
        private Transform brush;
        private float pamperT = -1;
        private string visitSticker;

        /// <summary>Your squishy comes along: it appears beside theirs and follows it round their room.</summary>
        private void BringBuddy()
        {
            buddy = new SquishyModel(room, C.sizes[ownRules.FavSizeIdx].s);
            buddy.SetFinish(C.finishes[ownRules.S.favIdx]);
            buddy.SetStage(ownRules.LifeStage());
            buddy.SetCosmetics(ownRules.Worn(ownRules.S.hat), ownRules.Worn(ownRules.S.face), ownRules.Worn(ownRules.S.neck));
            Node.SetLayer(buddy.Pivot, HomeLayer);
            var start = FreeSpotNear(new Vector2(ai.x + PetRadius() * 2.4f, ai.z), null);
            bx = start.x;
            bz = start.y;
            byaw = Mathf.Atan2(ai.x - bx, ai.z - bz);
            bPlayT = -1;
        }

        private float BuddyR { get { return buddy != null ? buddy.Scale * 1.14f : .15f; } }

        /// <summary>Each frame of a visit: your squishy stays near (or plays), the brush pampers, keeping clear of furniture.</summary>
        private void StepVisit(float dt)
        {
            if (!visiting || buddy == null) return;
            var host = new Vector2(ai.x, ai.z);
            Vector2 goal;
            float speed = .55f * (.8f + buddy.Scale * 2);
            bExtra = 0;
            if (bPlayT >= 0)
            {
                // Playing together: hop over and bump into them, then kick their ball over (or a little dance).
                bPlayT += dt;
                float contact = PetRadius() + BuddyR - .02f;
                if (bBumped < 0)
                {
                    goal = host;
                    speed *= 1.6f;
                    if (Vector2.Distance(new Vector2(bx, bz), host) <= contact + .03f)
                    {
                        bBumped = 0;
                        pet.V += 5;
                        buddy.V += 5;
                        sfx.Note(3);
                        Buzz(15);
                        var pw = PetWorld();
                        Glints(new Vector3((pw.x + bx) / 2, pw.y + pet.Scale, (pw.z + bz) / 2), "#F2A7B8", 6);
                        ownRules.S.needs[Needs.Play] = Mathf.Min(1, ownRules.S.needs[Needs.Play] + C.rules.visitPlay);
                        VisitAct("play");
                        bBall = items.Find(i => i.arch == "ball");
                    }
                }
                else if (bBall != null)
                {
                    goal = new Vector2(bBall.tx, bBall.tz);
                    speed *= 1.5f;
                    if (Vector2.Distance(new Vector2(bx, bz), goal) <= BuddyR + bBall.a.r + .04f)
                    {
                        // A pass to their squishy, who chases it.
                        var to = (host - goal).normalized;
                        bBall.vx = to.x * 3.2f;
                        bBall.vz = to.y * 3.2f;
                        bBall.bv = -5;
                        buddy.V += 3;
                        sfx.Kick();
                        if (ai.mode == "idle" || ai.mode == "walk") UseItem(bBall, false);
                        bBall = null;
                        bPlayT = 99;
                    }
                }
                else
                {
                    goal = new Vector2(bx, bz);
                    bBumped += dt;
                    if (Mathf.Repeat(bBumped, .45f) < dt) { buddy.V += 3; pet.V += 3; sfx.Note(UnityEngine.Random.Range(0, 6)); }
                    if (bBumped > 1.8f) bPlayT = 99;
                }
                if (bPlayT > 12) bPlayT = -1; // done (or gave up)
            }
            else
            {
                // It stays put and watches theirs (user, 4 Oct 2026: following them round jittered; no following at all).
                // It only steps aside if theirs comes right up to it.
                goal = new Vector2(bx, bz);
                var off = goal - host;
                float clear = PetRadius() + BuddyR + .06f;
                if (off.magnitude < clear) goal = host + (off.magnitude > 1e-4f ? off.normalized : Vector2.right) * (clear + .15f);
            }
            goal = FreeSpotNear(goal, null);
            var pos = new Vector2(bx, bz);
            var to2 = goal - pos;
            float dist = to2.magnitude;
            bool moving = dist > .06f;
            if (moving)
            {
                float step = Mathf.Min(dist, speed * dt);
                pos += to2 / dist * step;
                bhop += step / (.16f + buddy.Scale * .6f);
                bLift = buddy.Scale * .55f * Mathf.Abs(Mathf.Sin(Mathf.PI * bhop));
                bExtra = -.12f * Mathf.Sin(Mathf.PI * (bhop - Mathf.Floor(bhop)));
                byaw = Mathf.LerpAngle(byaw * Mathf.Rad2Deg, Mathf.Atan2(to2.x, to2.y) * Mathf.Rad2Deg, Mathf.Min(1, dt * 8)) * Mathf.Deg2Rad;
            }
            else
            {
                bLift *= .8f;
                byaw = Mathf.LerpAngle(byaw * Mathf.Rad2Deg, Mathf.Atan2(ai.x - pos.x, ai.z - pos.y) * Mathf.Rad2Deg, Mathf.Min(1, dt * 4)) * Mathf.Deg2Rad; // watching them
            }
            // Never inside furniture or inside their squishy.
            pos = FreeSpotNear(pos, null);
            var away = pos - host;
            float minD = (PetRadius() + BuddyR) * .75f; // only a deep overlap is pushed apart at once (stepping aside does the rest)
            if (away.magnitude < minD && away.magnitude > 1e-4f) pos = host + away.normalized * minD;
            bx = pos.x;
            bz = pos.y;
            buddy.Update(dt, bExtra, false, 0);
            buddy.Pivot.localPosition = new Vector3(bx, Y0 + .02f + bLift, bz);
            Node.Rot(buddy.Yaw, 0, byaw, 0);
            StepPamper(dt);
        }

        /// <summary>Play together: your squishy hops over to theirs.</summary>
        private void VisitPlay()
        {
            if (buddy == null) return;
            if (visitDone.Contains("play")) { Floater("Already played"); return; }
            bPlayT = 0;
            bBumped = -1;
            sfx.Tap();
        }

        /// <summary>Pamper: a soft brush over their squishy, with bubbles; it tops up their Clean.</summary>
        private void VisitPamper()
        {
            if (pamperT >= 0) return;
            if (visitDone.Contains("pamper")) { Floater("All pampered!"); return; }
            ai.act = new Activity { role = "makeDo", act = new ActivityData { label = "Being pampered", need = "clean", dur = 3.2f, rate = 0 } };
            ai.mode = "act";
            ai.actT = 0;
            ai.self = true;
            ai.spot = null;
            ai.target = null;
            ai.path.Clear();
            ai.seg = null;
            brush = Node.Group(room, "pamperBrush");
            Node.Mesh(brush, ThreeGeo.Cyl(.014f, .016f, .17f, 10), ThreeMat.M("#C9A27A"), 0, .09f, 0, shadow: false);
            Node.Mesh(brush, ThreeGeo.RBox(.11f, .04f, .06f, .015f), ThreeMat.M("#E86A92"), 0, 0, 0, shadow: false);
            Node.Mesh(brush, ThreeGeo.RBox(.1f, .03f, .05f, .01f), ThreeMat.M("#FFF7EC"), 0, -.03f, 0, shadow: false);
            Node.SetLayer(brush, HomeLayer);
            pamperT = 0;
            pet.Express(SquishyModel.Mouth.Grin, 3.4f);
            sfx.Tap();
        }

        private void StepPamper(float dt)
        {
            if (pamperT < 0 || brush == null) return;
            pamperT += dt;
            var pw = PetWorld();
            float h = pet.Scale * pet.StageScale, sweep = Mathf.Sin(pamperT * 7) * h * .6f;
            brush.localPosition = new Vector3(pw.x + sweep, pw.y + h * 1.05f + .03f * Mathf.Abs(Mathf.Sin(pamperT * 14)), pw.z + h * .2f);
            brush.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(pamperT * 7) * 25);
            if (UnityEngine.Random.value < dt * 10) bathBubbles.Spawn(new Vector3(pw.x + Rnd(-h, h) * .6f, pw.y + h * Rnd(.6f, 1.1f), pw.z + Rnd(-h, h) * .5f), new Vector3(Rnd(-.05f, .05f), Rnd(.12f, .3f), Rnd(-.05f, .05f)), Rnd(.025f, .045f), Rnd(1f, 1.8f), 1.5f, .03f);
            if (Mathf.Repeat(pamperT, .5f) < dt) pet.V += 1.5f;
            if (pamperT > 3.2f)
            {
                Node.Destroy(brush);
                brush = null;
                pamperT = -1;
                Rules.S.needs[Needs.Clean] = Mathf.Min(1, Rules.S.needs[Needs.Clean] + C.rules.visitPamper);
                Glints(new Vector3(pw.x, pw.y + h, pw.z), "#FFF3D6", 5);
                VisitAct("pamper");
            }
        }

        /// <summary>Leave a sticker: pick one; it's pressed onto their floor (they see it for a day, and who left it).</summary>
        private void VisitSticker()
        {
            if (visitDone.Contains("sticker")) { Floater("One per visit"); return; }
            Action Pick(string kind) { return () => { ui.HideMemo(); PressSticker(kind); }; }
            ui.ShowDialog("Leave a sticker", "They'll see it on their floor for a day.", "icon:friends",
                ("Heart", "#E86A92", "#B84A70", Hud.Cream, Pick("heart")),
                ("Star", "#F2B33D", "#C48A1E", Hud.Cream, Pick("star")),
                ("Flower", "#B48CE0", "#8A68B4", Hud.Cream, Pick("flower")),
                ("Wave", "#D9A64A", "#B0822F", Hud.Cream, Pick("wave")),
                ("Not now", "#EADCC6", "#CDB999", Hud.Ink, (Action)(() => ui.HideMemo())));
        }

        private void PressSticker(string kind)
        {
            var spot = FreeSpotNear(new Vector2(ai.x + Rnd(-.3f, .3f), ai.z + PetRadius() + .3f), null);
            PlaceStickerAt(kind, spot.x, spot.y, Rnd(-.4f, .4f));
            visitSticker = kind;
            sfx.Snap();
            Buzz(12);
            Glints(new Vector3(spot.x, Y0 + .1f, spot.y), "#FFF3D6", 4);
            VisitAct("sticker");
        }

        /// <summary>A sticker lying flat on the floor.</summary>
        private Transform PlaceStickerAt(string kind, float x, float z, float turn)
        {
            var mat = ThreeMat.Basic(Color.white, 1, ThreeMat.Blend.Alpha, Textures.Sticker(kind), true, false);
            var t = Node.Mesh(room, ThreeGeo.Plane(.22f, .22f), mat, x, Y0 + .006f, z, shadow: false);
            t.localRotation = Quaternion.Euler(-90, turn * Mathf.Rad2Deg, 0);
            Node.SetLayer(t, HomeLayer);
            return t;
        }

        /// <summary>Your own room: the stickers friends left (each for a day), out on the floor.</summary>
        private void PlaceStickers()
        {
            if (visiting || Rules == null) return;
            Rules.PruneStickers();
            foreach (var st in S.stickers)
            {
                var p = FreeSpotNear(new Vector2(Mathf.Cos(st.a), Mathf.Sin(st.a)) * FLOOR_R * st.r, null);
                PlaceStickerAt(st.kind, p.x, p.y, st.a * 3);
            }
        }

        /// <summary>"While you were away": each friend who visited and exactly what they did for your squishy.</summary>
        private System.Collections.IEnumerator ShowVisitGifts(List<GameRules.VisitGift> gifts)
        {
            while (ui.IntroOn || ui.NightOn || ui.CelebrationOn || visiting || mode != "home") yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForSecondsRealtime(1f);
            var sb = new System.Text.StringBuilder();
            int coins = 0;
            string fav = Own.Fav.name;
            foreach (var g in gifts)
            {
                var did = new List<string>();
                if (g.hunger > 0) did.Add("gave " + fav + " a snack (+" + Mathf.RoundToInt(g.hunger * 100) + "% hunger)");
                if (g.play > 0) did.Add("squished them (+" + Mathf.RoundToInt(g.play * 100) + "% play)");
                if (g.watered) did.Add("watered your plant");
                if (g.together > 0) did.Add("came round to play (+" + Mathf.RoundToInt(g.together * 100) + "% play)");
                if (g.clean > 0) did.Add("pampered " + fav + " (+" + Mathf.RoundToInt(g.clean * 100) + "% clean)");
                if (g.sticker != null) did.Add("left a " + g.sticker + " sticker");
                if (did.Count == 0) did.Add("popped in to say hi");
                sb.Append(g.name).Append(" ").Append(string.Join(", ", did)).Append(". +").Append(g.coins).Append(" coins.\n");
                coins += g.coins;
            }
            sfx.Chime();
            ui.ShowDialog(gifts.Count == 1 ? "A friend visited!" : gifts.Count + " friends visited!", sb.ToString().TrimEnd(), "icon:friends",
                ("Lovely! (+" + coins + " coins)", "#6F9A74", "#4C7552", Hud.Cream, (Action)(() => ui.HideMemo())));
        }

        /// <summary>One caring thing done on a visit: coins for you (once per kind), and a note for your friend.</summary>
        private void VisitAct(string kind)
        {
            if (!visiting || !visitDone.Add(kind)) return;
            visitActs++;
            ui.SetVisitTodos(VisitTodos());
            if (visitId == null) { Floater("Test visit: " + kind); return; }
            ownRules.AddCoins(C.rules.visitCoins);
            ui.SetCoins(ownRules.S.coins);
            sfx.Coin(); // no toast: the coins counter and the sound are enough
            _ = Online.RecordVisit(visitId, visitActs, string.Join(",", visitDone.Select(k => k == "sticker" && visitSticker != null ? "sticker:" + visitSticker : k)), ownRules.Fav.name); // the squishy's name: your own name stays on the phone
        }

        private void VisitSnack()
        {
            var own = ownRules.S;
            int k = Array.FindIndex(own.snacks, n => n > 0);
            if (k < 0) { Floater("No snacks", "bad"); sfx.Bonk(); return; }
            if (visitDone.Contains("feed")) { Floater("They're full"); return; }
            own.snacks[k]--;
            pet.Chewing = true;
            pet.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Grin, 2.5f);
            Floater("Yum!");
            VisitAct("feed");
        }

        private void EndVisit(bool now)
        {
            if (!visiting) return;
            bool realVisit = visitId != null;
            Action back = () =>
            {
                buddy = null; // its model goes with their room
                brush = null;
                pamperT = -1;
                bPlayT = -1;
                visiting = false;
                Rules = ownRules;
                ownRules = null;
                if (realVisit) TaskEvent("visit");
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
            pet.SetCosmetics(Rules.Worn(S.hat), Rules.Worn(S.face), Rules.Worn(S.neck));
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
