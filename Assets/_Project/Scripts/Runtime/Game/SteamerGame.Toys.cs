using System.Collections.Generic;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Play toys beyond the ball (one toy out at a time): pom-pom wand, bubble wand, xylophone and slide.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private static readonly string[] PlayRoles = { "play", "wand", "bubbles", "music", "slide" };
        private Transform floatPom;
        private Vector3 pomPos, pomGoal;
        private Vector2? wandFinger;
        private sealed class Bubble { public Transform t; public Vector3 p, v; public float born, life; }
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private Item bubbleToy;
        private int bubbleLeft;
        private float bubbleEmitT, bubbleJumpT = -1, bubbleJumpH;
        private Bubble bubbleJumpFor;
        private Vector2? bubblePlant;
        private Material bubbleMat;
        private float noteT;

        private void StartToy(Activity a)
        {
            if (a.it == null) return;
            if (a.role == "wand") StartWand(a.it);
            if (a.role == "bubbles") BlowBubbles(a.it);
            if (a.role == "music") noteT = 0;
        }

        /// <summary>Per-frame toy play inside StepAct. Returns true when the role was a toy.</summary>
        private bool StepToy(Activity a, float dt, float t, ref float lift, ref float extra)
        {
            var it = a.it;
            if (it == null) return false;
            switch (a.role)
            {
                case "wand": StepWand(it, dt, t, ref lift); return true;
                case "bubbles":
                    StepBubbles(dt, ref lift);
                    if (bubbles.Count == 0 && bubbleLeft == 0 && t > 1) ai.actT = a.act.dur;
                    return true;
                case "music":
                    extra = .12f * Mathf.Abs(Mathf.Sin(t * 8));
                    noteT -= dt;
                    if (noteT <= 0) { noteT = a.act.role == "concert" ? .4f : ai.self ? .55f : .45f; PlayBar(it, a.act.role == "concert" ? ConcertBar() : Random.Range(0, 6), false); }
                    return true;
                case "slide": StepSlide(it, t, ref lift, ref extra); return true;
            }
            return false;
        }

        private void StartWand(Item it)
        {
            it.parts.pom.gameObject.SetActive(false);
            if (floatPom == null)
            {
                floatPom = Node.Group(room, "floatPom");
                Squishy.Runtime.Models.ItemModels.Pompom(floatPom, C.Style(it.style) ?? C.Style("minimal"), .065f);
                Node.SetLayer(floatPom, HomeLayer);
            }
            floatPom.gameObject.SetActive(true);
            pomPos = ThreeWorld(it.parts.pom);
            pomGoal = pomPos;
        }

        /// <summary>The pom-pom follows your finger (or darts about by itself); the squishy chases and bats it.</summary>
        private void StepWand(Item it, float dt, float t, ref float lift)
        {
            if (wandFinger.HasValue) pomGoal = new Vector3(wandFinger.Value.x, Y0 + .16f, wandFinger.Value.y);
            else if (Dist(pomPos.x - pomGoal.x, pomPos.z - pomGoal.z) < .05f)
            {
                float a = Rnd(0, Mathf.PI * 2), r = Rnd(.25f, .6f);
                pomGoal = new Vector3(it.tx + Mathf.Cos(a) * r, Y0 + .16f, it.tz + Mathf.Sin(a) * r);
            }
            pomPos = Vector3.MoveTowards(pomPos, pomGoal, dt * (wandFinger.HasValue ? 6 : 1.1f));
            floatPom.localPosition = pomPos + new Vector3(0, .05f * Mathf.Sin(t * 7), 0);
            ChaseTo(pomPos.x, pomPos.z, dt, ref lift);
            if (Dist(pomPos.x - ai.x, pomPos.z - ai.z) < PetRadius() + .08f)
            {
                pet.V += 2.5f;
                sfx.Kick();
                GainPlay(.03f);
                float a = Rnd(0, Mathf.PI * 2);
                if (!wandFinger.HasValue) pomGoal = new Vector3(it.tx + Mathf.Cos(a) * .5f, Y0 + .16f, it.tz + Mathf.Sin(a) * .5f);
                pomPos += new Vector3(Mathf.Cos(a) * .15f, 0, Mathf.Sin(a) * .15f);
            }
        }

        /// <summary>The wand waves and a stream of bubbles comes out of its ring over a few seconds.</summary>
        private void BlowBubbles(Item it)
        {
            if (bubbleMat == null) bubbleMat = ThreeMat.Basic(ThreeMat.Lin("#DDF1F7"), .4f, ThreeMat.Blend.Alpha, depthWrite: false);
            bubbleToy = it;
            bubbleLeft = 12;
            // Bubble garden: more of them, drifting out towards the plant.
            bubblePlant = null;
            if (ai.act != null && ai.act.combo != null && ai.act.act.role == "bubblegarden")
            {
                bubbleLeft = 18;
                foreach (var p in ai.act.combo.pieces) { var pi = ItemOf(p); if (pi != null && pi.arch == "plant") bubblePlant = new Vector2(pi.tx, pi.tz); }
            }
            bubbleEmitT = 0;
            bubbleJumpT = -1;
            sfx.Hop();
        }

        private void EmitBubble()
        {
            var it = bubbleToy;
            if (it == null || it.parts.wand == null) { bubbleLeft = 0; return; }
            var ring = ThreeWorld(it.parts.wand.childCount > 1 ? it.parts.wand.GetChild(1) : it.parts.wand);
            // Out across the room in any direction, at its own height.
            float a = Rnd(0, Mathf.PI * 2), sp = Rnd(.28f, .5f);
            if (bubblePlant.HasValue) { a = Mathf.Atan2(bubblePlant.Value.y - ring.z, bubblePlant.Value.x - ring.x) + Rnd(-.7f, .7f); sp = Rnd(.18f, .34f); }
            var bub = new Bubble { p = ring, v = new Vector3(Mathf.Cos(a) * sp, Rnd(.05f, .18f), Mathf.Sin(a) * sp), born = time, life = Rnd(9, 14) };
            bub.t = Node.Mesh(room, ThreeGeo.Sph(Rnd(.045f, .07f), 12, 8), bubbleMat, ring.x, ring.y, ring.z, shadow: false);
            Node.SetLayer(bub.t, HomeLayer);
            bubbles.Add(bub);
        }

        /// <summary>Bubbles float out over the whole room; the squishy chases the nearest, bops low ones and jumps for high ones.</summary>
        private void StepBubbles(float dt, ref float lift)
        {
            // Blowing: the wand waves while bubbles stream out of it.
            if (bubbleLeft > 0 && bubbleToy != null)
            {
                bubbleEmitT -= dt;
                if (bubbleEmitT <= 0) { EmitBubble(); bubbleLeft--; bubbleEmitT = Rnd(.18f, .35f); }
                if (bubbleToy.parts.wand != null) bubbleToy.parts.wand.RotZ(-.35f + .5f * Mathf.Sin(time * 9));
            }
            else if (bubbleToy != null && bubbleToy.parts.wand != null) bubbleToy.parts.wand.RotZ(-.35f);
            if (bubbles.Count == 0) return;
            float head = Y0 + pet.Scale * pet.StageScale * 1.35f, lim = FLOOR_R - .15f;
            Bubble best = null;
            float bd = float.MaxValue;
            for (int k = bubbles.Count - 1; k >= 0; k--)
            {
                var bb = bubbles[k];
                // Drift, slow down, wobble and bob gently between knee and well-above-head height.
                bb.v *= Mathf.Exp(-.35f * dt);
                bb.v.y += (Mathf.Sin(time * .9f + bb.born) * .04f - (bb.p.y - (Y0 + .45f)) * .15f) * dt;
                bb.p += bb.v * dt;
                float rr = Dist(bb.p.x, bb.p.z);
                if (rr > lim) { float nx = bb.p.x / rr, nz = bb.p.z / rr, vn = bb.v.x * nx + bb.v.z * nz; if (vn > 0) { bb.v.x -= 2 * vn * nx; bb.v.z -= 2 * vn * nz; } bb.p.x = nx * lim; bb.p.z = nz * lim; }
                bb.p.y = Mathf.Clamp(bb.p.y, Y0 + .12f, Y0 + .85f);
                bb.t.localPosition = bb.p + new Vector3(Mathf.Sin(time * 2 + bb.born) * .03f, Mathf.Sin(time * 3 + bb.born) * .02f, Mathf.Cos(time * 1.7f + bb.born) * .02f);
                if (time - bb.born > bb.life) { PopBubble(k, 0); continue; }
                float d = Dist(bb.p.x - ai.x, bb.p.z - ai.z);
                if (d < bd) { bd = d; best = bb; }
            }
            if (best == null) return;
            // A jump in progress: up to the bubble and back down.
            if (bubbleJumpT >= 0)
            {
                bubbleJumpT += dt;
                float u = Mathf.Clamp01(bubbleJumpT / .6f);
                lift = Mathf.Max(lift, bubbleJumpH * 4 * u * (1 - u));
                if (bubbleJumpFor != null && bubbles.Contains(bubbleJumpFor) && u > .35f && u < .65f && Dist(bubbleJumpFor.p.x - ai.x, bubbleJumpFor.p.z - ai.z) < PetRadius() + .12f)
                {
                    pet.V += 2.5f;
                    PopBubble(bubbles.IndexOf(bubbleJumpFor), .035f);
                    bubbleJumpFor = null;
                }
                if (u >= 1) { bubbleJumpT = -1; pet.V += 3; } // a squishy landing
                return;
            }
            ChaseTo(best.p.x, best.p.z, dt, ref lift);
            if (bd < PetRadius() + .1f)
            {
                if (best.p.y <= head) { pet.V += 2; PopBubble(bubbles.IndexOf(best), .03f); } // bop the low ones
                else { bubbleJumpT = 0; bubbleJumpH = best.p.y - head + .08f; bubbleJumpFor = best; pet.V -= 3; sfx.Hop(); } // jump for the high ones
            }
        }

        private void PopBubble(int k, float gain)
        {
            var b = bubbles[k];
            bubbles.RemoveAt(k);
            Glints(b.p, "#E8F7FB", 2); // a bubble pops into a couple of glints, not a puff of steam
            Node.Destroy(b.t);
            sfx.Snap();
            if (gain > 0) GainPlay(gain);
        }

        /// <summary>Tap a bubble to pop it yourself. Returns true if one was hit.</summary>
        private bool TapBubble(Vector2 panel)
        {
            var ray = PickRay(panel);
            for (int k = 0; k < bubbles.Count; k++)
                if (RayPick.Hit(ray, bubbles[k].t) >= 0) { PopBubble(k, .02f); Buzz(8); return true; }
            return false;
        }

        /// <summary>A xylophone note: the bar dips, the squishy dances.</summary>
        private void PlayBar(Item it, int bar, bool byPlayer)
        {
            if (byPlayer && !visiting) TaskEvent("note");
            bar = Mathf.Clamp(bar, 0, 5);
            sfx.Note(bar);
            var b = it.parts.bars[bar];
            var rest = new Vector3(-.2f + bar * .08f, .09f, 0);
            b.localPosition = rest - new Vector3(0, .012f, 0);
            Later(.12f, () => { if (b != null) b.localPosition = rest; });
            pet.V += byPlayer ? 1.6f : .8f;
            if (byPlayer) { GainPlay(.03f); ai.actT = Mathf.Max(0, ai.actT - 1.2f); }
        }

        /// <summary>Which bar of a xylophone a tap landed on.</summary>
        private int BarAt(Item it, Vector2 panel)
        {
            var r = Space3.ThreeRay(cam, ToScreen(panel));
            if (!Space3.Floor(r, Y0 + .09f, out var hit)) return Random.Range(0, 6);
            var local = it.g.InverseTransformPoint(world.TransformPoint(hit));
            return Mathf.RoundToInt((local.x + .2f) / .08f);
        }

        /// <summary>Climb the ladder, pause on top, whoosh down the ramp, hop back round.</summary>
        private void StepSlide(Item it, float t, ref float lift, ref float extra)
        {
            // One run = climb the ladder, pause at the top, slide down, then hop back round the side (never through
            // the slide). The activity lasts two runs.
            const float Run = 2.4f;
            float dx = Mathf.Sin(it.ry), dz = Mathf.Cos(it.ry), u = t % Run;
            // Splash slide, docked onto the tub: one run, down the slide and on into the water in one go.
            var dock = SplashDocked(it) ? DockedTub(it) : null;
            if (dock != null && t >= 1f)
            {
                var bath = SpotOf(dock, C.Activity("splash") ?? C.Activity("bath"));
                Vector2 top0 = new Vector2(it.tx - dx * .12f, it.tz - dz * .12f);
                float k = Mathf.Clamp01((t - 1f) / .55f);
                var p = Vector2.Lerp(top0, bath.stand, k);
                ai.x = p.x;
                ai.z = p.y;
                ai.y = Mathf.Lerp(.47f, bath.y, k) + .07f * Mathf.Sin(k * Mathf.PI);
                extra = -.1f;
                petYawY = Mathf.Atan2(bath.stand.x - top0.x, bath.stand.y - top0.y);
                if (k >= 1) ai.actT = Mathf.Max(ai.actT, ai.act.act.dur); // in: the splash takes over
                return;
            }
            // The ladder is at the back (z -.23): climb up the outside of it, not through it.
            float back = .23f + PetRadius() * .75f;
            Vector2 foot = new Vector2(it.tx - dx * back, it.tz - dz * back), top = new Vector2(it.tx - dx * .12f, it.tz - dz * .12f), end = new Vector2(it.tx + dx * .42f, it.tz + dz * .42f);
            var side = new Vector2(it.tx + dz * .34f, it.tz - dx * .34f); // beside the slide, halfway along
            Vector2 pos, look;
            if (u < .6f) { float k = u / .6f; pos = foot; ai.y = .5f * k; extra = .05f * Mathf.Sin(u * 30); look = top - foot; } // straight up the ladder
            else if (u < .8f) { float k = (u - .6f) / .2f; pos = Vector2.Lerp(foot, top, k); ai.y = .5f - .03f * k + .06f * Mathf.Sin(k * Mathf.PI); look = top - foot; } // hop over onto the platform
            else if (u < 1f) { pos = top; ai.y = .47f; look = end - top; }
            else if (u < 1.5f) { float k = (u - 1f) / .5f; pos = Vector2.Lerp(top, end, k); ai.y = .47f * (1 - k); extra = -.1f; look = end - top; }
            else
            {
                float k = (u - 1.5f) / (Run - 1.5f);
                bool first = k < .5f;
                var a = first ? end : side;
                var b = first ? side : foot;
                float kk = first ? k * 2 : (k - .5f) * 2;
                pos = Vector2.Lerp(a, b, kk);
                ai.y = 0;
                lift = .12f * Mathf.Abs(Mathf.Sin(Mathf.PI * kk * 2));
                look = b - a;
            }
            ai.x = pos.x;
            ai.z = pos.y;
            petYawY = Mathf.Atan2(look.x, look.y);
        }

        private void ChaseTo(float x, float z, float dt, ref float lift)
        {
            float d = Dist(x - ai.x, z - ai.z);
            if (d < .01f) return;
            float step = Mathf.Min(d, 1.1f * (.8f + pet.Scale * 2) * dt);
            ai.x += (x - ai.x) / d * step;
            ai.z += (z - ai.z) / d * step;
            YawTo(x, z, dt, 10);
            ai.hopPh += step / (.16f + pet.Scale * .6f);
            lift = pet.Scale * .9f * Mathf.Abs(Mathf.Sin(Mathf.PI * ai.hopPh));
        }

        private void GainPlay(float g)
        {
            float cap = ai.self ? C.rules.selfCareCap : 1;
            if (S.needs[Needs.Play] < cap) S.needs[Needs.Play] = Mathf.Min(cap, S.needs[Needs.Play] + g);
        }

        private void EndToys()
        {
            if (floatPom != null) floatPom.gameObject.SetActive(false);
            foreach (var it in items) if (it.parts.pom != null) it.parts.pom.gameObject.SetActive(true);
            foreach (var b in bubbles) Node.Destroy(b.t);
            bubbles.Clear();
            bubbleLeft = 0;
            bubbleJumpT = -1;
            if (bubbleToy != null && bubbleToy.parts.wand != null) bubbleToy.parts.wand.RotZ(-.35f);
            bubbleToy = null;
            wandFinger = null;
            if (ai.act != null && ai.act.role == "slide") ai.y = 0;
        }

        /// <summary>Placing a toy from storage swaps out the one in the room (one toy at a time).</summary>
        private Item SwapOutSlot(string arch)
        {
            var t = C.Type(arch);
            if (t == null || string.IsNullOrEmpty(t.slot)) return null;
            var cur = items.Find(x => x.arch != arch && C.SameSlot(x.arch, arch));
            if (cur == null) return null;
            if (ai.act != null && ai.act.it == cur) FinishActivity();
            S.storage.Add(cur.key);
            items.Remove(cur);
            S.items.Remove(cur.st);
            cur.g.gameObject.SetActive(false);
            return cur;
        }
    }
}
