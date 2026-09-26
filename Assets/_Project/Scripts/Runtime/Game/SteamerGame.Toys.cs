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
        private readonly List<(Transform t, Vector3 p, float born)> bubbles = new List<(Transform, Vector3, float)>();
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
                    if (bubbles.Count == 0 && t > 1) ai.actT = a.act.dur;
                    return true;
                case "music":
                    extra = .12f * Mathf.Abs(Mathf.Sin(t * 8));
                    noteT -= dt;
                    if (noteT <= 0) { noteT = ai.self ? .55f : .45f; PlayBar(it, Random.Range(0, 6), false); }
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
                Node.Mesh(floatPom, ThreeGeo.Sph(.065f, 12, 10), ThreeMat.Pattern(C.Style(it.style) ?? C.Style("minimal")));
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

        private void BlowBubbles(Item it)
        {
            if (bubbleMat == null) bubbleMat = ThreeMat.Basic(ThreeMat.Lin("#DDF1F7"), .4f, ThreeMat.Blend.Alpha, depthWrite: false);
            sfx.Hop();
            for (int k = 0; k < 8; k++)
            {
                float a = Rnd(0, Mathf.PI * 2), r = Rnd(.2f, .7f);
                var p = new Vector3(it.tx + Mathf.Cos(a) * r, Y0 + Rnd(.12f, .35f), it.tz + Mathf.Sin(a) * r);
                var b = Node.Mesh(room, ThreeGeo.Sph(.055f, 12, 8), bubbleMat, p.x, p.y, p.z, shadow: false);
                Node.SetLayer(b, HomeLayer);
                bubbles.Add((b, p, time + Rnd(0, 6)));
            }
        }

        /// <summary>Bubbles drift and wobble; the squishy hops to pop the nearest one.</summary>
        private void StepBubbles(float dt, ref float lift)
        {
            if (bubbles.Count == 0) return;
            int best = 0;
            for (int k = 0; k < bubbles.Count; k++)
            {
                var (t0, p, born) = bubbles[k];
                p.y = Mathf.Min(Y0 + .45f, p.y + dt * .02f);
                bubbles[k] = (t0, p, born);
                t0.localPosition = p + new Vector3(Mathf.Sin(time * 2 + born) * .03f, Mathf.Sin(time * 3 + born) * .02f, 0);
                if (Dist(p.x - ai.x, p.z - ai.z) < Dist(bubbles[best].p.x - ai.x, bubbles[best].p.z - ai.z)) best = k;
            }
            var tgt = bubbles[best].p;
            ChaseTo(tgt.x, tgt.z, dt, ref lift);
            if (Dist(tgt.x - ai.x, tgt.z - ai.z) < PetRadius() + .05f) { pet.V += 2; PopBubble(best, .03f); }
        }

        private void PopBubble(int k, float gain)
        {
            var b = bubbles[k];
            bubbles.RemoveAt(k);
            HPuff(b.p, new Vector3(0, .3f, 0), .04f, .3f, 3, .2f);
            Node.Destroy(b.t);
            sfx.Snap();
            GainPlay(gain);
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
            Vector2 foot = new Vector2(it.tx - dx * .34f, it.tz - dz * .34f), top = new Vector2(it.tx - dx * .12f, it.tz - dz * .12f), end = new Vector2(it.tx + dx * .42f, it.tz + dz * .42f);
            var side = new Vector2(it.tx + dz * .34f, it.tz - dx * .34f); // beside the slide, halfway along
            Vector2 pos, look;
            if (u < .8f) { float k = u / .8f; pos = Vector2.Lerp(foot, top, k); ai.y = .47f * k; extra = .05f * Mathf.Sin(u * 30); look = top - foot; }
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
