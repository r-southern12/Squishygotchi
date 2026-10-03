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

        // ---------------- the first run ----------------

        private bool furnishing;

        /// <summary>A new game's room before its first steamer: empty (the pieces are there, hidden, and pop in later).</summary>
        private void HideForIntro()
        {
            foreach (var it in items) it.g.gameObject.SetActive(false);
            pet.Pivot.gameObject.SetActive(false);
            ui.SetHint("");
        }

        /// <summary>The first squishy comes home: the room turns slowly while the starter pieces pop in, then it appears.</summary>
        private void FurnishRoom() { StartCoroutine(FurnishRoomCo()); }

        private System.Collections.IEnumerator FurnishRoomCo()
        {
            if (furnishing) yield break;
            furnishing = true;
            ui.ShowHud(false);
            HideForIntro();
            yield return new WaitForSeconds(.8f);
            // The rug first, then round the room so each piece arrives as the room turns.
            var order = new List<Item>(items);
            order.Sort((a, b) => (a.arch == "rug" ? -10 : Mathf.Atan2(a.tz, a.tx)).CompareTo(b.arch == "rug" ? -10 : Mathf.Atan2(b.tz, b.tx)));
            foreach (var it in order)
            {
                if (it == null || it.g == null) continue;
                it.g.gameObject.SetActive(true);
                it.grow = 0;
                it.g.localScale = Vector3.one * .01f;
                it.bv = -3; // lands with a little wobble
                Glints(new Vector3(it.tx, .3f, it.tz), "#FFE08A", 1);
                sfx.Pop();
                Buzz(8);
                yield return new WaitForSeconds(.32f);
            }
            yield return new WaitForSeconds(.5f);
            // Last, the squishy itself.
            pet.Pivot.gameObject.SetActive(true);
            float size = pet.Scale;
            pet.Scale = .01f;
            growAnim = (.01f, size, 0);
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2; Glints(new Vector3(ai.x + Mathf.Cos(a) * .25f, .25f, ai.z + Mathf.Sin(a) * .25f), "#FFE08A", 1); }
            pet.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Grin, 2.5f);
            sfx.Chime();
            Buzz(20, 30, 20);
            S.intro = 0;
            WriteSave();
            yield return new WaitForSeconds(1.2f);
            furnishing = false;
            ui.ShowHud(true);
            DrawNeeds();
            ui.FlashHint("Welcome home, " + Rules.Fav.name + "!");
        }

        private void BuildHome()
        {
            Later(0, PlaceStickers); // friends' stickers on your floor (after the room is built)
            HR = Rules.RoomRadius();
            FLOOR_R = HR * .86f;
            Node.Mesh(home, ThreeGeo.RBox(9, .6f, 9, .2f), ThreeMat.M("#A87A4F"), 0, -.3f, 0, shadow: false);
            room = Node.Group(home, "room");
            homeWall = new SteamerModel(room, Mathf.RoundToInt(60 * HR / 2.3f), HR);
            curSkin = C.skins[Mathf.Clamp(S.curSkin, 0, C.skins.Length - 1)];
            homeWall.Skin(curSkin);
            UpdateMusic();
            // Steam (cooking): soft, see-through, round wisps (the faceted white puffs read as golf balls).
            homeSteam = new ParticlePool(70, ThreeGeo.Sph(1, 14, 10), SteamMat(), false, HomeLayer);
            fxPool = new ParticlePool(160, ThreeGeo.Plane(1, 1), ThreeMat.Basic(Color.white, 1, ThreeMat.Blend.Additive, Textures.Glint(), true, false), true, HomeLayer) { Glint = true }; // star glints, not blobs
            drops = new ParticlePool(40, ThreeGeo.Ico1(), WaterMat(), false, HomeLayer);
            // Bath bubbles: clear, round, swelling in and popping (they were white puffs that read as clouds).
            // Soap bubbles you can see: a painted bubble (clear middle, bright rim, sheen, highlight) facing the camera.
            // The plain pale spheres they replaced were so faint they vanished over the water.
            bathBubbles = new ParticlePool(90, ThreeGeo.Plane(1, 1), ThreeMat.Basic(Color.white, 1, ThreeMat.Blend.Alpha, Textures.Bubble(), true, false), true, HomeLayer) { Bubble = true };
            foreach (var st in S.items) AddItem(st);
            pet = new SquishyModel(room, .1f);
            selRingMat = ThreeMat.Basic(ThreeMat.Lin("#FFD27A"), .9f, ThreeMat.Blend.Alpha, depthWrite: false);
            selRing = Node.Mesh(room, ThreeGeo.FlatRing(.92f, 1, 40), selRingMat, 0, 0, 0, shadow: false);
            selRing.gameObject.SetActive(false);
            Node.SetLayer(home, HomeLayer);
            var tomb = items.Find(i => i.arch == "tomb");
            ai.x = 0; ai.z = .3f;
        }

        /// <summary>Steam: soft, see-through and round (faceted white puffs read as golf balls).</summary>
        private static Material SteamMat() { return ThreeMat.Basic(ThreeMat.Lin("#FFFBF4"), .3f, ThreeMat.Blend.Alpha, depthWrite: false); }

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
            navDirty = true;
            foreach (var it in items) { if (it.a.walk || it.arch == "ball") continue; obstacles.AddRange(WorldCircles(it)); }
            ComputeComfort();
            PlaceLamps();
        }

        private void ComputeComfort()
        {
            comfort = Rules.Comfort(S.items, out setBonus);
            ComputeCombos();
            ArrangeTeaCups();
            ui.SetComfort(Mathf.RoundToInt(comfort).ToString());
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

        private void SetLampShade(Item it) { it.parts.shadeMat.SetVector("_EmissionColor", ThreeMat.Lin("#FFB65C") * (it.st.lampOn ? .6f : 0)); }

        // ---------------- squishy ----------------

        private void SetPet(int idx)
        {
            S.favIdx = idx;
            pet.SetFinish(C.finishes[idx]);
            pet.Scale = C.sizes[Rules.FavSizeIdx].s;
            ui.SetName(C.finishes[idx].name);
            UpdateSub();
        }

        private void UpdateSub() { ui.SetSub("Day " + S.age + " · " + C.sizes[Rules.FavSizeIdx].name + " · " + GameRules.StageName(Rules.LifeStage())); }

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
                var approach = new Vector2(px + dx / l * (rr + .18f), pz + dz / l * (rr + .18f));
                if (string.IsNullOrEmpty(it.a.face) && !act.inside) approach = NearestSide(it) ?? approach; // no front: the nearest clear side
                // On top of the piece itself (its own height for its style; reading on the beanbag sank into it), else the
                // activity's height.
                float top = act.inside ? 0 : HopTop(it);
                return new Spot { approach = approach, stand = new Vector2(px, pz), y = act.perch > 0 ? (top > 0 ? top : act.perch) : .06f };
            }
            return new Spot { stand = FreeSpotNear(new Vector2(sx, sz), it), y = 0, face = new Vector2(px, pz) };
        }

        /// <summary>
        /// The nearest clear floor to a point: out of every piece's footprint (except the one being used) and inside the
        /// steamer. Walk-to spots use it so the squishy never walks into furniture to reach them.
        /// </summary>
        private Vector2 FreeSpotNear(Vector2 p, Item ignore)
        {
            float pr = PetRadius();
            for (int pass = 0; pass < 8; pass++)
            {
                bool moved = false;
                foreach (var o in obstacles)
                {
                    if (o.it == ignore) continue;
                    float dx = p.x - o.x, dz = p.y - o.z, d = Dist(dx, dz), min = o.r + pr + .03f;
                    if (d >= min) continue;
                    if (d < 1e-4f) { dx = -o.x; dz = -o.z; d = Dist(dx, dz); if (d < 1e-4f) { dx = 1; dz = 0; d = 1; } } // dead centre: out towards the middle
                    p = new Vector2(o.x + dx / d * min, o.z + dz / d * min);
                    moved = true;
                }
                float l = p.magnitude;
                if (l > FLOOR_R - .12f) p *= (FLOOR_R - .12f) / l;
                if (!moved) break;
            }
            return p;
        }

        /// <summary>Just outside a piece's open front (where the squishy steps out of the shower).</summary>
        private Vector2 OutsideOf(Item it)
        {
            float d = it.a.r + PetRadius() + .12f;
            return new Vector2(it.tx + Mathf.Sin(it.ry) * d, it.tz + Mathf.Cos(it.ry) * d);
        }

        /// <summary>Plans the walk there. False when there's no way through (it won't walk through furniture to get there).</summary>
        private bool PlanPath(float tx, float tz, float ty, Vector2? approach, Item skip)
        {
            var pts = new List<PathPt>();
            float cx = ai.x, cz = ai.z;
            if (ai.y > .02f && ai.perch.HasValue) { pts.Add(new PathPt { x = ai.perch.Value.x, z = ai.perch.Value.y, y = 0, big = true }); cx = ai.perch.Value.x; cz = ai.perch.Value.y; }
            float gx = approach.HasValue ? approach.Value.x : tx, gz = approach.HasValue ? approach.Value.y : tz;
            pts.AddRange(NavWay(cx, cz, gx, gz, skip)); // round tall pieces, over low ones
            if (!navReached) { ai.path.Clear(); ai.seg = null; return false; }
            if (approach.HasValue) pts.Add(new PathPt { x = approach.Value.x, z = approach.Value.y, y = 0 });
            pts.Add(new PathPt { x = tx, z = tz, y = ty, big = ty > .09f });
            ai.path = pts;
            ai.seg = null;
            return true;
        }

        /// <summary>No way to the piece without going through furniture: it doesn't go (and says so when you asked).</summary>
        private void CantReach(Item it, bool user)
        {
            ai.act = null;
            ai.target = null;
            ai.spot = null;
            ai.mode = "idle";
            ai.idleT = user ? 2.5f : .5f;
            if (user)
            {
                var t = it != null ? C.Type(it.arch) : null;
                FloaterAt(it, "Can't reach", "bad");
                sfx.Bonk();
            }
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

        /// <summary>
        /// Sitting on a seat, facing the table. It hops on from whichever clear side of the seat is nearest to where it
        /// is (user feedback: it always went round the far side, the long way, even with the stool against a wall),
        /// never through the table; it hops off the same way.
        /// </summary>
        private Spot SeatSpot(Item seat, Item table)
        {
            float dx = seat.tx - table.tx, dz = seat.tz - table.tz, l = Dist(dx, dz);
            if (l == 0) l = 1;
            var away = new Vector2(dx / l, dz / l);
            var approach = NearestSide(seat, -away) ?? new Vector2(seat.tx, seat.tz) + away * .32f;
            var at = new Vector2(seat.tx, seat.tz);
            var st = C.Style(seat.style);
            bool low = seat.a.role == "lounge" || (st != null && st.low);
            return new Spot { approach = approach, stand = at, y = low ? .1f : .28f, face = new Vector2(table.tx, table.tz) };
        }

        /// <summary>
        /// The teapot lifts, turns its spout to a cup and pours (a thin stream), then sets down again; for tea for two
        /// it pours for each cup in turn. A wisp of steam from the spout now and then.
        /// </summary>
        private void PourTea(Item table, float t, float dt, bool forTwo)
        {
            var pot = table.parts.pot;
            if (pot == null) return;
            const float Cycle = 2.6f;
            int round = Mathf.FloorToInt(t / Cycle), pours = forTwo ? 2 : 1;
            float c = t - round * Cycle;
            if (round >= pours) { round = pours - 1; c = Cycle; } // all poured: the pot stays set down
            float tilt = c < .5f ? Mathf.SmoothStep(0, 1, c / .5f) : c < 1.4f ? 1 : c < 1.9f ? 1 - Mathf.SmoothStep(0, 1, (c - 1.4f) / .5f) : 0;
            // Which cup: the one in front of the squishy, or each in turn for two.
            Transform cup = null;
            if (table.parts.cups != null && table.parts.cups.Length > 0)
            {
                float best = 1e9f;
                foreach (var cp in table.parts.cups) { var w = ThreeWorld(cp); float d = Dist(w.x - ai.x, w.z - ai.z); if (d < best) { best = d; cup = cp; } }
                if (forTwo && round % 2 == 1) foreach (var cp in table.parts.cups) if (cp != cup) { cup = cp; break; }
            }
            if (cup == null || table.parts.spout == null) return;
            ItemModels.PourPose(table.parts, cup.localPosition, tilt);
            var to = ThreeWorld(cup);
            var sp = ThreeWorld(table.parts.spout);
            // A thin stream straight down into the tea (it stops at the tea's surface).
            if (tilt > .9f && Random.value < dt * 45)
                drops.Spawn(sp, new Vector3((to.x - sp.x) * 2, -.25f, (to.z - sp.z) * 2), .011f, .12f, 0, -6, floor: to.y + .046f, col: ThreeMat.Hex("#B8793F"));
        }

        /// <summary>Puts each tea table's cups in front of the seats round it (and sets the pot back down).</summary>
        private void ArrangeTeaCups()
        {
            foreach (var table in items)
            {
                if (table.a.role != "tea" || table.parts.cups == null) continue;
                var seats = items.Where(x => (x.a.role == "seat" || x.a.role == "lounge") && Dist(x.tx - table.tx, x.tz - table.tz) < 1.1f)
                    .OrderBy(x => Dist(x.tx - table.tx, x.tz - table.tz)).Take(table.parts.cups.Length).ToList();
                for (int k = 0; k < table.parts.cups.Length; k++)
                {
                    var cup = table.parts.cups[k];
                    float wx, wz;
                    if (k < seats.Count) { wx = seats[k].tx - table.tx; wz = seats[k].tz - table.tz; }
                    else { float a = table.ry + (k == 0 ? 2.4f : -.7f); wx = Mathf.Sin(a); wz = Mathf.Cos(a); }
                    float l = Mathf.Max(.001f, Dist(wx, wz));
                    wx /= l; wz /= l;
                    // Into the table's own turned space.
                    float cr = Mathf.Cos(table.ry), sr = Mathf.Sin(table.ry);
                    float lx = wx * cr - wz * sr, lz = wx * sr + wz * cr;
                    cup.localPosition = new Vector3(lx * .2f, table.parts.potY, lz * .2f);
                }
                if (!(ai.act != null && ai.act.it == table)) { Node.Rot(table.parts.pot, 0, 0, 0); table.parts.pot.localPosition = new Vector3(0, table.parts.potY, 0); }
            }
        }

        /// <summary>
        /// Where to hop on from: the clear side of a piece nearest to the squishy (inside the room, not in another
        /// piece), never from the "not" direction (the table's side). Null if nowhere is clear.
        /// </summary>
        private Vector2? NearestSide(Item it, Vector2? not = null)
        {
            Vector2 at = new Vector2(it.tx, it.tz), from = new Vector2(ai.x, ai.z);
            float reach = it.a.r + PetRadius() + .04f, lim = FLOOR_R - .15f, bestD = float.MaxValue;
            Vector2? best = null;
            for (int k = 0; k < 16; k++)
            {
                float ang = k * Mathf.PI / 8;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                if (not.HasValue && Vector2.Dot(dir, not.Value) > .45f) continue;
                var p = at + dir * reach;
                if (p.magnitude > lim) continue;
                bool blocked = false;
                foreach (var o in obstacles) if (o.it != it && Dist(p.x - o.x, p.y - o.z) < o.r + PetRadius() * .8f) { blocked = true; break; }
                if (blocked) continue;
                float d = Vector2.Distance(p, from);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        private bool SeatNear(Item t) { return items.Any(it => (it.a.role == "seat" || it.a.role == "lounge") && Dist(it.tx - t.tx, it.tz - t.tz) < 1.1f); }

        /// <summary>useItem: user=true means the player asked, so the need can fill completely.</summary>
        private void UseItem(Item it, bool user, RecipeData recipe = null, Activity chainFrom = null)
        {
            if (S.dead) return;
            if (S.tucked) { if (user) Floater("Paused · see Settings", "bad"); return; }
            string role = it.a.role;
            // Tucked in for the night: it stays in bed; tapping any piece (lights and plants too) is ignored. The moon wakes it.
            if (S.asleep && chainFrom == null && !sleepStarting) { if (user) FloaterAt(it, "Shh, " + Rules.Fav.name + " is asleep"); return; }
            if (role == "seat" && chainFrom == null)
            {
                var t = items.Find(x => x.a.role == "tea" && Dist(x.tx - it.tx, x.tz - it.tz) < 1.1f);
                if (t != null) { it = t; role = "tea"; }
            }
            // A finished combo takes precedence over the piece's own use (a chain carries on to its next piece).
            var cm = chainFrom != null ? chainFrom.combo : ComboAt(it);
            string comboAct = null;
            if (cm != null)
            {
                if (chainFrom == null && !IsLead(cm, it)) { it = LeadOf(cm) ?? it; role = it.a.role; }
                comboAct = chainFrom != null ? cm.combo.then : cm.combo.act;
                if (string.IsNullOrEmpty(role) || role == "decor") role = comboAct;
            }
            if (role == "decor") { FloaterAt(it, "+" + it.a.comfort + " comfort"); return; }
            if (string.IsNullOrEmpty(role)) { if (it.a.cat == "Wall") FloaterAt(it, "Room divider"); return; }
            var act = !string.IsNullOrEmpty(comboAct) ? C.Activity(comboAct) ?? C.Activity(role) : C.Activity(role);
            if (act == null) return;
            if (act.needSeat && !SeatNear(it)) { FloaterAt(it, "Needs a seat", "bad"); return; }
            if (role == "eat" && recipe == null) recipe = C.recipes[0];
            if (Condition() < .1f && !user) return;
            CleanupCook();
            if (chainFrom == null) EndToys(); // a new activity: the last toy's bits (bubbles, the pom-pom) are tidied away
            ai.act = new Activity { it = it, role = role, act = act, recipe = recipe, combo = cm, chained = chainFrom != null };
            ai.actT = 0;
            ai.kicks = 0;
            ai.self = !user;
            ai.target = it;
            if (System.Array.IndexOf(PlayRoles, role) >= 0 || role == "bounce") pet.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Grin, 1.4f);
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
                var s = SeatSpot(seat, it);
                ai.spot = s;
                ai.mode = "walk";
                if (!PlanPath(s.stand.x, s.stand.y, s.y, s.approach, seat)) { CantReach(it, user); return; }
            }
            else if (it.a.cat == "Floor")
            {
                // On the rug, but never inside what stands on it (a cushion in the middle); shaking dry after the shower
                // happens just outside the shower, wherever the rug is (it ran across the room through everything).
                var at = chainFrom != null && chainFrom.it != null && comboAct == "shake" ? OutsideOf(chainFrom.it) : new Vector2(it.tx, it.tz);
                var s = new Spot { stand = FreeSpotNear(at, it), y = 0 };
                ai.spot = s;
                ai.mode = "walk";
                if (!PlanPath(s.stand.x, s.stand.y, 0, null, it)) { CantReach(it, user); return; }
            }
            else if (role == "slide")
            {
                // Start at the foot of the ladder, behind the slide.
                float dx = Mathf.Sin(it.ry), dz = Mathf.Cos(it.ry);
                var s = new Spot { stand = FreeSpotNear(new Vector2(it.tx - dx * .34f, it.tz - dz * .34f), it), y = 0, face = new Vector2(it.tx, it.tz) };
                ai.spot = s;
                ai.mode = "walk";
                if (!PlanPath(s.stand.x, s.stand.y, 0, null, it)) { CantReach(it, user); return; }
            }
            else
            {
                var s = SpotOf(it, act);
                if (role == "dressup") s.face = null; // it faces you, not the wardrobe
                ai.spot = s;
                ai.mode = "walk";
                if (!PlanPath(s.stand.x, s.stand.y, s.y, s.approach, it)) { CantReach(it, user); return; }
            }
            // Busy by itself with something: say so, and that a tap now earns a tip (the passive-income interaction).
            if (user) ui.ShowBubble(string.IsNullOrEmpty(act.need) ? "idle" : act.need, recipe != null && role == "eat" ? recipe.name : act.label, false);
            else ui.ShowBubble(string.IsNullOrEmpty(act.need) ? "idle" : act.need, act.label, false);
        }

        /// <summary>The squishy looks after itself, but only up to about half.</summary>
        private void SelfCare(int need)
        {
            string[][] map = { new[] { "snack", "eat" }, PlayRoles, new[] { "lounge", "bed" }, new[] { "wash" } };
            foreach (var role in map[need])
            {
                if (role == "snack" && !S.snacks.Any(n => n > 0)) continue;
                var it = NearestRole(role);
                if (it != null) { UseItem(it, false); if (ai.act != null) return; }
            }
            string label = need == Needs.Clean ? "Grooming" : need == Needs.Hunger ? "Nibbling crumbs" : need == Needs.Rest ? "Dozing" : "Wiggling";
            ai.act = new Activity { role = "makeDo", act = new ActivityData { label = label, need = Needs.Names[need], dur = 4, rate = .06f } };
            ai.mode = "act";
            ai.actT = 0;
            ai.self = true;
            ai.spot = null;
            ai.target = null;
            ui.ShowBubble(Needs.Names[need], label, false);
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
            if (!PlanPath(tx, tz, 0, null, null)) { ai.mode = "idle"; ai.idleT = 1; }
            ui.HideBubble();
        }

        private void Autonomous()
        {
            if (S.asleep) return; // asleep for the night: it stays put (StepNight keeps it in bed)
            float cond = Condition();
            if (cond < .12f) { ai.idleT = 4; ui.ShowBubble(Needs.Names[Rules.LowestNeed()], "Help…", true); return; }
            int low = Rules.LowestNeed();
            if (S.needs[low] < .35f && Random.value < .8f) { SelfCare(low); return; }
            var plants = items.FindAll(i => i.arch == "plant" && i.st.wilt > .5f);
            if (plants.Count > 0 && Random.value < .3f) { UseItem(plants[0], false); return; }
            if (Random.value < C.rules.selfPlayChance)
            {
                // Content and bored: it wanders off to play or lounge on its own (a chance to catch it and earn a tip).
                var fun = items.FindAll(i => (System.Array.IndexOf(PlayRoles, i.a.role) >= 0 || i.a.role == "lounge" || i.a.role == "seat" || i.a.role == "bounce" || StartsCombo(i))
                    );
                // Finished combos are favourites: half the time it picks one of those.
                var fav = fun.FindAll(StartsCombo);
                if (fav.Count > 0 && Random.value < .5f) fun = fav;
                if (fun.Count > 0) { UseItem(fun[Random.Range(0, fun.Count)], false); if (ai.act != null) return; }
            }
            if (Random.value < .7f) Wander(); else ai.idleT = Rnd(1.5f, 3);
        }

        /// <summary>Seconds left of the energised glow after it played with something by itself (runtime only).</summary>
        private float energyT;
        private Transform tipCoin, tipSpin;
        private float tipCoinS;

        /// <summary>
        /// While tips are waiting, a little gold coin spins over its head (a bit bigger the more it holds). It stays
        /// until tapped: nothing is lost by not tapping straight away.
        /// </summary>
        private void StepTipCoin(float dt)
        {
            bool on = S.tipPile > 0 && !visiting && mode == "home" && !S.dead;
            if (tipCoin == null)
            {
                if (!on) return;
                tipCoin = Node.Group(room, "tipCoin");
                tipSpin = Node.Group(tipCoin, "spin");
                Node.Mesh(tipSpin, ThreeGeo.Cyl(.04f, .04f, .026f, 28), ThreeMat.M("#E8B83A"), 0, 0, 0, shadow: false).RotX(Mathf.PI / 2);
                Node.Mesh(tipSpin, ThreeGeo.Torus(.039f, .01f, 6, 28), ThreeMat.M("#C99426"), 0, 0, 0, shadow: false);
                Node.Mesh(tipSpin, ThreeGeo.Cyl(.027f, .027f, .03f, 24), ThreeMat.M("#F6D46A"), 0, 0, 0, shadow: false).RotX(Mathf.PI / 2);
                Node.Mesh(tipSpin, ThreeGeo.RBox(.01f, .03f, .034f, .004f), ThreeMat.M("#C99426"), 0, 0, 0, shadow: false); // the stamp in the middle
                Node.SetLayer(tipCoin, HomeLayer);
            }
            tipCoinS = Mathf.MoveTowards(tipCoinS, on ? 1 : 0, dt * 5);
            tipCoin.gameObject.SetActive(tipCoinS > .001f);
            if (tipCoinS <= .001f) return;
            var p = PetWorld();
            float bob = .015f * Mathf.Sin(time * 3.2f);
            tipCoin.localPosition = new Vector3(p.x, p.y + pet.Scale * pet.StageScale * 1.3f + .19f + bob, p.z);
            tipCoin.localScale = Vector3.one * (tipCoinS * (1.25f - .25f * tipCoinS) * (1 + Mathf.Min(.5f, S.tipPile / 40f)));
            tipSpin.RotY(time * 3.5f);
        }

        /// <summary>Collects every tip waiting in the coin (a squish, or a tap on what it played with).</summary>
        private void TipCoins()
        {
            if (!visiting) TaskEvent("tip");
            energyT = 0;
            energisedBy = null;
            int coins = S.tipPile;
            S.tipPile = 0;
            if (coins <= 0) return;
            Rules.AddCoins(coins);
            WriteSave();
            sfx.Coin();
            Buzz(15);
            Floater("+" + coins + " coins");
            pet.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Grin, 1.2f);
        }

        private Item energisedBy; // the thing it just played with: tapping it also pays the bonus

        private void Energise(Item by)
        {
            energisedBy = by;
            energyT = C.rules.energySeconds;
            // A tip goes into the coin; tips keep piling up for tipStackSeconds after the first, then it just waits.
            long now = System.DateTime.UtcNow.Ticks;
            bool first = S.tipPile == 0;
            if (first) S.tipStart = now;
            if (now - S.tipStart <= System.TimeSpan.FromSeconds(C.rules.tipStackSeconds).Ticks) S.tipPile += Random.Range(C.rules.tipMin, C.rules.tipMax + 1);
            if (!first) return;
            ui.ShowBubble("coin", "Feeling bouncy · squish me!", false);
            Later(3f, () => { if (ai.mode == "idle" && energyT > 0) ui.HideBubble(); });
        }

        private void FinishActivity()
        {
            CleanupCook();
            EndToys();
            var a = ai.act;
            bool energise = !visiting && a != null && ai.self && a.it != null;
            if (a != null && !string.IsNullOrEmpty(a.act.need))
                Floater((ai.self ? "" : "+") + char.ToUpper(a.act.need[0]) + a.act.need.Substring(1));
            ai.act = null;
            ai.target = null;
            ai.mode = "idle";
            ai.idleT = Rnd(1.5f, 3.5f);
            ui.HideBubble();
            if (energise) Energise(a.it);
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

        /// <summary>Sitting on a piece that's squashing (a beanbag poofing as it lands): it sinks and rises with it.</summary>
        private float PerchSink()
        {
            var A = ai.act;
            if (ai.mode != "act" || A == null || A.it == null || A.it.bx == 0 || !(A.act.perch > 0 || A.night)) return 1;
            return 1 + A.it.bx * .3f;
        }

        private void StartAct()
        {
            var A = ai.act;
            if (A == null) return;
            // Landing on a piece: soft ones (beanbag, cushion) poof down and back; others give a little bounce.
            if (A.it != null && (A.act.perch > 0 || A.night) && ai.y > .02f) A.it.bv = A.it.a.role == "lounge" ? -9 : -5;
            if (!ai.self && !visiting && !A.night) TaskEvent("act_" + A.role); // (bedtime isn't a nap mission) missions: "go down the slide", "bath time"...
            if (A.role == "lamp" && A.it != null)
            {
                A.it.st.lampOn = !A.it.st.lampOn;
                SetLampShade(A.it);
                PlaceLamps();
                sfx.Tap();
                FloaterAt(A.it, A.it.st.lampOn ? "Lights on" : "Lights off");
            }
            if (A.role == "bath") sfx.Bath();
            if (A.role == "plant" && A.it != null) { A.it.st.wilt = 0; ComputeComfort(); }
            if (A.role == "shower" && A.it != null) A.it.parts.openT = 0;
            StartToy(A);
            if (A.recipe != null && A.role == "eat")
            {
                var rc = A.recipe;
                var chef = ChefAt(A.it);
                if (chef != null) A.cookMul = chef.cookMul;
                if (rc.ing.Length > 0 && !ai.self)
                {
                    foreach (var i in rc.ing) S.pantry[i] = Mathf.Max(0, S.pantry[i] - 1);
                    if (chef != null && Random.value < chef.bonusIng)
                    {
                        int saved = rc.ing[Random.Range(0, rc.ing.Length)];
                        S.pantry[saved]++;
                        var stv = A.it;
                        Later(1.2f, () => FloaterAt(stv, C.pantry[saved].name + " saved"));
                    }
                    foreach (var i in rc.tools)
                    {
                        if (chef != null && Random.value < chef.wearSkip) continue;
                        S.toolDur[i] = Mathf.Max(0, S.toolDur[i] - 1);
                        if (S.toolDur[i] == 0) { int ti = i; var st = A.it; Later(.9f, () => FloaterAt(st, C.tools[ti].name + " broke!", "bad")); }
                    }
                }
                Floater("Cooking " + rc.name);
                sfx.Cook();
                var pan = A.it.parts.pan;
                cookTi = rc.tools.Length > 0 ? rc.tools[0] : -1;
                cookTool = cookTi >= 0 ? KitchenModels.Tool(C, S, cookTi, null, A.it.g) : null;
                if (cookTool != null)
                {
                    cookTool.localScale = Vector3.one * .55f;
                    cookTool.localPosition = pan.localPosition + new Vector3(0, .04f, 0);
                    Node.SetLayer(cookTool, HomeLayer);
                    pan.gameObject.SetActive(C.tools[cookTi].shape != "wok" && C.tools[cookTi].shape != "steamer");
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
                else { Floater("No snacks", "bad"); ai.actT = A.act.dur; }
            }
            StartCombo(A);
        }

        private void CleanupCook()
        {
            ClearComboProps();
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
                if (A.role == "plant") { FloaterAt(A.it, "Watered"); if (visiting) VisitAct("water"); else TaskEvent("water"); }
                if ((A.role == "eat" || A.role == "dine") && A.recipe != null && A.recipe.ing.Length > 0)
                {
                    int i = System.Array.IndexOf(C.recipes, A.recipe), l0 = Rules.RecipeLvl(i);
                    S.recipeXP[i]++;
                    int l1 = Rules.RecipeLvl(i);
                    TaskEvent("cook");
                    if (l1 > l0) Later(.6f, () => CelebrateRecipe(i, l1));
                }
                if (A.role == "bed" && !items.Any(x => x.arch == "lamp" && x.st.lampOn)) TaskEvent("nap");
                if (A.role == "tea") TaskEvent("tea");
            }
            EndCombo(A);
            if (ChainCombo(A)) return;
            FinishActivity();
        }

        // ---------------- holiday pause (Settings) ----------------

        /// <summary>Holiday pause, from Settings only: every need pauses (Rest still fills) while you're away.</summary>
        public void Tuck()
        {
            if (S.tucked || !Rules.CanTuck()) return;
            CleanupCook();
            ai.act = null;
            ai.target = null;
            ai.path.Clear();
            ai.seg = null;
            ai.mode = "idle";
            ui.HideBubble();
            S.tucked = true;
            Floater("Holiday pause on");
            ui.SetHint("Holiday pause");
            sfx.Chime();
            WriteSave();
        }

        public void Wake()
        {
            if (!S.tucked) return;
            S.tucked = false;
            ai.idleT = 1.5f;
            pet.V += 3;
            Floater("Welcome back!");
            ui.SetHint("");
            sfx.Chime();
            WriteSave();
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
            var rec = Rules.EndLife(false, why);
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
                    if (i % 3 == 0) Glints(new Vector3(ai.x + Mathf.Cos(a) * .2f, Y0 + .15f, ai.z + Mathf.Sin(a) * .2f), "#E8E2DA", 1);
                }
                ShowLifeCard(rec);
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
            if (S.lives.Count > 0) ShowLifeCard(S.lives[S.lives.Count - 1]);
        }

        public void NextSquishy()
        {
            var opts = S.squishOwned.Select(q => q.i).Where(i => i != S.favIdx).ToList();
            BeginLife(opts.Count > 0 ? opts[0] : 0, false);
        }

        /// <summary>After a full life: a baby of the same type (the collection keeps its copies and size).</summary>
        public void NewBaby() { BeginLife(S.favIdx, true); }

        private void BeginLife(int next, bool keepCopy)
        {
            if (Rules.TrialOver()) { ui.HideMemo(); ShowPaywall(); return; }
            ui.HideMemo();
            ui.ShowHud(true);
            Rules.StartLife(next, keepCopy);
            SetPet(next);
            shownStage = (GameRules.Life)(-1);
            ApplyLook();
            pet.Grey = 0;
            pet.Pivot.gameObject.SetActive(true);
            DrawNeeds();
            var t = items.FindLast(i => i.arch == "tomb");
            float x = 0, z = .3f;
            if (t != null) { x = t.tx + .5f; z = t.tz; }
            ai.x = x; ai.z = z; ai.y = 0;
            FreePet();
            ai.mode = "idle";
            ai.idleT = 2;
            ai.act = null;
            Floater(keepCopy ? "A new baby " + C.finishes[next].name + "!" : "Welcome, " + C.finishes[next].name + "!");
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
            // A few soft white glints where it was kicked (it was a grey steam puff from the old smoke effects).
            if (fxPool != null)
                for (int i = 0; i < 4; i++)
                {
                    float ga = Rnd(0, Mathf.PI * 2);
                    fxPool.Spawn(new Vector3(bl.tx, Y0 + .1f, bl.tz), new Vector3(Mathf.Cos(ga) * .25f, Rnd(.25f, .45f), Mathf.Sin(ga) * .25f), Rnd(.04f, .06f), Rnd(.5f, .8f), 2f, -.2f, w: new Vector3(Rnd(0, 6), 0, Rnd(-2, 2)), col: Color.white);
                }
        }

        private void StepHome(float dt)
        {
            pet.Chewing = false; // StepAct turns it on while eating
            if (S.dead || _dying) { pet.Update(dt, .35f, true, 1); ApplyPetTransform(0, true); AnimateFurniture(dt); return; }
            if (S.intro > 0) { AnimateFurniture(dt); return; } // the first run: nothing lives here yet (no needs, no wandering)
            if (S.tucked)
            {
                // Tucked in: fast asleep where it lies, gently breathing; nothing drains.
                pet.Update(dt, .18f + .04f * Mathf.Sin(time * 1.6f), true, 0);
                zTimer -= dt;
                if (zTimer <= 0 && mode == "home") { zTimer = 1.6f; Floater("z", "z"); }
                ui.SetCond("Paused", "#8C7BB0");
                ai.actT += dt;
                ApplyPetTransform(0, true);
                AnimateFurniture(dt);
                return;
            }
            float sdt = dt * timeScale;
            Rules.DrainScale = Rules.DrainScaleAt(System.DateTime.UtcNow.Ticks);
            int age0 = S.age;
            bool died = !visiting && Rules.StepCare(sdt, comfort); // a friend's squishy doesn't drain while you visit
            if (S.age != age0) UpdateSub();
            needT += dt;
            if (needT > .5f) { needT = 0; DrawNeeds(); if (Random.value < .2f) ComputeComfort(); }
            foreach (var it in items)
                if (it.arch == "plant")
                {
                    if (!visiting) it.st.wilt = Mathf.Min(1, it.st.wilt + sdt * C.rules.plantWiltRate);
                    it.parts.topWiltX = it.st.wilt * .35f;
                    ApplyPlantTop(it);
                }
            if (died) { Die(); return; }
            float cond = Condition();
            var st = GameRules.Stage(cond);
            if (S.asleep) ui.SetCond("Asleep", "#8C7BB0"); // asleep for the night
            else ui.SetCond(st[0], st[1]);
            float droop = Sstep(.5f, .08f, cond);
            pet.Grey = Sstep(.25f, .03f, cond) * .85f;
            if (cond > .5f && mode == "home")
            {
                TaskEvent("happy", dt);
                if (S.needs[0] > .5f && S.needs[1] > .5f && S.needs[2] > .5f && S.needs[3] > .5f) TaskEvent("allhalf", dt);
                Rules.StepHappy(dt, comfort);
            }
            if (homeWall.Grow.HasValue)
            {
                homeWall.Grow = Mathf.Min(1, homeWall.Grow.Value + dt * 1.2f);
                float s0 = homeWall.Group.localScale.x;
                homeWall.Group.localScale = Vector3.one * (s0 + (1 - s0) * Mathf.Min(1, dt * 5));
                if (homeWall.Grow >= 1) { homeWall.Group.localScale = Vector3.one; homeWall.Grow = null; }
            }
            float slowMove = 1 - droop * .55f, extra = 0, lift = 0;
            bool sleeping = ai.mode == "act" && ai.act != null && (ai.act.act.sleep || ai.act.act.closed);
            float hopH = pet.Scale * .9f;
            if (mode == "edit" || pet.Held)
            {
                // Arranging: lying on the bed (asleep or napping) or sitting on something, it rides along when that
                // piece is moved or lifted, and keeps doing what it was doing.
                if (mode == "edit" && rideItem != null && items.Contains(rideItem))
                {
                    float c = Mathf.Cos(rideItem.ry), s = Mathf.Sin(rideItem.ry);
                    ai.x = rideItem.tx + rideOff.x * c + rideOff.y * s;
                    ai.z = rideItem.tz - rideOff.x * s + rideOff.y * c;
                    lift = rideItem.g.localPosition.y - Y0;
                    if (sleeping) extra = .18f + .04f * Mathf.Sin(time * 1.6f);
                }
            }
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
            else if (ai.mode == "walk" || ai.mode == "chase") StepWalk(dt, slowMove, droop, hopH, ref lift, ref extra);
            else if (ai.mode == "fling") StepFling(dt, ref lift, ref extra);
            else if (ai.mode == "act") StepAct(dt, ref extra, ref lift);
            StepBalls(dt);
            StepBallHome(dt);
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
            if (energyT > 0)
            {
                // Energised: a slow, visible undulation that fades away over the last twenty seconds.
                energyT = sleeping || S.tucked ? 0 : energyT - dt;
                extra += .08f * Mathf.Clamp01(energyT / 20f) * Mathf.Sin(time * 2.4f);
            }
            StepTipCoin(dt);
            StepStrayBubbles(dt);
            // Sitting or lying on something: the squishy drapes over its edges (a big one envelops a small stool).
            var supRole = ai.mode == "act" && ai.act != null && ai.act.it != null ? ai.act.role : null;
            bool sup = (supRole == "seat" || supRole == "lounge" || supRole == "bed") && ai.actT > .2f;
            pet.SetSupport(sup ? ai.act.it.a.r * .75f / Mathf.Max(.01f, pet.Scale * pet.StageScale) : 0, sup);
            pet.Update(dt, extra, sleeping, droop);
            ApplyPetTransform(lift, sleeping);
            AnimateFurniture(dt);
        }

        private void ApplyPetTransform(float lift, bool sleeping)
        {
            pet.Pivot.localPosition = new Vector3(ai.x, Y0 + .02f + ai.y * PerchSink() + lift, ai.z);
            petYawZ = sleeping ? Mathf.Sin(ai.actT * .8f) * .05f : petYawZ * .9f;
            Node.Rot(pet.Yaw, 0, petYawY, petYawZ);
        }

        private void StepWalk(float dt, float slowMove, float droop, float hopH, ref float lift, ref float extra)
        {
            var ball = ai.mode == "chase" ? ai.act.it : null;
            if (ball != null && ai.path.Count == 0 && ai.seg == null) ai.path = new List<PathPt> { new PathPt { x = ball.tx, z = ball.tz, y = 0, chase = true } };
            if (ai.seg == null && ai.path.Count > 0)
            {
                var p = ai.path[0];
                ai.path.RemoveAt(0);
                ai.seg = new Seg { fx = ai.x, fz = ai.z, fy = ai.y, tx = p.x, tz = p.z, ty = p.y, big = p.big, chase = p.chase, on = p.on, d = 0, len = Mathf.Max(.001f, Dist(p.x - ai.x, p.z - ai.z)) };
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
            if (s.chase) PushOutOfFurniture(ball); // chasing the ball: round the furniture, not through it
            else if (!s.big && s.fy <= .02f && s.ty <= .02f) PushOutOfTall(ai.target); // on the floor: never through a tall piece
            YawTo(s.tx, s.tz, dt, 10);
            if (s.big) lift = .4f * Mathf.Sin(Mathf.PI * u);
            else
            {
                float prev = ai.hopPh;
                ai.hopPh += step / (.16f + pet.Scale * .6f);
                lift = hopH * (1 - droop * .6f) * Mathf.Abs(Mathf.Sin(Mathf.PI * ai.hopPh));
                // Stretch in the air, squash on landing (the prototype squashed on take-off, so it looked flattened mid-hop).
                extra = -.12f * Mathf.Sin(Mathf.PI * (ai.hopPh - Mathf.Floor(ai.hopPh)));
                if (Mathf.Floor(ai.hopPh) > Mathf.Floor(prev)) { pet.V += 1.2f; if (Random.value < .25f) sfx.Hop(); }
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
                    if (ai.kicks >= (ai.self ? 2 : 3)) { var A0 = ai.act; EndCombo(A0); if (!ChainCombo(A0)) FinishActivity(); }
                }
            }
            else if (u >= 1)
            {
                ai.seg = null;
                if (s.on != null) { s.on.bv = -6; pet.V += 2.5f; sfx.Hop(); } // landed on a table or stool: it bounces like a tap
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
            float COOK = 2.4f * A.cookMul;
            if (A.role == "eat" && t > COOK && A.combo == null && MoveDinnerToTable(A)) return; // Dinner table: eaten sitting at the table
            bool eating = (A.role == "eat" && t > COOK) || A.role == "dine";
            float eatDur = A.role == "dine" ? act.dur : act.dur - COOK;
            if (eating || (A.role == "snack" && t > .4f)) pet.Chewing = true;
            if (A.recipe != null)
            {
                int ri = System.Array.IndexOf(C.recipes, A.recipe);
                float mul = 1 + .1f * (Rules.RecipeLvl(ri) - 1);
                rate = eating ? A.recipe.hunger * mul / eatDur : 0;
                capN = ai.self ? .5f : (A.recipe.cap > 0 ? A.recipe.cap : 1);
                if (eating && !string.IsNullOrEmpty(A.recipe.bonusNeed) && !ai.self)
                {
                    int bk = Needs.Index(A.recipe.bonusNeed);
                    S.needs[bk] = Mathf.Min(1, S.needs[bk] + A.recipe.bonus * mul / eatDur * dt);
                }
            }
            void Fill(string k, float r)
            {
                int i = Needs.Index(k);
                if (i >= 0 && S.needs[i] < capN) S.needs[i] = Mathf.Min(capN, S.needs[i] + r * dt);
            }
            if (!string.IsNullOrEmpty(act.need)) Fill(act.need, rate);
            if (!string.IsNullOrEmpty(act.also)) Fill(act.also, rate * (act.alsoMul > 0 ? act.alsoMul : .5f));
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
                        string ts = C.tools[cookTi].shape;
                        if (ts == "spatula") rx = -.3f + Mathf.Sin(t * 10) * .5f;
                        else if (ts == "cleaver") { tl.localPosition += new Vector3(0, Mathf.Abs(Mathf.Sin(t * 12)) * .05f, 0); rx = Mathf.Sin(t * 12) * .3f; }
                        else if (ts == "cutlery") ryy = t * 6;
                        else if (ts == "wok") { tl.localPosition += new Vector3(0, Mathf.Abs(Mathf.Sin(t * 6)) * .05f, 0); rz = Mathf.Sin(t * 6) * .3f; }
                        else if (ts == "rollingpin") { tl.localPosition += new Vector3(Mathf.Sin(t * 5) * .06f, 0, 0); ryy = Mathf.PI / 2; }
                        else if (ts == "ladle") ryy = t * 5;
                        else if (ts == "chopsticks") rz = Mathf.Sin(t * 14) * .2f;
                        Node.Rot(tl, rx, ryy, rz);
                    }
                    else pan.RotZ(Mathf.Sin(t * 20) * .06f);
                    if (Random.value < dt * (cookTi >= 0 && C.tools[cookTi].shape == "steamer" ? 14 : 6))
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
            else if (A.role == "dine")
            {
                extra = .22f * Mathf.Max(0, Mathf.Sin(t * 12));
                if (cookDish != null) cookDish.localScale = Vector3.one * (.55f * (.35f + .65f * Mathf.Max(0, 1 - t / act.dur)));
            }
            else if (A.role == "snack") extra = .18f * Mathf.Max(0, Mathf.Sin(t * 14));
            else if (A.role == "tea" && it != null)
            {
                extra = .06f * Mathf.Sin(t * 3);
                PourTea(it, t, dt, A.act.role == "teaparty");
            }
            else if (act.sleep)
            {
                extra = .18f + .04f * Mathf.Sin(t * 1.6f);
                zTimer -= dt;
                if (zTimer <= 0) { zTimer = 1.3f; Floater("z", "z"); }
            }
            else if (A.role == "bath" && it != null)
            {
                // Floating in the water with its top half above the rim (it used to sink out of sight), bobbing gently.
                float h = 1.46f * pet.Scale * pet.StageScale;
                lift = .19f - ai.y - .2f * h + .012f * Mathf.Sin(t * 2.4f); // .19 = the water line; only the bottom fifth is under
                extra = .04f * Mathf.Sin(t * 6);
                if (Random.value < dt * 7) { var wp = ThreeWorld(it.parts.water); bathBubbles.Spawn(new Vector3(wp.x + Rnd(-.2f, .2f), wp.y + .03f, wp.z + Rnd(-.13f, .13f)), new Vector3(Rnd(-.03f, .03f), Rnd(.08f, .22f), Rnd(-.03f, .03f)), Rnd(.014f, .032f), Rnd(1.2f, 2.2f), 1.5f, .03f); }
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
            StepToy(A, dt, t, ref lift, ref extra);
            StepCombo(A, dt, t, ref lift, ref extra);
            if (t >= act.dur) EndAct();
        }

        private static void ApplyPlantTop(Item it) { if (it.parts.top != null) Node.Rot(it.parts.top, it.parts.topWiltX, 0, it.parts.topSwayZ); }

        /// <summary>A part's position in three space (room coordinates).</summary>
        private Vector3 ThreeWorld(Transform t) { return world.InverseTransformPoint(t.position); }

        /// <summary>After play, a ball left sitting away from where it was placed pops back home (it used to get stuck in corners).</summary>
        private void StepBallHome(float dt)
        {
            foreach (var bl in items)
            {
                if (bl.arch != "ball") continue;
                if (!bl.st.homeSet) { bl.st.hx = bl.tx; bl.st.hz = bl.tz; bl.st.homeSet = true; }
                bool busy = mode == "edit" || bl.vx != 0 || bl.vz != 0 || (ai.act != null && ai.act.it == bl) || (drag != null && drag.ball == bl)
                    || Dist(bl.tx - bl.st.hx, bl.tz - bl.st.hz) < .12f || Dist(bl.st.hx - ai.x, bl.st.hz - ai.z) < PetRadius() + bl.a.r + .05f;
                bl.restT = busy ? 0 : bl.restT + dt;
                if (bl.restT < 3) continue;
                bl.restT = 0;
                Glints(new Vector3(bl.tx, Y0 + .1f, bl.tz), "#FFF3D6", 2);
                bl.tx = bl.st.hx;
                bl.tz = bl.st.hz;
                bl.g.localPosition = new Vector3(bl.tx, bl.g.localPosition.y, bl.tz);
                bl.grow = 0;
                sfx.Snap();
            }
        }

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
                        if (sp > .5f) { sfx.Bonk(); Glints(new Vector3(nx * lim, Y0 + .1f, nz * lim), "#FFF3D6", 1); }
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
            if (comboRings.Count > 0) StepComboRings();
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
