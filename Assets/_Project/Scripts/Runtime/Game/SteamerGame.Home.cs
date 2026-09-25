using System.Collections.Generic;
using System.Linq;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    public sealed partial class SteamerGame
    {
        private static readonly string[] NeedWord = { "Hungry!", "Bored!", "Sleepy!", "Grubby!" };
        private Transform selRing;
        private Material selRingMat;
        private float needT, zTimer, happyTAcc, petYawY, petYawZ;
        private (float from, float to, float t)? growAnim;
        private int? grewTo;
        private Transform cookTool, cookDish;
        private Item cookStove;
        private int cookTi = -1;

        // ---------------- building ----------------

        private void BuildHome()
        {
            HR = C.roomLevels[S.roomLv].r;
            FLOOR_R = HR * .86f;
            Node.Mesh(home, ThreeGeo.RBox(9, .6f, 9, .2f), ThreeMat.M("#A87A4F"), 0, -.3f, 0, shadow: false);
            room = Node.Group(home, "room");
            homeWall = new SteamerModel(room, Mathf.RoundToInt(60 * HR / 2.3f), HR);
            curSkin = C.skins[Mathf.Clamp(S.curSkin, 0, C.skins.Length - 1)];
            homeWall.Skin(curSkin);
            homeSteam = new ParticlePool(70, ThreeGeo.Ico1(), SteamMat(), false, HomeLayer);
            drops = new ParticlePool(40, ThreeGeo.Ico1(), WaterMat(), false, HomeLayer);
            foreach (var st in S.items) AddItem(st);
            pet = new SquishyModel(room, .1f);
            selRingMat = ThreeMat.Basic(ThreeMat.Lin("#FFD27A"), .9f, ThreeMat.Blend.Alpha, depthWrite: false);
            selRing = Node.Mesh(room, ThreeGeo.FlatRing(.92f, 1, 40), selRingMat, 0, 0, 0, shadow: false);
            selRing.gameObject.SetActive(false);
            Node.SetLayer(home, HomeLayer);
            var tomb = items.Find(i => i.arch == "tomb");
            ai.x = 0; ai.z = .3f;
        }

        private static Material SteamMat()
        {
            var m = ThreeMat.Lambert(ThreeMat.Lin("#FFFBF2"), ThreeMat.Lin("#FFF1DA") * .4f);
            m.SetFloat("_ReceiveShadows", 0);
            return m;
        }

        private static Material WaterMat()
        {
            var m = ThreeMat.Lambert(ThreeMat.Lin("#DDF1F7"), ThreeMat.Lin("#BFE6F0") * .5f);
            m.SetFloat("_ReceiveShadows", 0);
            return m;
        }

        private void HPuff(Vector3 p, Vector3 v, float s, float life, float drag = 2, float g = .5f) { homeSteam.Spawn(p, v, s, life, drag, g); }

        private Item AddItem(PieceState st)
        {
            var it = new Item { st = st, a = C.Type(st.Arch) };
            it.g = ItemModels.Build(C, it.arch, it.style, room, it.parts);
            it.g.localPosition = new Vector3(st.x, Y0, st.z);
            it.g.RotY(st.ry);
            Node.SetLayer(it.g, HomeLayer);
            items.Add(it);
            if (it.arch == "stove") DecorateStove(it);
            if (it.arch == "lamp") SetLampShade(it);
            return it;
        }

        private Item AddItem(string key, float x, float z, float ry)
        {
            var st = new PieceState { key = key, x = x, z = z, ry = ry };
            S.items.Add(st);
            return AddItem(st);
        }

        private void RemoveItem(Item it)
        {
            items.Remove(it);
            S.items.Remove(it.st);
            Node.Destroy(it.g);
        }

        private void Restyle(Item it, string styleId)
        {
            var pos = it.g.localPosition;
            Node.Destroy(it.g);
            it.parts = new ItemParts();
            it.st.key = it.arch + ":" + styleId;
            it.g = ItemModels.Build(C, it.arch, styleId, room, it.parts);
            it.g.localPosition = pos;
            it.g.RotY(it.ry);
            Node.SetLayer(it.g, HomeLayer);
            DecorateStove(it);
            if (it.arch == "lamp") SetLampShade(it);
        }

        private void DecorateStove(Item it)
        {
            if (it.arch != "stove") return;
            KitchenModels.DecorateStove(Rules, it.g, it.parts, it.style);
            Node.SetLayer(it.g, HomeLayer);
        }

        private void DecorateStoves() { foreach (var it in items) DecorateStove(it); }

        private int PieceCount(string arch) { return Rules.PieceCount(arch); }
        private int DecorCount() { return Rules.DecorCount(); }

        private float PetRadius() { return pet.Scale * 1.14f; }

        private List<Obstacle> WorldCircles(Item it)
        {
            var res = new List<Obstacle>();
            float c = Mathf.Cos(it.ry), s = Mathf.Sin(it.ry);
            var cs = it.a.circles;
            if (cs == null || cs.Length == 0) { res.Add(new Obstacle { x = it.tx, z = it.tz, r = it.a.r, it = it }); return res; }
            for (int k = 0; k + 2 < cs.Length; k += 3)
                res.Add(new Obstacle { x = it.tx + cs[k] * c + cs[k + 1] * s, z = it.tz - cs[k] * s + cs[k + 1] * c, r = cs[k + 2], it = it });
            return res;
        }

        private void RebuildObstacles()
        {
            obstacles = new List<Obstacle>();
            foreach (var it in items) { if (it.a.walk || it.arch == "ball") continue; obstacles.AddRange(WorldCircles(it)); }
            ComputeComfort();
            PlaceLamps();
        }

        private void ComputeComfort()
        {
            comfort = Rules.Comfort(S.items, out setBonus);
            ui.SetComfort("Comfort " + Mathf.RoundToInt(comfort) + (setBonus.Length > 0 ? " · " + setBonus + " set" : ""));
        }

        private void PlaceLamps()
        {
            var lamps = items.FindAll(i => i.arch == "lamp");
            for (int i = 0; i < 2; i++)
            {
                if (i < lamps.Count) SceneLighting.Lamp(i, new Vector3(lamps[i].tx, Y0 + .7f, lamps[i].tz), lamps[i].st.lampOn ? .8f : 0);
                else SceneLighting.Lamp(i, new Vector3(0, -100, 0), 0);
            }
        }

        private void SetLampShade(Item it) { it.parts.shadeMat.SetColor("_EmissionColor", ThreeMat.Lin("#FFB65C") * (it.st.lampOn ? .6f : 0)); }

        // ---------------- squishy ----------------

        private void SetPet(int idx)
        {
            S.favIdx = idx;
            pet.SetFinish(C.finishes[idx]);
            pet.Scale = C.sizes[Rules.FavSizeIdx].s;
            ui.SetName(C.finishes[idx].name);
            UpdateSub();
        }

        private void UpdateSub() { ui.SetSub("Day " + S.age + " · " + C.sizes[Rules.FavSizeIdx].name); }

        private void DrawNeeds() { ui.DrawNeeds(S.needs); }

        private float Condition() { return Rules.Condition(); }

        // ---------------- activities ----------------

        private Spot SpotOf(Item it, ActivityData act)
        {
            float px = it.tx, pz = it.tz, rr = it.a.r, front = act.front > 0 ? act.front : .2f, sx, sz;
            if (act.toward || string.IsNullOrEmpty(it.a.face))
            {
                float dx = ai.x - px, dz = ai.z - pz;
                if (act.toward) { dx = -px; dz = -pz; }
                float l = Mathf.Sqrt(dx * dx + dz * dz);
                if (l < .05f) { dx = 0; dz = 1; l = 1; }
                sx = px + dx / l * (rr + front);
                sz = pz + dz / l * (rr + front);
            }
            else
            {
                sx = px + Mathf.Sin(it.ry) * (rr + front);
                sz = pz + Mathf.Cos(it.ry) * (rr + front);
            }
            float l2 = Mathf.Sqrt(sx * sx + sz * sz);
            if (l2 > FLOOR_R - .12f) { sx *= (FLOOR_R - .12f) / l2; sz *= (FLOOR_R - .12f) / l2; }
            if (act.perch > 0 || act.inside)
            {
                float dx = -px, dz = -pz, l = Mathf.Sqrt(dx * dx + dz * dz);
                if (l == 0) l = 1;
                if (it.a.face == "z") { dx = Mathf.Sin(it.ry); dz = Mathf.Cos(it.ry); l = 1; }
                return new Spot { approach = new Vector2(px + dx / l * (rr + .18f), pz + dz / l * (rr + .18f)), stand = new Vector2(px, pz), y = act.perch > 0 ? act.perch : .06f };
            }
            return new Spot { stand = new Vector2(sx, sz), y = 0, face = new Vector2(px, pz) };
        }

        private Obstacle? Blocked(float ax, float az, float bx, float bz, Item skip)
        {
            foreach (var o in obstacles)
            {
                if (o.it == skip) continue;
                float dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                if (l2 == 0) l2 = 1e-6f;
                float t = ((o.x - ax) * dx + (o.z - az) * dz) / l2;
                if (t < .03f || t > .97f) continue;
                float px = ax + dx * t - o.x, pz = az + dz * t - o.z, rr = o.r + PetRadius() + .04f;
                if (px * px + pz * pz < rr * rr) return o;
            }
            return null;
        }

        private PathPt Detour(float ax, float az, float bx, float bz, Item skip)
        {
            var ob = Blocked(ax, az, bx, bz, skip);
            if (ob == null) return null;
            var o = ob.Value;
            float dx = bx - ax, dz = bz - az, l = Mathf.Sqrt(dx * dx + dz * dz);
            if (l == 0) l = 1;
            float nx = -dz / l, nz = dx / l;
            float side = ((o.x - ax) * nx + (o.z - az) * nz) > 0 ? -1 : 1, off = o.r + PetRadius() + .18f;
            float wx = o.x + nx * off * side, wz = o.z + nz * off * side;
            if (Mathf.Sqrt(wx * wx + wz * wz) > FLOOR_R - .2f) { wx = o.x - nx * off * side; wz = o.z - nz * off * side; }
            return new PathPt { x = wx, z = wz, y = 0 };
        }

        private void PlanPath(float tx, float tz, float ty, Vector2? approach, Item skip)
        {
            var pts = new List<PathPt>();
            float cx = ai.x, cz = ai.z;
            if (ai.y > .02f && ai.perch.HasValue) { pts.Add(new PathPt { x = ai.perch.Value.x, z = ai.perch.Value.y, y = 0, big = true }); cx = ai.perch.Value.x; cz = ai.perch.Value.y; }
            float gx = approach.HasValue ? approach.Value.x : tx, gz = approach.HasValue ? approach.Value.y : tz;
            var w = Detour(cx, cz, gx, gz, skip);
            if (w != null) { pts.Add(w); var w2 = Detour(w.x, w.z, gx, gz, skip); if (w2 != null) pts.Add(w2); }
            if (approach.HasValue) pts.Add(new PathPt { x = approach.Value.x, z = approach.Value.y, y = 0 });
            pts.Add(new PathPt { x = tx, z = tz, y = ty, big = ty > .09f });
            ai.path = pts;
            ai.seg = null;
        }

        private Item NearestRole(string role)
        {
            Item best = null;
            float bd = 1e9f;
            foreach (var it in items)
            {
                if (it.a.role != role) continue;
                float d = Dist(it.tx - ai.x, it.tz - ai.z);
                if (d < bd) { bd = d; best = it; }
            }
            return best;
        }

        private static float Dist(float dx, float dz) { return Mathf.Sqrt(dx * dx + dz * dz); }

        private bool SeatNear(Item t) { return items.Any(it => (it.a.role == "seat" || it.a.role == "lounge") && Dist(it.tx - t.tx, it.tz - t.tz) < 1.1f); }

        /// <summary>useItem: user=true means the player asked, so the need can fill completely.</summary>
        private void UseItem(Item it, bool user, RecipeData recipe = null)
        {
            if (S.dead) return;
            string role = it.a.role;
            if (role == "decor") { FloaterAt(it, "Decor · +" + it.a.comfort + " comfort"); return; }
            if (string.IsNullOrEmpty(role)) { if (it.a.cat == "Wall") FloaterAt(it, "Room divider"); return; }
            if (role == "seat")
            {
                var t = items.Find(x => x.a.role == "tea" && Dist(x.tx - it.tx, x.tz - it.tz) < 1.1f);
                if (t != null) { it = t; role = "tea"; }
            }
            var act = C.Activity(role);
            if (act == null) return;
            if (it.a.size > 0 && Rules.FavSizeIdx < it.a.size) { FloaterAt(it, "Needs " + C.sizes[it.a.size].name + " size", "bad"); return; }
            if (act.needSeat && !SeatNear(it)) { FloaterAt(it, "Needs a seat nearby", "bad"); return; }
            if (role == "eat" && recipe == null) recipe = C.recipes[0];
            if (Condition() < .1f && !user) return;
            CleanupCook();
            ai.act = new Activity { it = it, role = role, act = act, recipe = recipe };
            ai.actT = 0;
            ai.kicks = 0;
            ai.self = !user;
            ai.target = it;
            if (role == "play")
            {
                ai.mode = "chase";
                ai.path = new List<PathPt>();
                ai.seg = null;
                if (ai.y > .02f && ai.perch.HasValue) ai.path.Add(new PathPt { x = ai.perch.Value.x, z = ai.perch.Value.y, y = 0, big = true });
            }
            else if (role == "tea")
            {
                Item seat = null;
                float bd = 1e9f;
                foreach (var x in items)
                {
                    if (x.a.role != "seat" && x.a.role != "lounge") continue;
                    float d = Dist(x.tx - it.tx, x.tz - it.tz);
                    if (d < 1.1f && d < bd) { bd = d; seat = x; }
                }
                float dx = seat.tx - it.tx, dz = seat.tz - it.tz, l = Dist(dx, dz);
                if (l == 0) l = 1;
                var st = C.Style(seat.style);
                bool low = seat.a.role == "lounge" || (st != null && st.low);
                var s = new Spot { approach = new Vector2(seat.tx + dx / l * .32f, seat.tz + dz / l * .32f), stand = new Vector2(seat.tx, seat.tz), y = low ? .1f : .28f, face = new Vector2(it.tx, it.tz) };
                ai.spot = s;
                ai.mode = "walk";
                PlanPath(s.stand.x, s.stand.y, s.y, s.approach, seat);
            }
            else
            {
                var s = SpotOf(it, act);
                ai.spot = s;
                ai.mode = "walk";
                PlanPath(s.stand.x, s.stand.y, s.y, s.approach, it);
            }
            ui.ShowBubble(string.IsNullOrEmpty(act.need) ? "idle" : act.need, (recipe != null && role == "eat" ? recipe.name : act.label) + (user ? "" : " (on its own)"), false);
        }

        /// <summary>The squishy looks after itself, but only up to about half.</summary>
        private void SelfCare(int need)
        {
            string[][] map = { new[] { "snack", "eat" }, new[] { "play" }, new[] { "lounge", "bed" }, new[] { "wash" } };
            foreach (var role in map[need])
            {
                if (role == "snack" && !S.snacks.Any(n => n > 0)) continue;
                var it = NearestRole(role);
                if (it != null && !(it.a.size > 0 && Rules.FavSizeIdx < it.a.size)) { UseItem(it, false); if (ai.act != null) return; }
            }
            string label = need == Needs.Clean ? "Grooming" : need == Needs.Hunger ? "Nibbling crumbs" : need == Needs.Rest ? "Dozing" : "Wiggling";
            ai.act = new Activity { role = "makeDo", act = new ActivityData { label = label, need = Needs.Names[need], dur = 4, rate = .06f } };
            ai.mode = "act";
            ai.actT = 0;
            ai.self = true;
            ai.spot = null;
            ai.target = null;
            ui.ShowBubble(Needs.Names[need], label + " (on its own)", false);
        }

        private void Wander()
        {
            float tx = 0, tz = 0;
            for (int k = 0; k < 8; k++)
            {
                float a = Rnd(0, Mathf.PI * 2), r = Rnd(.2f, 1.4f);
                tx = Mathf.Cos(a) * r;
                tz = Mathf.Sin(a) * r;
                if (!obstacles.Any(o => Dist(o.x - tx, o.z - tz) < o.r + .15f)) break;
            }
            ai.act = null;
            ai.target = null;
            ai.mode = "walk";
            PlanPath(tx, tz, 0, null, null);
            ui.HideBubble();
        }

        private void Autonomous()
        {
            float cond = Condition();
            if (cond < .12f) { ai.idleT = 4; ui.ShowBubble(Needs.Names[Rules.LowestNeed()], "Help…", true); return; }
            int low = Rules.LowestNeed();
            if (S.needs[low] < .35f && Random.value < .8f) { SelfCare(low); return; }
            var plants = items.FindAll(i => i.arch == "plant" && i.st.wilt > .5f);
            if (plants.Count > 0 && Random.value < .3f) { UseItem(plants[0], false); return; }
            if (Random.value < .65f) Wander(); else ai.idleT = Rnd(2, 4);
        }

        private void FinishActivity()
        {
            CleanupCook();
            var a = ai.act;
            if (a != null && !string.IsNullOrEmpty(a.act.need))
                Floater((ai.self ? "" : "+") + char.ToUpper(a.act.need[0]) + a.act.need.Substring(1) + (ai.self ? " (half)" : ""));
            ai.act = null;
            ai.target = null;
            ai.mode = "idle";
            ai.idleT = Rnd(2.5f, 5);
            ui.HideBubble();
            if (ai.y > .02f) Wander();
            var sh = items.Find(i => i.arch == "shower");
            if (sh != null && sh.parts.curtain != null) sh.parts.openT = 1;
        }

        private void FreePet()
        {
            if (ai.y > .02f)
            {
                var it = items.Find(i => (i.a.role == "bed" || i.a.role == "bath" || i.a.role == "lounge" || i.a.role == "bounce" || i.a.role == "seat") && Dist(i.tx - ai.x, i.tz - ai.z) < .25f);
                if (it != null) { ai.perch = SpotOf(it, C.Activity(it.a.role) ?? C.Activity("bed")).approach; return; }
                ai.y = 0;
            }
            foreach (var o in obstacles)
            {
                float dx = ai.x - o.x, dz = ai.z - o.z, d = Dist(dx, dz);
                if (d < o.r + PetRadius())
                {
                    float l = d > 0 ? d : 1;
                    ai.x = o.x + (d > 0 ? dx / l : 1) * (o.r + PetRadius() + .05f);
                    ai.z = o.z + (d > 0 ? dz / l : 0) * (o.r + PetRadius() + .05f);
                }
            }
        }

        private void StartAct()
        {
            var A = ai.act;
            if (A == null) return;
            if (A.role == "lamp" && A.it != null)
            {
                A.it.st.lampOn = !A.it.st.lampOn;
                SetLampShade(A.it);
                PlaceLamps();
                sfx.Tap();
                FloaterAt(A.it, A.it.st.lampOn ? "Lights on" : "Lights off · full rest");
            }
            if (A.role == "plant" && A.it != null) { A.it.st.wilt = 0; ComputeComfort(); }
            if (A.role == "shower" && A.it != null) A.it.parts.openT = 0;
            if (A.recipe != null && A.role == "eat")
            {
                var rc = A.recipe;
                if (rc.ing.Length > 0 && !ai.self)
                {
                    foreach (var i in rc.ing) S.pantry[i] = Mathf.Max(0, S.pantry[i] - 1);
                    foreach (var i in rc.tools)
                    {
                        S.toolDur[i] = Mathf.Max(0, S.toolDur[i] - 1);
                        if (S.toolDur[i] == 0) { int ti = i; var st = A.it; Later(.9f, () => FloaterAt(st, C.tools[ti].name + " broke! Fix it in the shop", "bad")); }
                    }
                }
                Floater("Cooking " + rc.name);
                var pan = A.it.parts.pan;
                cookTi = rc.tools.Length > 0 ? rc.tools[0] : -1;
                cookTool = cookTi >= 0 ? KitchenModels.Tool(C, S, cookTi, null, A.it.g) : null;
                if (cookTool != null)
                {
                    cookTool.localScale = Vector3.one * .55f;
                    cookTool.localPosition = pan.localPosition + new Vector3(0, .04f, 0);
                    Node.SetLayer(cookTool, HomeLayer);
                    pan.gameObject.SetActive(cookTi != 3 && cookTi != 5);
                }
                cookDish = KitchenModels.Dish(C, rc == null ? 0 : System.Array.IndexOf(C.recipes, rc), room);
                cookDish.localScale = Vector3.one * .55f;
                cookDish.gameObject.SetActive(false);
                Node.SetLayer(cookDish, HomeLayer);
                cookStove = A.it;
            }
            if (A.role == "snack")
            {
                int k = System.Array.FindIndex(S.snacks, n => n > 0);
                if (k >= 0)
                {
                    S.snacks[k]--;
                    Floater("-1 " + C.snacks[k].name);
                    if (!ai.self) TaskEvent("snack");
                }
                else { Floater("No snacks · buy some in the shop", "bad"); ai.actT = A.act.dur; }
            }
        }

        private void CleanupCook()
        {
            if (cookTool != null) Node.Destroy(cookTool);
            if (cookDish != null) Node.Destroy(cookDish);
            if (cookStove != null && cookStove.parts.pan != null) cookStove.parts.pan.gameObject.SetActive(true);
            cookTool = cookDish = null;
            cookStove = null;
        }

        private void EndAct()
        {
            var A = ai.act;
            if (A != null && !ai.self)
            {
                if (A.role == "plant") { FloaterAt(A.it, "Watered · +comfort"); TaskEvent("water"); }
                if (A.role == "eat" && A.recipe != null && A.recipe.ing.Length > 0)
                {
                    int i = System.Array.IndexOf(C.recipes, A.recipe), l0 = Rules.RecipeLvl(i);
                    S.recipeXP[i]++;
                    int l1 = Rules.RecipeLvl(i);
                    TaskEvent("cook");
                    if (l1 > l0) { var rc = A.recipe; Later(.6f, () => Floater(rc.name + " ★" + l1 + "!")); }
                }
                if (A.role == "bed" && !items.Any(x => x.arch == "lamp" && x.st.lampOn)) TaskEvent("nap");
                if (A.role == "tea") TaskEvent("tea");
                if ((A.role == "bath" || A.role == "shower") && A.scrubbed) TaskEvent("scrub");
            }
            FinishActivity();
        }

        // ---------------- death ----------------

        private bool _dying;

        private void Die()
        {
            S.dead = true;
            ai.mode = "dead";
            ui.HideBubble();
            int low = Rules.LowestNeed();
            string why = new[] { "Left hungry", "Left lonely", "Left exhausted", "Left grubby" }[low];
            sfx.Sad();
            Buzz(60, 80, 60);
            _dying = true;
            Later(1.4f, () =>
            {
                _dying = false;
                pet.Pivot.gameObject.SetActive(false);
                var t = AddItem("tomb:", ai.x, ai.z, 0);
                t.g.localScale = Vector3.one * .01f;
                t.grow = 0;
                RebuildObstacles();
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2;
                    HPuff(new Vector3(ai.x + Mathf.Cos(a) * .15f, Y0 + .08f, ai.z + Mathf.Sin(a) * .15f), new Vector3(Mathf.Cos(a) * .8f, .5f, Mathf.Sin(a) * .8f), .07f, .7f, 3, .3f);
                }
                ShowMemo(why);
            });
        }

        private void ShowMemo(string why)
        {
            ui.ShowMemo(Rules.Fav.name + " has passed away", why + " for too long. Its tombstone stays in the room, and all your things carry over to your next squishy.");
            ui.ShowHud(false);
            WriteSave();
        }

        /// <summary>Resuming a save whose squishy died while away.</summary>
        private void ResumeDead()
        {
            ai.mode = "dead";
            var t = items.Find(i => i.arch == "tomb");
            if (t == null) { S.dead = false; Die(); return; }
            pet.Pivot.gameObject.SetActive(false);
            ShowMemo(new[] { "Left hungry", "Left lonely", "Left exhausted", "Left grubby" }[Rules.LowestNeed()]);
        }

        public void NextSquishy()
        {
            ui.HideMemo();
            ui.ShowHud(true);
            var opts = S.squishOwned.Select(q => q.i).Where(i => i != S.favIdx).ToList();
            int next = opts.Count > 0 ? opts[0] : 0;
            Rules.RemoveSquish(S.favIdx);
            if (Rules.SquishCount(next) == 0) Rules.SetSquish(next, 1);
            S.dead = false;
            S.deathClock = 0;
            S.age = 1;
            SetPet(next);
            pet.Grey = 0;
            pet.Pivot.gameObject.SetActive(true);
            for (int k = 0; k < 4; k++) S.needs[k] = .75f;
            DrawNeeds();
            var t = items.Find(i => i.arch == "tomb");
            float x = 0, z = .3f;
            if (t != null) { x = t.tx + .5f; z = t.tz; }
            ai.x = x; ai.z = z; ai.y = 0;
            FreePet();
            ai.mode = "idle";
            ai.idleT = 2;
            ai.act = null;
            Floater("Welcome, " + C.finishes[next].name + "!");
            sfx.Chime();
        }

        // ---------------- simulation step ----------------

        private void YawTo(float tx, float tz, float dt, float rate = 8)
        {
            float want = Mathf.Atan2(tx - ai.x, tz - ai.z), d = want - petYawY;
            d = Mathf.Atan2(Mathf.Sin(d), Mathf.Cos(d));
            petYawY += d * Mathf.Min(1, dt * rate);
        }

        private void KickBall(Item bl, float power = 0)
        {
            float dx = bl.tx - ai.x, dz = bl.tz - ai.z, l = Dist(dx, dz);
            if (l == 0) l = 1;
            float a = Mathf.Atan2(dz / l, dx / l) + Rnd(-.5f, .5f), sp = power > 0 ? power : Rnd(2.6f, 3.8f);
            bl.vx = Mathf.Cos(a) * sp;
            bl.vz = Mathf.Sin(a) * sp;
            bl.bv = -5;
            sfx.Kick();
            Buzz(10);
            shake = Mathf.Max(shake, .12f);
            HPuff(new Vector3(bl.tx, Y0 + .08f, bl.tz), new Vector3(-dx / l * .6f, .5f, -dz / l * .6f), .07f, .4f, 3, .3f);
        }

        private void StepHome(float dt)
        {
            if (S.dead || _dying) { pet.Update(dt, .35f, true, 1); ApplyPetTransform(0, true); AnimateFurniture(dt); return; }
            float sdt = dt * timeScale;
            int age0 = S.age;
            bool died = Rules.StepCare(sdt, comfort);
            if (S.age != age0) UpdateSub();
            needT += dt;
            if (needT > .5f) { needT = 0; DrawNeeds(); if (Random.value < .2f) ComputeComfort(); }
            foreach (var it in items)
                if (it.arch == "plant")
                {
                    it.st.wilt = Mathf.Min(1, it.st.wilt + sdt * C.rules.plantWiltRate);
                    it.parts.topWiltX = it.st.wilt * .35f;
                    ApplyPlantTop(it);
                }
            if (died) { Die(); return; }
            float cond = Condition();
            var st = GameRules.Stage(cond);
            ui.SetCond(st[0], st[1]);
            float droop = Sstep(.5f, .08f, cond);
            pet.Grey = Sstep(.25f, .03f, cond) * .85f;
            if (cond > .5f && mode == "home")
            {
                TaskEvent("happy", dt);
                S.happyT += dt * Rules.HappyRate(comfort) / 60;
                if (S.happyT >= 1) { int n = Mathf.FloorToInt(S.happyT); S.happyT -= n; Rules.AddCoins(n); }
            }
            if (homeWall.Grow.HasValue)
            {
                homeWall.Grow = Mathf.Min(1, homeWall.Grow.Value + dt * 1.2f);
                float s0 = homeWall.Group.localScale.x;
                homeWall.Group.localScale = Vector3.one * (s0 + (1 - s0) * Mathf.Min(1, dt * 5));
                if (homeWall.Grow >= 1) { homeWall.Group.localScale = Vector3.one; homeWall.Grow = null; }
            }
            float slowMove = 1 - droop * .55f, extra = 0, lift = 0;
            bool sleeping = ai.mode == "act" && ai.act != null && ai.act.act.sleep;
            float hopH = pet.Scale * .9f;
            if (mode == "edit" || pet.Held) { }
            else if (ai.mode == "idle")
            {
                ai.idleT -= dt;
                if (ai.idleT <= 0) Autonomous();
                var cp = Space3.U(cam.transform.position);
                YawTo(cp.x, cp.z, dt, 3);
                if (!reduce) extra = .02f * Mathf.Sin(time * 2.2f * slowMove);
                ai.warnT -= dt;
                if (ai.warnT <= 0)
                {
                    ai.warnT = 6;
                    int low = Rules.LowestNeed();
                    if (S.needs[low] < .3f) { ui.ShowBubble(Needs.Names[low], NeedWord[low], true); Later(2.2f, () => { if (ai.mode == "idle") ui.HideBubble(); }); }
                }
            }
            else if (ai.mode == "walk" || ai.mode == "chase") StepWalk(dt, slowMove, droop, hopH, ref lift);
            else if (ai.mode == "act") StepAct(dt, ref extra, ref lift);
            StepBalls(dt);
            float rr = Dist(ai.x, ai.z);
            if (rr > FLOOR_R - .1f) { ai.x *= (FLOOR_R - .1f) / rr; ai.z *= (FLOOR_R - .1f) / rr; }
            if (growAnim.HasValue)
            {
                var ga = growAnim.Value;
                ga.t = Mathf.Min(1, ga.t + dt / 1.2f);
                float k = ga.t;
                pet.Scale = ga.from + (ga.to - ga.from) * (1 - Mathf.Pow(1 - k, 3)) + Mathf.Sin(k * Mathf.PI * 3) * (1 - k) * .01f;
                growAnim = k >= 1 ? ((float, float, float)?)null : ga;
            }
            pet.Update(dt, extra, sleeping, droop);
            ApplyPetTransform(lift, sleeping);
            AnimateFurniture(dt);
        }

        private void ApplyPetTransform(float lift, bool sleeping)
        {
            pet.Pivot.localPosition = new Vector3(ai.x, Y0 + .02f + ai.y + lift, ai.z);
            petYawZ = sleeping ? Mathf.Sin(ai.actT * .8f) * .05f : petYawZ * .9f;
            Node.Rot(pet.Yaw, 0, petYawY, petYawZ);
        }

        private void StepWalk(float dt, float slowMove, float droop, float hopH, ref float lift)
        {
            var ball = ai.mode == "chase" ? ai.act.it : null;
            if (ball != null && ai.path.Count == 0 && ai.seg == null) ai.path = new List<PathPt> { new PathPt { x = ball.tx, z = ball.tz, y = 0, chase = true } };
            if (ai.seg == null && ai.path.Count > 0)
            {
                var p = ai.path[0];
                ai.path.RemoveAt(0);
                ai.seg = new Seg { fx = ai.x, fz = ai.z, fy = ai.y, tx = p.x, tz = p.z, ty = p.y, big = p.big, chase = p.chase, d = 0, len = Mathf.Max(.001f, Dist(p.x - ai.x, p.z - ai.z)) };
            }
            var s = ai.seg;
            if (s == null) return;
            if (s.chase) { s.tx = ball.tx; s.tz = ball.tz; s.fx = ai.x; s.fz = ai.z; s.d = 0; s.len = Mathf.Max(.001f, Dist(s.tx - s.fx, s.tz - s.fz)); }
            float speed = (s.big ? 1f : (s.chase ? 1.15f : .7f)) * slowMove * (.8f + pet.Scale * 2), step = speed * dt;
            if (s.chase) { float reach = PetRadius() + ball.a.r; step = Mathf.Min(step, Mathf.Max(0, s.len - reach)); }
            s.d = Mathf.Min(s.len, s.d + step);
            float u = s.d / s.len;
            ai.x = s.fx + (s.tx - s.fx) * u;
            ai.z = s.fz + (s.tz - s.fz) * u;
            ai.y = s.fy + (s.ty - s.fy) * u;
            YawTo(s.tx, s.tz, dt, 10);
            if (s.big) lift = .4f * Mathf.Sin(Mathf.PI * u);
            else
            {
                float prev = ai.hopPh;
                ai.hopPh += step / (.16f + pet.Scale * .6f);
                lift = hopH * (1 - droop * .6f) * Mathf.Abs(Mathf.Sin(Mathf.PI * ai.hopPh));
                if (Mathf.Floor(ai.hopPh) > Mathf.Floor(prev)) { pet.V += 2.2f; if (Random.value < .25f) sfx.Hop(); }
            }
            if (s.chase)
            {
                if (Dist(ball.tx - ai.x, ball.tz - ai.z) <= PetRadius() + ball.a.r + .03f)
                {
                    ai.kicks++;
                    pet.V += 3;
                    KickBall(ball);
                    if (!ai.self) TaskEvent("kick");
                    float cap = ai.self ? .5f : 1;
                    if (S.needs[Needs.Play] < cap) S.needs[Needs.Play] = Mathf.Min(cap, S.needs[Needs.Play] + (ai.self ? .05f : .12f));
                    ai.seg = null;
                    ai.path.Clear();
                    if (ai.kicks >= (ai.self ? 2 : 3)) FinishActivity();
                }
            }
            else if (u >= 1)
            {
                ai.seg = null;
                if (ai.path.Count == 0 && ai.mode == "walk")
                {
                    pet.V += s.big ? 3 : 1.5f;
                    if (ai.act != null)
                    {
                        ai.mode = "act";
                        ai.actT = 0;
                        if (ai.spot != null && ai.spot.approach.HasValue) ai.perch = ai.spot.approach;
                        StartAct();
                    }
                    else { ai.mode = "idle"; ai.idleT = Rnd(2, 4.5f); }
                }
            }
        }

        private void StepAct(float dt, ref float extra, ref float lift)
        {
            var A = ai.act;
            var act = A.act;
            var it = A.it;
            ai.actT += dt;
            float t = ai.actT;
            if (ai.spot != null && ai.spot.face.HasValue) YawTo(ai.spot.face.Value.x, ai.spot.face.Value.y, dt, 6);
            float cap = ai.self ? (act.cap > 0 ? Mathf.Min(act.cap, .5f) : .5f) : (act.cap > 0 ? act.cap : 1);
            float rate = act.rate;
            if (A.role == "bed") { bool lampOn = items.Any(i => i.arch == "lamp" && i.st.lampOn); if (lampOn && !ai.self) rate *= .5f; }
            float capN = cap;
            const float COOK = 2.4f;
            bool eating = A.role == "eat" && t > COOK;
            if (A.recipe != null)
            {
                int ri = System.Array.IndexOf(C.recipes, A.recipe);
                float mul = 1 + .1f * (Rules.RecipeLvl(ri) - 1);
                rate = eating ? A.recipe.hunger * mul / (act.dur - COOK) : 0;
                capN = ai.self ? .5f : (A.recipe.cap > 0 ? A.recipe.cap : 1);
                if (eating && !string.IsNullOrEmpty(A.recipe.bonusNeed) && !ai.self)
                {
                    int bk = Needs.Index(A.recipe.bonusNeed);
                    S.needs[bk] = Mathf.Min(1, S.needs[bk] + A.recipe.bonus * mul / (act.dur - COOK) * dt);
                }
            }
            void Fill(string k, float r)
            {
                int i = Needs.Index(k);
                if (i >= 0 && S.needs[i] < capN) S.needs[i] = Mathf.Min(capN, S.needs[i] + r * dt);
            }
            if (!string.IsNullOrEmpty(act.need)) Fill(act.need, rate);
            if (!string.IsNullOrEmpty(act.also)) Fill(act.also, rate * .5f);
            if (A.role == "eat" && it != null)
            {
                var pan = it.parts.pan;
                if (!eating)
                {
                    extra = .05f * Mathf.Abs(Mathf.Sin(t * 6));
                    if (cookTool != null)
                    {
                        var tl = cookTool;
                        var bp = pan.localPosition;
                        tl.localPosition = new Vector3(bp.x, bp.y + .04f, bp.z);
                        float rx = 0, ryy = 0, rz = 0;
                        if (cookTi == 0) rx = -.3f + Mathf.Sin(t * 10) * .5f;
                        else if (cookTi == 1) { tl.localPosition += new Vector3(0, Mathf.Abs(Mathf.Sin(t * 12)) * .05f, 0); rx = Mathf.Sin(t * 12) * .3f; }
                        else if (cookTi == 2) ryy = t * 6;
                        else if (cookTi == 3) { tl.localPosition += new Vector3(0, Mathf.Abs(Mathf.Sin(t * 6)) * .05f, 0); rz = Mathf.Sin(t * 6) * .3f; }
                        else if (cookTi == 4) { tl.localPosition += new Vector3(Mathf.Sin(t * 5) * .06f, 0, 0); ryy = Mathf.PI / 2; }
                        else if (cookTi == 6) ryy = t * 5;
                        else if (cookTi == 7) rz = Mathf.Sin(t * 14) * .2f;
                        Node.Rot(tl, rx, ryy, rz);
                    }
                    else pan.RotZ(Mathf.Sin(t * 20) * .06f);
                    if (Random.value < dt * (cookTi == 5 ? 14 : 6))
                    {
                        var wp = ThreeWorld(pan);
                        HPuff(new Vector3(wp.x, wp.y + .1f, wp.z), new Vector3(Rnd(-.2f, .2f), .8f, 0), Rnd(.04f, .07f), .8f, 1.5f, .4f);
                    }
                }
                else
                {
                    if (cookTool != null) { Node.Destroy(cookTool); cookTool = null; pan.gameObject.SetActive(true); }
                    if (cookDish != null)
                    {
                        if (!cookDish.gameObject.activeSelf)
                        {
                            cookDish.gameObject.SetActive(true);
                            float dx = it.tx - ai.x, dz = it.tz - ai.z, l = Dist(dx, dz);
                            if (l == 0) l = 1;
                            cookDish.localPosition = new Vector3(ai.x + dx / l * (PetRadius() + .1f), Y0, ai.z + dz / l * (PetRadius() + .1f));
                            sfx.Drop();
                        }
                        float k = 1 - (t - COOK) / (act.dur - COOK);
                        cookDish.localScale = Vector3.one * (.55f * (.35f + .65f * Mathf.Max(0, k)));
                    }
                    extra = .22f * Mathf.Max(0, Mathf.Sin(t * 12));
                }
            }
            else if (A.role == "snack") extra = .18f * Mathf.Max(0, Mathf.Sin(t * 14));
            else if (A.role == "tea" && it != null)
            {
                extra = .06f * Mathf.Sin(t * 3);
                it.parts.pot.RotZ(Mathf.Sin(t * 1.5f) * .25f);
                if (Random.value < dt * 3) { var wp = ThreeWorld(it.parts.pot); HPuff(new Vector3(wp.x, wp.y + .2f, wp.z), new Vector3(0, .6f, 0), .05f, 1, 1.5f, .3f); }
            }
            else if (act.sleep)
            {
                extra = .18f + .04f * Mathf.Sin(t * 1.6f);
                zTimer -= dt;
                if (zTimer <= 0) { zTimer = 1.3f; Floater("z", "z"); }
            }
            else if (A.role == "bath" && it != null)
            {
                lift = -pet.Scale * .35f;
                extra = .05f * Mathf.Sin(t * 6);
                if (Random.value < dt * 8) { var wp = ThreeWorld(it.parts.water); HPuff(new Vector3(wp.x + Rnd(-.18f, .18f), wp.y + .04f, wp.z + Rnd(-.12f, .12f)), new Vector3(0, Rnd(.4f, .9f), 0), Rnd(.03f, .06f), Rnd(.6f, 1), 1, .2f); }
            }
            else if (A.role == "shower" && it != null)
            {
                extra = .05f * Mathf.Sin(t * 7);
                if (Random.value < dt * 28) { var wp = it.Pos; drops.Spawn(new Vector3(wp.x + Rnd(-.12f, .12f), wp.y + .85f, wp.z + Rnd(-.2f, .05f)), new Vector3(0, -2, 0), .015f, .45f, 0, -4); }
            }
            else if (A.role == "wash") extra = .08f * Mathf.Sin(t * 10);
            else if (A.role == "bounce") { lift = Mathf.Abs(Mathf.Sin(t * 4)) * .5f; if (Mathf.Abs(Mathf.Sin(t * 4)) < .08f) extra = .4f; }
            else if (A.role == "plant" && it != null)
            {
                it.parts.topSwayZ = Mathf.Sin(t * 9) * .08f * (1 - t / act.dur);
                ApplyPlantTop(it);
                if (Random.value < dt * 10) { var wp = it.Pos; drops.Spawn(new Vector3(wp.x + Rnd(-.1f, .1f), wp.y + .7f, wp.z + Rnd(-.1f, .1f)), new Vector3(0, -1, 0), .015f, .4f, 0, -3); }
            }
            else if (A.role == "makeDo") extra = .06f * Mathf.Sin(t * 5);
            if (t >= act.dur) EndAct();
        }

        private static void ApplyPlantTop(Item it) { if (it.parts.top != null) Node.Rot(it.parts.top, it.parts.topWiltX, 0, it.parts.topSwayZ); }

        /// <summary>A part's position in three space (room coordinates).</summary>
        private Vector3 ThreeWorld(Transform t) { return world.InverseTransformPoint(t.position); }

        private void StepBalls(float dt)
        {
            foreach (var bl in items)
            {
                if (bl.arch != "ball" || (bl.vx == 0 && bl.vz == 0)) continue;
                float e = Mathf.Exp(-.55f * dt);
                bl.vx *= e;
                bl.vz *= e;
                float bx = bl.tx + bl.vx * dt, bz = bl.tz + bl.vz * dt, sp = Dist(bl.vx, bl.vz);
                float lim = FLOOR_R - .12f, r = Dist(bx, bz);
                if (r > lim)
                {
                    float nx = bx / r, nz = bz / r, dot = bl.vx * nx + bl.vz * nz;
                    if (dot > 0)
                    {
                        bl.vx = (bl.vx - 2 * dot * nx) * .85f;
                        bl.vz = (bl.vz - 2 * dot * nz) * .85f;
                        if (sp > .5f) { sfx.Bonk(); HPuff(new Vector3(nx * lim, Y0 + .1f, nz * lim), new Vector3(0, .4f, 0), .05f, .35f, 3, .2f); }
                    }
                    bx = nx * lim;
                    bz = nz * lim;
                }
                foreach (var o in obstacles)
                {
                    float dx = bx - o.x, dz = bz - o.z, d = Mathf.Max(.001f, Dist(dx, dz)), min = o.r + bl.a.r;
                    if (d >= min) continue;
                    float nx = dx / d, nz = dz / d, dot = bl.vx * nx + bl.vz * nz;
                    if (dot < 0) { bl.vx = (bl.vx - 2 * dot * nx) * .8f; bl.vz = (bl.vz - 2 * dot * nz) * .8f; o.it.bv = -2.5f; if (sp > .5f) sfx.Bonk(); }
                    bx = o.x + nx * min;
                    bz = o.z + nz * min;
                }
                float pdx = bx - ai.x, pdz = bz - ai.z, pd = Mathf.Max(.001f, Dist(pdx, pdz)), pmin = PetRadius() + bl.a.r;
                if (pd < pmin && mode != "edit")
                {
                    float nx = pdx / pd, nz = pdz / pd, dot = bl.vx * nx + bl.vz * nz;
                    if (dot < 0) { bl.vx -= 2 * dot * nx; bl.vz -= 2 * dot * nz; pet.V += 1.5f; }
                    bx = ai.x + nx * pmin;
                    bz = ai.z + nz * pmin;
                }
                bl.tx = bx;
                bl.tz = bz;
                bl.g.localPosition = new Vector3(bx, bl.g.localPosition.y, bz);
                var b = bl.parts.ball;
                // rotation.x += vz*dt/.1; rotation.z -= vx*dt/.1 (applied incrementally, like three's Euler updates)
                b.localRotation = Quaternion.AngleAxis(bl.vz * dt / .1f * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(-bl.vx * dt / .1f * Mathf.Rad2Deg, Vector3.forward) * b.localRotation;
                if (sp < .03f) { bl.vx = 0; bl.vz = 0; }
            }
        }

        /// <summary>Furniture glides to its target, lifts while carried and bounces when tapped or dropped.</summary>
        private void AnimateFurniture(float dt)
        {
            foreach (var it in items)
            {
                var pos = it.g.localPosition;
                if (it.arch != "ball" || mode == "edit" || (it.vx == 0 && it.vz == 0))
                {
                    float k = Mathf.Min(1, dt * (it == moving ? 30 : 12));
                    pos.x += (it.tx - pos.x) * k;
                    pos.z += (it.tz - pos.z) * k;
                }
                float ly = Y0 + (it.lift > 0 ? .28f + .03f * Mathf.Sin(time * 6) : 0);
                pos.y += (ly - pos.y) * Mathf.Min(1, dt * 14);
                it.g.localPosition = pos;
                if (it.bx != 0 || it.bv != 0)
                {
                    it.bv += (-220 * it.bx - 12 * it.bv) * dt;
                    it.bx += it.bv * dt;
                    if (Mathf.Abs(it.bx) < .001f && Mathf.Abs(it.bv) < .01f) { it.bx = 0; it.bv = 0; }
                }
                float gs = 1;
                if (it.grow.HasValue) { it.grow = Mathf.Min(1, it.grow.Value + dt * 1.5f); gs = it.grow.Value; }
                it.g.localScale = new Vector3((1 - it.bx * .18f) * gs, (1 + it.bx * .3f) * gs, (1 - it.bx * .18f) * gs);
                it.g.RotY(it.ry);
                if (it.arch == "shower" && it.parts.curtain != null) AnimateCurtain(it, dt);
            }
            selRing.gameObject.SetActive(mode == "edit" && sel != null);
            if (sel != null)
            {
                selRing.localPosition = new Vector3(sel.g.localPosition.x, Y0 + .06f, sel.g.localPosition.z);
                float s = sel.a.circles != null && sel.a.circles.Length > 0 ? .62f : sel.a.r + .06f;
                selRing.localScale = new Vector3(s, 1, s);
                ThreeMat.SetOpacity(selRingMat, .6f + .3f * Mathf.Sin(time * 6));
            }
        }

        private void AnimateCurtain(Item it, float dt)
        {
            var p = it.parts;
            p.open += (p.openT - p.open) * Mathf.Min(1, dt * 4);
            float o = p.open;
            for (int i = 0; i < p.curtainBase.Length; i++)
            {
                float bx = p.curtainBase[i].x, by = p.curtainBase[i].y;
                float g = -.27f + (bx + .27f) * (1 - o * .82f), amp = .012f + o * .03f + (1 - o) * .006f;
                p.curtainWork[i] = new Vector3(g, by, Mathf.Sin(bx * 30 + time * 1.5f) * amp * (1 + (-by) * .6f));
            }
            p.curtainMesh.vertices = p.curtainWork;
            p.curtainMesh.RecalculateBounds();
            RayPick.Forget(p.curtainMesh);
        }
    }
}
