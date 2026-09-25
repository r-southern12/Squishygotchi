using System.Collections.Generic;
using System.Linq;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
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
            float z = c.zoom, e = c.edit;
            float hx = ai.x * (1 - z), hz = ai.z * (1 - z), hy = (Y0 + pet.Scale + ai.y) * (1 - z) + .3f * z;
            float tx = hx + (c.panT.x - hx) * e, tz = hz + (c.panT.z - hz) * e, ty = hy + (.25f - hy) * e;
            float kk = Mathf.Min(1, dt * (e > .5f ? 12 : 3));
            c.target += new Vector3((tx - c.target.x) * kk, (ty - c.target.y) * kk, (tz - c.target.z) * kk);
            float near = .45f + pet.Scale * 2.4f;
            float dH = FitDist(near + (HR * 1.12f - near) * z), dE = FitDist(.7f + (HR * 1.12f - .7f) * c.ez), elH = 42 + 14 * z + c.htilt;
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

        public void ToggleView()
        {
            camS.zoomT = camS.zoomT > .5f ? 0 : 1;
            ui.SetViewIcon(camS.zoomT > .5f);
            sfx.Tap();
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
            ptrs[id] = new Pointer { pos = p, t = Time.realtimeSinceStartup };
            if (mode == "unbox") { HoldStart(); return; }
            if (ptrs.Count == 2)
            {
                var ab = ptrs.Values.ToArray();
                Vector2 a = ab[0].pos, b = ab[1].pos;
                if (mode == "edit" && moving != null) { twist = (Mathf.Atan2(b.y - a.y, b.x - a.x), moving.ry, 0); return; }
                pinch0 = Vector2.Distance(a, b);
                zoom0 = mode == "edit" ? camS.ezT : camS.zoomT;
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
            if (PetHit(p))
            {
                if (ai.mode == "act" && ai.act != null && ai.act.act.scrub) { drag.scrub = true; return; }
                drag.squish = true;
                pet.Held = true;
                sfx.Squish();
                Buzz(12);
                return;
            }
            var hitIt = ItemHit(p);
            if (hitIt != null && hitIt.arch == "ball")
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
                    else camS.zoomT = Mathf.Clamp01(zoom0 - (d - pinch0) / 220);
                }
                return;
            }
            if (drag == null || drag.squish) return;
            if (!drag.moved && Vector2.Distance(p, drag.start) > 6) drag.moved = true;
            if (drag.item != null)
            {
                var fp = FloorPoint(p, drag.touch ? 55 : 0);
                if (!fp.HasValue) return;
                var it = drag.item;
                float x = fp.Value.x + (drag.touch ? 0 : drag.ox), z = fp.Value.z + (drag.touch ? 0 : drag.oz);
                float l = LimitFor(it), rr = Dist(x, z);
                if (rr > l - .12f)
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
                    if (ai.act != null) ai.act.scrubbed = true;
                    var pp = PetWorld();
                    HPuff(new Vector3(pp.x + Rnd(-.1f, .1f), pp.y + pet.Scale * .9f, pp.z + Rnd(-.1f, .1f)), new Vector3(Rnd(-.3f, .3f), .5f, Rnd(-.3f, .3f)), Rnd(.03f, .05f), .6f, 2, .2f);
                }
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
            if (ptrs.Count < 2) { pinch0 = 0; twist = null; }
            if (mode == "unbox") { HoldEnd(); return; }
            if (drag == null) return;
            if (ptrs.Count > 0 && drag.item != null) return;
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
                if (it.a.role == "tea" || it.a.role == "seat")
                {
                    var t = it.a.role == "tea" ? it : items.Find(x => x.a.role == "tea");
                    if (t != null) FloaterAt(t, SeatNear(t) ? "Tea station ready" : "Tea needs a seat nearby", SeatNear(t) ? null : "bad");
                }
            }
            else if (drag.squish)
            {
                pet.Held = false;
                float gain = Condition() < .12f ? C.rules.squishPlayGainCritical : C.rules.squishPlayGain;
                S.needs[Simulation.Game.Needs.Play] = Mathf.Min(1, S.needs[Simulation.Game.Needs.Play] + gain);
                DrawNeeds();
                Floater("+Play");
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
                var it = ItemHit(p);
                if (it != null)
                {
                    it.bv = -7;
                    sfx.Tap();
                    Buzz(8);
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
            camS.zoomT = Mathf.Clamp01(camS.zoomT + deltaY * .0015f);
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
            FreePet();
            ai.mode = "idle";
            ai.act = null;
            ai.idleT = 1;
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
                ui.DrawTray(C.Type(sel.arch).name + " skins · " + got.Count + " of " + all.Count, shown.Select(c => (c.key, Rules.Owned(c.key), c.key == cur.key, c.name)).ToList(), null, key =>
                {
                    var c = C.Cat(key);
                    if (!Rules.Owned(key)) { ui.SetHint("Find the " + c.name + " in steamers"); sfx.Bonk(); return; }
                    Snap();
                    Restyle(cur, c.style);
                    cur.bv = -6;
                    RebuildObstacles();
                    DrawTray();
                    sfx.Snap();
                    Buzz(8);
                });
                return;
            }
            string title = "Storage (" + S.storage.Count + ") · Decor " + DecorCount() + "/" + Rules.RoomLevel.slots;
            if (S.storage.Count == 0) { ui.DrawTray(title, new List<(string, bool, bool, string)>(), "Empty. Get more from steamers or the shop.", null); return; }
            var list = S.storage.Select((k, i) => (k + "#" + i, true, false, "Place " + C.Cat(k).name)).ToList();
            ui.DrawTray(title, list, null, key => PlaceFromStorage(int.Parse(key.Substring(key.IndexOf('#') + 1))));
        }

        private void PlaceFromStorage(int i)
        {
            string key0 = S.storage[i], arch = key0.Split(':')[0];
            if (C.IsDecor(arch) && DecorCount() >= Rules.RoomLevel.slots) { ui.SetHint("Decor full (" + Rules.RoomLevel.slots + "). Expand the room for more.", true); sfx.Bonk(); Buzz(20); return; }
            if (!C.IsDecor(arch) && items.Count(x => x.arch == arch) >= C.MaxPerRoom(arch)) { ui.SetHint("Only " + C.MaxPerRoom(arch) + " " + C.Type(arch).name.ToLowerInvariant() + " per room", true); sfx.Bonk(); Buzz(20); return; }
            Snap();
            string key = S.storage[i];
            S.storage.RemoveAt(i);
            float x = camS.target.x, z = camS.target.z;
            for (int k = 0; k < 12; k++)
            {
                float a = Rnd(0, Mathf.PI * 2), r = Rnd(0, FLOOR_R * .7f), tx = Mathf.Cos(a) * r, tz = Mathf.Sin(a) * r;
                if (!obstacles.Any(o => Dist(o.x - tx, o.z - tz) < o.r + .35f)) { x = tx; z = tz; break; }
            }
            var it = AddItem(key, x, z, 0);
            it.g.localPosition = new Vector3(x, Y0 + 1.2f, z);
            it.bv = -6;
            Settle(it);
            RebuildObstacles();
            Select(it);
            DrawTray();
            sfx.Drop();
            Buzz(10);
        }

        private float LimitFor(Item it) { return FLOOR_R - (it.a.circles != null && it.a.circles.Length > 0 ? .2f : it.a.r * .8f); }

        private static void FaceCentreAt(Item it, float x, float z)
        {
            if (it.a.face == "z") it.ry = Mathf.Atan2(-x, -z);
            else if (it.a.face == "x") it.ry = Mathf.Atan2(z, -x);
        }

        /// <summary>Soft collision: items slide apart instead of refusing the drop.</summary>
        private void Settle(Item moved)
        {
            var solid = items.Where(it => !it.a.walk).ToList();
            for (int iter = 0; iter < 14; iter++)
            {
                for (int i = 0; i < solid.Count; i++)
                for (int j = i + 1; j < solid.Count; j++)
                {
                    Item A = solid[i], Bb = solid[j];
                    foreach (var p in WorldCircles(A))
                    foreach (var q in WorldCircles(Bb))
                    {
                        float dx = q.x - p.x, dz = q.z - p.z, d = Mathf.Max(.001f, Dist(dx, dz)), min = p.r + q.r + .02f;
                        if (d >= min) continue;
                        float push = min - d, nx = dx / d, nz = dz / d, wa = A == moved ? .2f : .5f, wb = Bb == moved ? .2f : .5f;
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

        private void ExpandRoom()
        {
            float oldR = HR;
            S.roomLv++;
            HR = C.roomLevels[S.roomLv].r;
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
                HPuff(new Vector3(Mathf.Cos(a) * HR, .3f, Mathf.Sin(a) * HR), new Vector3(Mathf.Cos(a) * 1.2f, .8f, Mathf.Sin(a) * 1.2f), .14f, .8f, 2, .4f);
            }
            shake = .4f;
            sfx.Land();
            Buzz(30, 40, 30);
            ui.SetHint("Room expanded to level " + (S.roomLv + 1) + "!", true);
            DrawTray();
            WriteSave();
        }
    }
}
