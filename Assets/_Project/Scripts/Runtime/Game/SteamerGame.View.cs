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
        private static readonly (float deg, string name)[] Tilts = { (84, "Top-down"), (62, "Angled"), (38, "Low") };
        private Item sel, moving;
        private readonly List<Snapshot> undo = new List<Snapshot>();
        private (float a0, float r0, int step)? twist;
        private readonly Dictionary<int, Pointer> ptrs = new Dictionary<int, Pointer>();
        private Drag drag;
        private float pinch0, zoom0;

        // ---------------- camera ----------------

        private float FitDist(float half)
        {
            float vf = cam.fieldOfView * Mathf.Deg2Rad, hf = 2 * Mathf.Atan(Mathf.Tan(vf / 2) * cam.aspect);
            return Mathf.Max(half / Mathf.Tan(hf / 2), half * 1.15f / Mathf.Tan(vf / 2));
        }

        private void PlaceCamera(Vector3 pos, Vector3 target)
        {
            cam.transform.position = Space3.U(pos);
            cam.transform.rotation = Quaternion.LookRotation(Space3.U(target) - Space3.U(pos), Vector3.up);
        }

        private void HomeCamera(float dt)
        {
            var c = camS;
            if (drag == null || !drag.moved || drag.item != null || drag.ball != null || drag.scrub) { c.yawV *= Mathf.Pow(.02f, dt); c.yaw += c.yawV; }
            if (ui.IntroOn) c.yaw += dt * .14f; // the title screen slowly turns the room
            c.zoom += (c.zoomT - c.zoom) * Mathf.Min(1, dt * 4);
            c.edit += ((mode == "edit" ? 1 : 0) - c.edit) * Mathf.Min(1, dt * 4);
            c.ez += (c.ezT - c.ez) * Mathf.Min(1, dt * 6);
            c.tilt += (c.tiltT - c.tilt) * Mathf.Min(1, dt * 6);
            if (c.edge.HasValue && moving != null) PanBy(-c.edge.Value.x * dt * 260, -c.edge.Value.y * dt * 260);
            if (c.panGoal.HasValue)
            {
                float k2 = Mathf.Min(1, dt * 4);
                c.panT.x += (c.panGoal.Value.x - c.panT.x) * k2;
                c.panT.z += (c.panGoal.Value.y - c.panT.z) * k2;
                if (Dist(c.panGoal.Value.x - c.panT.x, c.panGoal.Value.y - c.panT.z) < .01f) c.panGoal = null;
            }
            // zoom: -1 close-up on the squishy, 0 follow (the prototype's default), 1 whole room.
            float z = Mathf.Max(0, c.zoom), close = Mathf.Max(0, -c.zoom), e = c.edit;
            float hx = ai.x * (1 - z), hz = ai.z * (1 - z), hy = (Y0 + pet.Scale * (1 - .35f * Mathf.Min(1, close)) + ai.y) * (1 - z) + .3f * z;
            float tx = hx + (c.panT.x - hx) * e, tz = hz + (c.panT.z - hz) * e, ty = hy + (.25f - hy) * e;
            float kk = Mathf.Min(1, dt * (e > .5f ? 12 : 3));
            c.target += new Vector3((tx - c.target.x) * kk, (ty - c.target.y) * kk, (tz - c.target.z) * kk);
            float near = .45f + pet.Scale * 2.4f;
            float dH = FitDist((near + (HR * 1.12f - near) * z) * (1 - .55f * Mathf.Min(1, close) - .27f * Mathf.Max(0, close - 1))) /* past -1: closer still, for squishing */, dE = FitDist(.7f + (HR * 1.12f - .7f) * c.ez), elH = 42 + 14 * z - 14 * close + c.htilt;
            float d = dH + (dE - dH) * e, el = (elH + (c.tilt - elH) * e) * Mathf.Deg2Rad;
            var pos = new Vector3(c.target.x + Mathf.Sin(c.yaw) * Mathf.Cos(el) * d, c.target.y + Mathf.Sin(el) * d, c.target.z + Mathf.Cos(c.yaw) * Mathf.Cos(el) * d);
            PlaceCamera(pos, c.target);
            homeWall.Cutaway(Mathf.Sin(c.yaw), Mathf.Cos(c.yaw), dt, (e > .5f && c.tilt > 74) ? -.1f : .42f);
        }

        private void PanBy(float dx, float dy)
        {
            var c = camS;
            float half = .7f + (HR * 1.12f - .7f) * c.ez, k = half * 2 / ui.Width, cs = Mathf.Cos(c.yaw), sn = Mathf.Sin(c.yaw), se = Mathf.Max(.5f, Mathf.Sin(c.tilt * Mathf.Deg2Rad));
            c.panT.x += (-cs * dx - sn * dy / se) * k;
            c.panT.z += (sn * dx - cs * dy / se) * k;
            float l = Dist(c.panT.x, c.panT.z), mx = HR * .85f;
            if (l > mx) { c.panT.x *= mx / l; c.panT.z *= mx / l; }
            c.panGoal = null;
        }

        public void ZoomIn() { camS.ezT = Mathf.Clamp01(camS.ezT - .25f); if (sel != null) camS.panGoal = new Vector2(sel.tx, sel.tz); sfx.Tap(); }
        public void ZoomOut() { camS.ezT = Mathf.Clamp01(camS.ezT + .25f); if (camS.ezT >= 1) camS.panGoal = Vector2.zero; sfx.Tap(); }

        public void CycleTilt()
        {
            int i = System.Array.FindIndex(Tilts, t => Mathf.Abs(t.deg - camS.tiltT) < 6);
            camS.tiltT = Tilts[(i + 1) % Tilts.Length].deg;
            SetTiltLbl();
            sfx.Tap();
        }

        private void SetTiltLbl()
        {
            var t = Tilts.Aggregate((a, b) => Mathf.Abs(b.deg - camS.tiltT) < Mathf.Abs(a.deg - camS.tiltT) ? b : a);
            ui.SetTiltLabel(t.name);
        }

        // ---------------- picking ----------------

        private Vector2 ToScreen(Vector2 panel) { return new Vector2(panel.x / ui.Width * Screen.width, (1 - panel.y / ui.Height) * Screen.height); }

        private Ray PickRay(Vector2 panel) { return cam.ScreenPointToRay(ToScreen(panel)); }

        private Item ItemHit(Vector2 panel)
        {
            var ray = PickRay(panel);
            Item best = null;
            float bd = float.MaxValue;
            foreach (var it in items)
            {
                float d = RayPick.Hit(ray, it.g);
                if (d >= 0 && d < bd) { bd = d; best = it; }
            }
            if (best != null) return best;
            // Small toys (the pom-pom wand...) are thin: a tap near one on the floor counts too.
            var fp = FloorPoint(panel, 0);
            if (!fp.HasValue) return null;
            float near = float.MaxValue;
            foreach (var it in items)
            {
                if (it.a.r > .35f || it.a.cat == "Floor" || it.arch == "tomb") continue;
                float d = Dist(fp.Value.x - it.tx, fp.Value.z - it.tz);
                if (d < Mathf.Max(.3f, it.a.r * 1.8f) && d < near) { near = d; best = it; }
            }
            return best;
        }

        private bool PetHit(Vector2 panel) { return pet.Pivot.gameObject.activeSelf && RayPick.Hit(PickRay(panel), pet.Body, false) >= 0; }

        private Vector3? FloorPoint(Vector2 panel, float liftPx)
        {
            var r = Space3.ThreeRay(cam, ToScreen(new Vector2(panel.x, panel.y - liftPx)));
            return Space3.Floor(r, Y0, out var hit) ? hit : (Vector3?)null;
        }

        // ---------------- input (the 3D canvas) ----------------

        public void CanvasDown(int id, Vector2 p, bool touch)
        {
            // A lost pointer-up (notification shade, system gesture, app switch) must never leave a stale finger
            // behind: with one stuck entry every new touch looked like a pinch and nothing in the room could be tapped.
            if (!touch || Input.touchCount <= 1) { ptrs.Clear(); drag = null; if (squeezing) { pet.EndPinch(); squeezing = false; } squeezeArm = false; }
            ptrs[id] = new Pointer { pos = p, t = Time.realtimeSinceStartup };
            if (mode == "unbox") { if (!UnboxSquishDown(p, touch)) HoldStart(); return; }
            if (ptrs.Count == 2)
            {
                var ab = ptrs.Values.ToArray();
                Vector2 a = ab[0].pos, b = ab[1].pos;
                if (mode == "edit" && moving != null) { twist = (Mathf.Atan2(b.y - a.y, b.x - a.x), moving.ry, 0); return; }
                pinch0 = Vector2.Distance(a, b);
                zoom0 = mode == "edit" ? camS.ezT : camS.zoomT;
                // Zoomed in with both fingers on the squishy: bringing them together squeezes it (spreading still zooms out).
                squeezeArm = mode == "home" && CloseUp && !S.tucked && PinchPoints(a, b, out sqA, out sqB);
                squeezeMid0 = (a + b) / 2;
                if (squishMode) { drag = null; pet.Held = false; return; } // squish mode: two fingers are for squeezing only
                camS.mid = (a + b) / 2;
                drag = null;
                pet.Held = false;
                return;
            }
            drag = new Drag { start = p, touch = touch };
            if (mode == "edit")
            {
                var it = ItemHit(p);
                if (it != null)
                {
                    Snap();
                    Select(it);
                    moving = it;
                    it.lift = 1;
                    sfx.Lift();
                    Buzz(12);
                    drag.item = it;
                    var fp = FloorPoint(p, 0);
                    drag.ox = fp.HasValue ? it.tx - fp.Value.x : 0;
                    drag.oz = fp.HasValue ? it.tz - fp.Value.z : 0;
                }
                else Select(null);
                return;
            }
            if (S.dead) return;
            bool onPet = PetHit(p);
            if (onPet && !S.tucked && S.tipPile > 0) TipCoins();
            if (ai.mode == "act" && ai.act != null && ai.act.role == "wand")
            {
                // Pom-pom chase: a touch anywhere steers the pom-pom (even on the squishy or furniture), so small hands can't miss.
                drag.wand = true;
                var fw = FloorPoint(p, 0);
                if (fw.HasValue) wandFinger = new Vector2(fw.Value.x, fw.Value.z);
                return;
            }
            if (onPet)
            {
                if (S.tucked) { Floater("Paused · see Settings"); return; }
                if (ai.mode == "act" && ai.act != null && ai.act.act.scrub) { drag.scrub = true; return; }
                if (CloseUp && BeginTactile(p)) return;
                drag.squish = true;
                drag.t0 = Time.realtimeSinceStartup;
                pet.Held = true;
                sfx.Squish();
                Buzz(12);
                return;
            }
            var hitIt = ItemHit(p);
            if (hitIt != null && hitIt.arch == "ball" && !squishMode)
            {
                drag.ball = hitIt;
                var fp = FloorPoint(p, 0);
                if (fp.HasValue) drag.hist.Add(new Vector3(fp.Value.x, fp.Value.z, Time.realtimeSinceStartup));
            }
        }

        public void CanvasMove(int id, Vector2 p)
        {
            if (!ptrs.TryGetValue(id, out var prevP)) return;
            var prev = prevP.pos;
            ptrs[id] = new Pointer { pos = p, t = Time.realtimeSinceStartup };
            if (drag != null && drag.unboxSq) { UnboxSquishMove(p); return; }
            if (mode == "unbox") return;
            if (ptrs.Count == 2)
            {
                var ab = ptrs.Values.ToArray();
                Vector2 a = ab[0].pos, b = ab[1].pos;
                if (twist.HasValue && moving != null)
                {
                    var tw = twist.Value;
                    float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) - tw.a0;
                    int step = Mathf.RoundToInt(ang / (Mathf.PI / 12));
                    if (step != tw.step) { twist = (tw.a0, tw.r0, step); moving.ry = tw.r0 - step * Mathf.PI / 12; sfx.Snap(); Buzz(5); }
                    return;
                }
                if (pinch0 > 0 && (squeezing || squeezeArm))
                {
                    float dq = Vector2.Distance(a, b);
                    if (squeezing)
                    {
                        float amt = Mathf.Clamp01((pinch0 - dq) / (pinch0 * .9f)); // fingers nearly touching: squeezed nearly flat
                        if (Mathf.Floor(amt * 5) != Mathf.Floor(squeezeLast * 5)) Buzz(3); // soft ticks as it gives
                        squeezeLast = amt;
                        pet.SetPinch(amt);
                        // Both fingers moving together push the squeezed lump around.
                        var mplane = new Plane(-cam.transform.forward, (sqA + sqB) / 2);
                        Ray r0 = PickRay(squeezeMid0), r1 = PickRay((a + b) / 2);
                        if (mplane.Raycast(r0, out float t0) && mplane.Raycast(r1, out float t1)) pet.DragPinch(Vector3.ClampMagnitude(r1.GetPoint(t1) - r0.GetPoint(t0), pet.Scale * pet.StageScale * .8f));
                        return;
                    }
                    if (dq < pinch0 - 8) { squeezing = true; squeezeArm = false; squeezeLast = 0; pet.BeginPinch(sqA, sqB); sfx.Press(); Buzz(8); return; }
                    if (dq > pinch0 + 8 && !squishMode) squeezeArm = false; // spreading: it's a zoom after all (not in squish mode)
                    else return;
                }
                if (pinch0 > 0)
                {
                    float d = Vector2.Distance(a, b);
                    if (mode == "edit")
                    {
                        camS.ezT = Mathf.Clamp01(zoom0 - (d - pinch0) / 250);
                        var mid = (a + b) / 2;
                        if (camS.mid.HasValue) PanBy(mid.x - camS.mid.Value.x, mid.y - camS.mid.Value.y);
                        camS.mid = mid;
                    }
                    else if (!squishMode)
                    {
                        camS.zoomT = Mathf.Clamp(zoom0 - (d - pinch0) / 220, MinZoom, 1);
                        if (camS.zoomT <= MinZoom + .01f) EnterSquishMode(); // all the way in: lock on for squishing
                    }
                }
                return;
            }
            if (drag != null && drag.tactile) { MoveTactile(p); return; }
            if (drag == null || drag.squish) return;
            if (!drag.moved && Vector2.Distance(p, drag.start) > 6) drag.moved = true;
            if (drag.item != null)
            {
                var fp = FloorPoint(p, drag.touch ? 55 : 0);
                if (!fp.HasValue) return;
                var it = drag.item;
                float x = fp.Value.x + (drag.touch ? 0 : drag.ox), z = fp.Value.z + (drag.touch ? 0 : drag.oz);
                float l = LimitFor(it), rr = Dist(x, z);
                if (it.a.cat == "Wall")
                {
                    // Room dividers snap end-on to the steamer wall, pointing into the room, when brought near it.
                    float snapR = FLOOR_R - HalfLength(it);
                    if (rr > snapR - .15f)
                    {
                        x *= snapR / rr;
                        z *= snapR / rr;
                        if (!it.atWall) { it.atWall = true; sfx.Snap(); Buzz(6); }
                        FaceCentreAt(it, x, z);
                        it.ry += Mathf.PI / 2;
                    }
                    else it.atWall = false;
                }
                else if (it.arch == "slide" && SnapSlideToTub(it, ref x, ref z)) { }
                else if (it.a.cat != "Floor" && SnapToPlacedWall(it, ref x, ref z)) { }
                else if (rr > l - .12f)
                {
                    x *= l / rr;
                    z *= l / rr;
                    if (!it.atWall) { it.atWall = true; sfx.Snap(); Buzz(6); }
                    FaceCentreAt(it, x, z);
                }
                else it.atWall = false;
                it.tx = x;
                it.tz = z;
                const float m2 = 44;
                int ex = p.x < m2 ? -1 : p.x > ui.Width - m2 ? 1 : 0, ey = p.y < m2 + 60 ? -1 : p.y > ui.Height - m2 - 150 ? 1 : 0;
                camS.edge = ex == 0 && ey == 0 ? (Vector2?)null : new Vector2(ex, ey);
                return;
            }
            if (drag.scrub)
            {
                if (PetHit(p))
                {
                    S.needs[Simulation.Game.Needs.Clean] = Mathf.Min(1, S.needs[Simulation.Game.Needs.Clean] + C.rules.scrubGain);
                    pet.V += .6f;
                    if (ai.act != null && !ai.act.scrubbed)
                    {
                        // Counts the moment you scrub (not when the bath ends), so the task can't be lost if the bath is cut short.
                        ai.act.scrubbed = true;
                        TaskEvent("scrub");
                    }
                    var pp = PetWorld();
                    // Scrubbing makes clear foam bubbles (the old white puffs read as golf balls).
                    for (int k = 0; k < 2; k++) bathBubbles.Spawn(new Vector3(pp.x + Rnd(-.12f, .12f), pp.y + pet.Scale * Rnd(.5f, .9f), pp.z + Rnd(-.12f, .12f)), new Vector3(Rnd(-.08f, .08f), Rnd(.1f, .25f), Rnd(-.08f, .08f)), Rnd(.016f, .03f), Rnd(.9f, 1.6f), 1.5f, .03f);
                }
                return;
            }
            if (drag.wand)
            {
                var fw = FloorPoint(p, 0);
                if (fw.HasValue) wandFinger = new Vector2(fw.Value.x, fw.Value.z);
                return;
            }
            if (drag.ball != null)
            {
                var fp = FloorPoint(p, 0);
                if (fp.HasValue) { drag.hist.Add(new Vector3(fp.Value.x, fp.Value.z, Time.realtimeSinceStartup)); if (drag.hist.Count > 6) drag.hist.RemoveAt(0); }
                return;
            }
            if (drag.moved)
            {
                float dx = p.x - prev.x, dy = p.y - prev.y;
                camS.yawV = -dx * .009f;
                camS.yaw += camS.yawV;
                if (mode == "edit") { camS.tiltT = Mathf.Clamp(camS.tiltT + dy * .3f, 30, 86); camS.tilt = camS.tiltT; SetTiltLbl(); }
                else camS.htilt = Mathf.Clamp(camS.htilt + dy * .15f, -10, 26);
            }
        }

        public void CanvasUp(int id, Vector2 p)
        {
            ptrs.Remove(id);
            if (ptrs.Count < 2) { pinch0 = 0; twist = null; EndSqueeze(); }
            if (mode == "unbox") { if (drag != null && drag.unboxSq) { UnboxSquishUp(); drag = null; return; } HoldEnd(); return; }
            if (drag == null) return;
            if (ptrs.Count > 0 && drag.item != null) return;
            if (drag.tactile) { EndTactile(); drag = null; return; }
            if (drag.squish && drag.flung) { drag = null; return; }
            if (drag.wand) { wandFinger = null; drag = null; return; }
            if (drag.item != null)
            {
                var it = drag.item;
                it.lift = 0;
                moving = null;
                camS.edge = null;
                Settle(it);
                RebuildObstacles();
                it.bv = -6;
                sfx.Drop();
                Buzz(10);
                TaskEvent("arrange");
                ComboFeedback(it);
                if (it.a.role == "tea" || it.a.role == "seat")
                {
                    var t = it.a.role == "tea" ? it : items.Find(x => x.a.role == "tea");
                    if (t != null) FloaterAt(t, SeatNear(t) ? "Tea station ready" : "Tea needs a seat nearby", SeatNear(t) ? null : "bad");
                }
            }
            else if (drag.squish)
            {
                pet.Held = false;
                SquishFx(PetWorld() + Vector3.up * pet.Scale * .6f, 5, .35f);
                if (visiting) VisitAct("pet");
                pet.Express(Squishy.Runtime.Models.SquishyModel.Mouth.Smile, 1.4f); // a happy little smile after a squish
                float gain = Condition() < .12f ? C.rules.squishPlayGainCritical : C.rules.squishPlayGain;
                S.needs[Simulation.Game.Needs.Play] = Mathf.Min(1, S.needs[Simulation.Game.Needs.Play] + gain);
                DrawNeeds();
                TaskEvent("squish");
            }
            else if (drag.ball != null)
            {
                var h = drag.hist;
                if (drag.moved && h.Count >= 2)
                {
                    Vector3 a = h[0], b = h[h.Count - 1];
                    float dt = Mathf.Max(.016f, b.z - a.z);
                    float vx = (b.x - a.x) / dt, vz = (b.y - a.y) / dt, sp = Dist(vx, vz), mx = 6;
                    if (sp > mx) { vx *= mx / sp; vz *= mx / sp; }
                    drag.ball.vx = vx;
                    drag.ball.vz = vz;
                    sfx.Kick();
                    Buzz(12);
                    UseItem(drag.ball, true);
                }
                else { drag.ball.bv = -7; sfx.Tap(); UseItem(drag.ball, true); }
            }
            else if (!drag.moved && !drag.scrub)
            {
                if (TapBubble(p)) { drag = null; return; }
                var it = squishMode ? null : ItemHit(p); // squish mode: taps are for the squishy only
                if (it != null)
                {
                    if (it.a.role == "music" && ai.mode == "act" && ai.act != null && ai.act.it == it) { PlayBar(it, BarAt(it, p), true); drag = null; return; }
                    it.bv = -7;
                    sfx.Tap();
                    Buzz(8);
                    if (!visiting && S.tipPile > 0 && it == energisedBy && ai.mode != "act") { TipCoins(); drag = null; return; } // tap what it played with
                    if (visiting && it.a.role != "plant") { Floater("Just visiting · their plants would love some water"); drag = null; return; }
                    if (it.a.role == "eat") OpenCook(it); else UseItem(it, true);
                }
            }
            drag = null;
        }

        public void CanvasWheel(Vector2 p, float deltaY)
        {
            if (mode == "unbox") return;
            if (mode == "edit")
            {
                bool zin = deltaY < 0;
                camS.ezT = Mathf.Clamp01(camS.ezT + deltaY * .0015f);
                if (zin)
                {
                    var fp = FloorPoint(p, 0);
                    if (fp.HasValue) { camS.panT.x += (fp.Value.x - camS.panT.x) * .15f; camS.panT.z += (fp.Value.z - camS.panT.z) * .15f; camS.panGoal = null; }
                }
                return;
            }
            if (squishMode) { if (deltaY > 0) ExitSquishMode(); return; }
            camS.zoomT = Mathf.Clamp(camS.zoomT + deltaY * .0015f, MinZoom, 1);
            if (camS.zoomT <= MinZoom + .01f) EnterSquishMode();
        }

        // ---------------- arrange ----------------

        private void Snap()
        {
            var s = new Snapshot { store = S.storage.ToList() };
            foreach (var it in items) s.list.Add((it, it.tx, it.tz, it.ry));
            undo.Add(s);
            if (undo.Count > 30) undo.RemoveAt(0);
            ui.SetUndoEnabled(true);
        }

        private void Select(Item it)
        {
            var was = sel;
            sel = it;
            if (was != it && mode == "edit") Later(0, DrawTray);
            if (it != null && camS.ezT < .6f) camS.panGoal = new Vector2(it.tx, it.tz);
            ui.SetRotEnabled(it != null);
            ui.SetPutAwayEnabled(it != null && it.arch != "tomb");
        }

        private Item rideItem; // the piece it was lying or sitting on when Arrange opened
        private Vector2 rideOff; // where on that piece (in the piece's own turned space)

        public void EnterEdit()
        {
            if (S.dead) return;
            CloseCook();
            SetMode("edit");
            camS.ezT = 1;
            camS.tiltT = 62;
            camS.panT = Vector3.zero;
            camS.panGoal = null;
            SetTiltLbl();
            Select(null);
            undo.Clear();
            ui.SetUndoEnabled(false);
            arrangeHave = ComboSnapshot(); // what counts as news while arranging
            // Lying or sitting on a piece: remember where on it, so it rides along when the piece moves.
            rideItem = ai.mode == "act" && ai.act != null && ai.act.it != null && (ai.act.act.perch > 0 || ai.act.act.inside || ai.act.night) ? ai.act.it : null;
            if (rideItem != null)
            {
                float dx = ai.x - rideItem.tx, dz = ai.z - rideItem.tz, c = Mathf.Cos(rideItem.ry), s = Mathf.Sin(rideItem.ry);
                rideOff = new Vector2(dx * c - dz * s, dx * s + dz * c);
            }
            pet.Held = false;
            ai.seg = null;
            sfx.Tap();
        }

        private void ExitEdit()
        {
            Select(null);
            moving = null;
            camS.edge = null;
            RebuildObstacles();
            // Still on its piece (it rode along): carry on (asleep in bed stays asleep). Otherwise start afresh.
            bool rode = rideItem != null && items.Contains(rideItem) && ai.act != null && ai.act.it == rideItem;
            bool floorSleep = ai.act != null && ai.act.night && ai.act.it == null;
            if (!rode)
            {
                FreePet();
                if (!floorSleep) { ai.mode = "idle"; ai.act = null; ai.idleT = 1; }
            }
            rideItem = null;
            SetMode("home");
            sfx.Tap();
            WriteSave();
        }

        public void Undo()
        {
            if (undo.Count == 0) return;
            var s = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            foreach (var it in items.ToList())
                if (!s.list.Any(e => e.it == it)) { items.Remove(it); S.items.Remove(it.st); it.g.gameObject.SetActive(false); }
            foreach (var e in s.list)
            {
                if (!items.Contains(e.it)) { items.Add(e.it); S.items.Add(e.it.st); e.it.g.gameObject.SetActive(true); }
                e.it.tx = e.x;
                e.it.tz = e.z;
                e.it.ry = e.r;
            }
            S.storage.Clear();
            S.storage.AddRange(s.store);
            DrawTray();
            ui.SetUndoEnabled(undo.Count > 0);
            RebuildObstacles();
            sfx.Tap();
            Buzz(8);
            ComboFeedback(null);
        }

        public void RotateSelected()
        {
            if (sel == null) return;
            Snap();
            sel.ry += Mathf.PI / 4;
            sel.bv = -4;
            Settle(sel);
            sfx.Snap();
            Buzz(8);
            ComboFeedback(sel);
        }

        /// <summary>Start fresh: every piece goes to storage (after a check), down to an empty room if you like.</summary>
        public void OnClearRoom()
        {
            if (mode != "edit") return;
            int n = items.Count(x => x.arch != "tomb");
            if (n == 0) { ui.SetHint("The room is already empty"); return; }
            sfx.Tap();
            ui.ShowDialog("Put everything away?", "All " + n + " pieces go to storage, so you can start the room fresh. Undo brings them back.", null,
                ("Put all away", "#6F9A74", "#4C7552", UI.Hud.Cream, (System.Action)(() =>
                {
                    ui.HideMemo();
                    Snap();
                    if (ai.act != null) FinishActivity();
                    foreach (var it in items.Where(x => x.arch != "tomb").ToList())
                    {
                        S.storage.Add(it.key);
                        items.Remove(it);
                        S.items.Remove(it.st);
                        it.g.gameObject.SetActive(false);
                    }
                    Select(null);
                    RebuildObstacles();
                    DrawTray();
                    ComboFeedback(null);
                    sfx.Drop();
                    Buzz(20);
                })),
                ("Keep them", "#EADCC6", "#CDB999", UI.Hud.Ink, (System.Action)(() => ui.HideMemo())));
        }

        public void PutAway()
        {
            if (sel == null || sel.arch == "tomb") return;
            if (ai.act != null && ai.act.it == sel) FinishActivity();
            Snap();
            S.storage.Add(sel.key);
            items.Remove(sel);
            S.items.Remove(sel.st);
            sel.g.gameObject.SetActive(false);
            Select(null);
            DrawTray();
            ComboFeedback(null);
            RebuildObstacles();
            sfx.Drop();
        }

        private void DrawTray()
        {
            if (sel != null && sel.arch != "tomb")
            {
                var all = C.Catalogue.Where(c => c.arch == sel.arch).ToList();
                var got = all.Where(c => Rules.Owned(c.key)).ToList();
                var shown = got.Concat(all.Where(c => !Rules.Owned(c.key)).Take(6)).ToList();
                var cur = sel;
                var skinTiles = shown.Select(c => (c.key, Rules.Owned(c.key), c.key == cur.key, c.name)).ToList();
                skinTiles.Insert(0, ("__back", true, false, "Back")); // back to storage and new pieces
                ui.DrawTrayTabs(null, null, null);
                ui.DrawTray(C.Type(sel.arch).name + " skins · " + got.Count + " of " + all.Count, skinTiles, null, key =>
                {
                    if (key == "__back") { Select(null); DrawTray(); sfx.Tap(); return; }
                    var c = C.Cat(key);
                    if (!Rules.Owned(key)) { ui.SetHint("Find the " + c.name + " in steamers"); sfx.Bonk(); return; }
                    Snap();
                    Restyle(cur, c.style);
                    TaskEvent("reskin");
                    cur.bv = -6;
                    RebuildObstacles();
                    DrawTray();
                    sfx.Snap();
                    Buzz(8);
                });
                return;
            }
            string title = "Storage (" + S.storage.Count + ") · Pieces " + Rules.ItemCount() + "/" + Rules.ItemSlots();
            // Tabs by room so the list stays short as the collection grows: what's stored, then each room's pieces.
            var stored = S.storage.Select((k, i) => (k + "#" + i, true, false, "Place " + C.Cat(k).name)).ToList();
            var fresh = NewPieces();
            var skins = Enumerable.Range(0, C.skins.Length).Where(i => i == 0 || Rules.Owned("skin:" + i)).ToList();
            var tabs = new List<(string id, string label)>();
            if (stored.Count > 0) tabs.Add(("stored", "Stored " + stored.Count));
            foreach (var r in C.trayRooms ?? new TrayRoomData[0])
            {
                int n = stored.Count(e => InRoom(e.Item1, r.id)) + fresh.Count(c => InRoom(c.key, r.id));
                if (n > 0) tabs.Add((r.id, r.name + " " + n));
            }
            if (skins.Count > 1) tabs.Add(("steamer", "Steamer"));
            if (trayTab == null || !tabs.Any(t => t.id == trayTab)) trayTab = tabs.Count > 0 ? tabs[0].id : null;
            ui.DrawTrayTabs(tabs, trayTab, id => { trayTab = id; DrawTray(); sfx.Tap(); });
            var list = new List<(string, bool, bool, string)>();
            if (trayTab == "stored") list.AddRange(stored);
            else if (trayTab != null && trayTab != "steamer")
            {
                list.AddRange(stored.Where(e => InRoom(e.Item1, trayTab)));
                var here = fresh.Where(c => InRoom(c.key, trayTab)).ToList();
                if (here.Count > 0) { list.Add(("__new", true, false, "New")); foreach (var c in here) list.Add((c.key + "#new", true, false, "New " + c.name)); }
            }
            // The steamer itself: pick a skin for the room right here.
            if (trayTab == "steamer" && skins.Count > 1) { foreach (int i in skins) list.Add(("skin:" + i + "#s", true, C.skins[i] == curSkin, C.skins[i].name + "|" + C.skins[i].a + "|" + C.skins[i].b)); }
            if (list.Count == 0) { ui.DrawTray(title, list, "Empty. Get more from steamers.", null); return; }
            ui.DrawTray(title, list, null, key =>
            {
                if (key.StartsWith("__")) return;
                if (key.EndsWith("#new")) { S.storage.Add(key.Substring(0, key.Length - 4)); PlaceFromStorage(S.storage.Count - 1); return; }
                if (key.StartsWith("skin:"))
                {
                    curSkin = C.skins[int.Parse(key.Substring(5, key.IndexOf('#') - 5))];
                    homeWall.Skin(curSkin);
                    UpdateMusic();
                    WriteSave();
                    sfx.Snap();
                    Buzz(8);
                    DrawTray();
                    return;
                }
                PlaceFromStorage(int.Parse(key.Substring(key.IndexOf('#') + 1)));
            });
        }

        private string trayTab;

        private bool InRoom(string key, string room)
        {
            var t = C.Type(key.Split(':')[0].Split('#')[0]);
            return t != null && GameRules.Fits(t.rooms, room);
        }

        /// <summary>
        /// Pieces you can add fresh: any decor you own while decor slots are free, and a second of the types that
        /// allow more than one per room (a sink for the bathroom and one for the kitchen, a second stool).
        /// </summary>
        private List<Squishy.Simulation.Game.CatalogueItem> NewPieces()
        {
            bool free = Rules.ItemCount() < Rules.ItemSlots();
            return C.Catalogue.Where(c => free && Rules.Owned(c.key) && (C.IsDecor(c.arch) ||
                    C.MaxPerRoom(c.arch) > 1 && items.Count(x => x.arch == c.arch) + S.storage.Count(k => k.StartsWith(c.arch + ":")) < C.MaxPerRoom(c.arch)))
                .OrderBy(c => c.arch).ToList();
        }

        private void PlaceFromStorage(int i)
        {
            string key0 = S.storage[i], arch = key0.Split(':')[0];
            // Every piece takes space (a toy swapped for the one out doesn't need more).
            bool swaps = items.Any(x => x.arch != "tomb" && x.arch != arch && C.SameSlot(x.arch, arch));
            if (!swaps && Rules.ItemCount() >= Rules.ItemSlots()) { ui.SetHint("Room full (" + Rules.ItemSlots() + " pieces). Level up the room for more space.", true); sfx.Bonk(); Buzz(20); return; }
            if (!C.IsDecor(arch) && items.Count(x => x.arch == arch) >= C.MaxPerRoom(arch)) { ui.SetHint("Only " + C.MaxPerRoom(arch) + " " + C.Type(arch).name.ToLowerInvariant() + " per room", true); sfx.Bonk(); Buzz(20); return; }
            Snap();
            string key = S.storage[i];
            S.storage.RemoveAt(i);
            float x = camS.target.x, z = camS.target.z;
            var swapped = SwapOutSlot(arch); // one toy at a time: the old one goes to storage
            for (int k = 0; k < 12; k++)
            {
                float a = Rnd(0, Mathf.PI * 2), r = Rnd(0, FLOOR_R * .7f), tx = Mathf.Cos(a) * r, tz = Mathf.Sin(a) * r;
                if (!obstacles.Any(o => Dist(o.x - tx, o.z - tz) < o.r + .35f)) { x = tx; z = tz; break; }
            }
            if (swapped != null) { x = swapped.tx; z = swapped.tz; }
            var it = AddItem(key, x, z, 0);
            it.g.localPosition = new Vector3(x, Y0 + 1.2f, z);
            it.bv = -6;
            Settle(it);
            RebuildObstacles();
            Select(it);
            DrawTray();
            sfx.Drop();
            Buzz(10);
            ComboFeedback(it);
        }

        private float LimitFor(Item it) { return FLOOR_R - (it.a.circles != null && it.a.circles.Length > 0 ? .2f : it.a.r * .8f); }

        /// <summary>Half an item's length along its own x axis (from its collision circles).</summary>
        private static float HalfLength(Item it)
        {
            var c = it.a.circles;
            if (c == null || c.Length < 3) return it.a.r;
            float h = 0;
            for (int i = 0; i + 2 < c.Length; i += 3) h = Mathf.Max(h, Mathf.Abs(c[i]) + c[i + 2]);
            return h;
        }

        /// <summary>
        /// Brought close to a placed room divider or screen, a piece slides along it with its back to it (either side),
        /// the way pieces snap to the steamer wall.
        /// </summary>
        private bool SnapToPlacedWall(Item it, ref float x, ref float z)
        {
            foreach (var w in items)
            {
                if (w == it || w.a.cat != "Wall") continue;
                float ux = Mathf.Cos(w.ry), uz = -Mathf.Sin(w.ry), nx = Mathf.Sin(w.ry), nz = Mathf.Cos(w.ry); // the wall's length and face
                float dx = x - w.tx, dz = z - w.tz, along = dx * ux + dz * uz, across = dx * nx + dz * nz;
                float half = HalfLength(w), thick = w.a.circles != null && w.a.circles.Length >= 3 ? w.a.circles[2] : .16f;
                float depth = it.a.circles != null && it.a.circles.Length > 0 ? .2f : it.a.r * .8f;
                if (Mathf.Abs(along) > half + .05f || Mathf.Abs(across) > thick + depth + .18f) continue;
                float side = across >= 0 ? 1 : -1;
                across = side * (thick + depth + .01f);
                along = Mathf.Clamp(along, -half, half);
                x = w.tx + ux * along + nx * across;
                z = w.tz + uz * along + nz * across;
                // Back to the wall: its front faces away from it.
                if (it.a.face == "z") it.ry = Mathf.Atan2(side * nx, side * nz);
                else if (it.a.face == "x") it.ry = Mathf.Atan2(-side * nz, side * nx);
                if (!it.atWall) { it.atWall = true; sfx.Snap(); Buzz(6); }
                return true;
            }
            return false;
        }

        private static void FaceCentreAt(Item it, float x, float z)
        {
            if (it.a.face == "z") it.ry = Mathf.Atan2(-x, -z);
            else if (it.a.face == "x") it.ry = Mathf.Atan2(z, -x);
        }

        /// <summary>Soft collision: items slide apart instead of refusing the drop.</summary>
        private void Settle(Item moved)
        {
            foreach (var b in items) if (b.arch == "ball") b.st.homeSet = false; // placed again: its home is wherever it ends up
            var solid = items.Where(it => !it.a.walk).ToList();
            for (int iter = 0; iter < 14; iter++)
            {
                for (int i = 0; i < solid.Count; i++)
                for (int j = i + 1; j < solid.Count; j++)
                {
                    Item A = solid[i], Bb = solid[j];
                    if (DockedPair(A, Bb)) continue; // a slide docked onto the tub overlaps it on purpose
                    foreach (var p in WorldCircles(A))
                    foreach (var q in WorldCircles(Bb))
                    {
                        float dx = q.x - p.x, dz = q.z - p.z, d = Mathf.Max(.001f, Dist(dx, dz)), min = p.r + q.r + .02f;
                        if (d >= min) continue;
                        float push = min - d, nx = dx / d, nz = dz / d, wa = A == moved ? .2f : .5f, wb = Bb == moved ? .2f : .5f;
                        // Walls stay put: whatever meets a wall is the one that moves (unless you are moving the wall).
                        bool aw = A.a.cat == "Wall" && A != moved, bw = Bb.a.cat == "Wall" && Bb != moved;
                        if (aw && !bw) { wa = 0; wb = 1; }
                        else if (bw && !aw) { wa = 1; wb = 0; }
                        A.tx -= nx * push * wa; A.tz -= nz * push * wa;
                        Bb.tx += nx * push * wb; Bb.tz += nz * push * wb;
                    }
                }
                foreach (var it in items)
                {
                    float l = LimitFor(it), rr = Dist(it.tx, it.tz);
                    if (rr > l) { it.tx *= l / rr; it.tz *= l / rr; }
                }
            }
        }

        // ---------------- room expansion ----------------

        /// <summary>A room level bought: space for one more piece.</summary>
        private void LevelUpRoom()
        {
            S.roomLv++;
            sfx.Chime();
            Buzz(20, 30, 20);
            ui.SetHint("Room level " + (S.roomLv + 1) + ": space for " + Rules.ItemSlots() + " pieces", true);
            if (items.Count > 0) foreach (var it in items) it.bv = -3;
            DrawTray();
            WriteSave();
        }

        /// <summary>The squishy reached a bigger size: the steamer grows wider round it.</summary>
        private void GrowRoom()
        {
            float oldR = HR;
            HR = Rules.RoomRadius();
            FLOOR_R = HR * .86f;
            Node.Destroy(homeWall.Group);
            homeWall = new SteamerModel(room, Mathf.RoundToInt(60 * HR / 2.3f), HR);
            homeWall.Skin(curSkin);
            Node.SetLayer(homeWall.Group, HomeLayer);
            homeWall.Group.localScale = Vector3.one * (oldR / HR);
            homeWall.Grow = 0;
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2;
                Glints(new Vector3(Mathf.Cos(a) * HR, .3f, Mathf.Sin(a) * HR), "#FFE08A", 1);
            }
            shake = .4f;
            sfx.Land();
            Buzz(30, 40, 30);
            ui.SetHint(Rules.Fav.name + " is " + C.sizes[Rules.FavSizeIdx].name + " now: the steamer grew bigger!", true);
            DrawTray();
            WriteSave();
        }
    }
}
