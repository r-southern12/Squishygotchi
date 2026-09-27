using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// The squishy as a tactile toy (user request, 27 Sep 2026). Zoomed right in, touches press slow-rise dents
    /// into it (SquishyModel.Tactile). Zoomed out, holding on it squeezes it until it pings out from under your
    /// finger and bounces round the room. Rare and better squishies shed soft particles in their own style
    /// (glitter, galaxy stars, glow motes, holographic rainbow) whenever they're squished or bounce.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private ParticlePool fxPool;
        private Vector3 flingV;
        private float flingSpin;

        private bool CloseUp { get { return camS.zoom < -.6f; } }

        // ---------------- tactile dents (close-up) ----------------

        /// <summary>Starts a press on the squishy at a screen point. Returns false if the finger missed it.</summary>
        private bool BeginTactile(Vector2 p)
        {
            var ray = PickRay(p);
            float t = RayPick.Hit(ray, pet.Body, false);
            if (t < 0) return false;
            var hit = ray.GetPoint(t);
            drag.tactile = true;
            drag.dent = pet.PressAt(hit, C.rules.dentRadius);
            if (!visiting) TaskEvent("dent");
            if (ai.mode == "walk" || ai.mode == "chase") { ai.mode = "idle"; ai.path = null; }
            ai.idleT = Mathf.Max(ai.idleT, 3);
            sfx.Press();
            Buzz(4);
            SquishFx(world.InverseTransformPoint(hit), 2, .2f);
            return true;
        }

        private void MoveTactile(Vector2 p)
        {
            var ray = PickRay(p);
            float t = RayPick.Hit(ray, pet.Body, false);
            if (t < 0) return;
            var hit = ray.GetPoint(t);
            int was = drag.dent;
            drag.dent = pet.MovePress(drag.dent, hit);
            if (drag.dent != was)
            {
                // Each new dent in a smear: a soft squish and a few motes.
                if (!visiting) TaskEvent("dent");
                // A smear is quiet: the faintest tick and the odd mote.
                Buzz(2);
                if (Random.value < .3f) SquishFx(world.InverseTransformPoint(hit), 1, .15f);
            }
        }

        private void EndTactile()
        {
            pet.Release(drag.dent);
            pet.Express(SquishyModel.Mouth.Smile, 1.2f);
            float gain = (Condition() < .12f ? C.rules.squishPlayGainCritical : C.rules.squishPlayGain) * .6f;
            S.needs[Needs.Play] = Mathf.Min(1, S.needs[Needs.Play] + gain);
            DrawNeeds();
            if (visiting) VisitAct("pet"); else TaskEvent("squish");
        }

        // ---------------- squishing the new squishy in its steamer ----------------

        /// <summary>On the reveal, the squishy fills its steamer like the toy: press it, hold deeper, drag to smear.</summary>
        private bool UnboxSquishDown(Vector2 p, bool touch)
        {
            if (!newbie.Pivot.gameObject.activeSelf || !(ustate == "card" || ustate == "landed")) return false;
            var ray = PickRay(p);
            float t = RayPick.Hit(ray, newbie.Body, false);
            if (t < 0) return false;
            drag = new Drag { start = p, touch = touch, unboxSq = true };
            drag.dent = newbie.PressAt(ray.GetPoint(t), C.rules.dentRadius * 1.6f);
            newbie.Express(SquishyModel.Mouth.Oh, .8f);
            sfx.Press();
            Buzz(5);
            return true;
        }

        private void UnboxSquishMove(Vector2 p)
        {
            var ray = PickRay(p);
            float t = RayPick.Hit(ray, newbie.Body, false);
            if (t < 0) return;
            int was = drag.dent;
            drag.dent = newbie.MovePress(drag.dent, ray.GetPoint(t));
            if (drag.dent != was) Buzz(2);
        }

        private void UnboxSquishUp()
        {
            newbie.ReleaseAll();
            newbie.Express(SquishyModel.Mouth.Smile, 1.2f);
        }

        // ---------------- hold to ping (zoomed out) ----------------

        /// <summary>Called every frame: a squish held long enough pings the squishy out from under the finger.</summary>
        private void CheckFlingHold()
        {
            if (drag == null || !drag.squish || drag.flung || S.tucked || S.dead) return;
            if (Time.realtimeSinceStartup - drag.t0 < C.rules.flingHold) return;
            drag.flung = true;
            pet.Held = false;
            if (ai.mode == "act") FinishActivity();
            var fp = FloorPoint(drag.start, 0);
            var dir = fp.HasValue ? new Vector2(ai.x - fp.Value.x, ai.z - fp.Value.z) : Vector2.zero;
            if (dir.sqrMagnitude < 1e-4f) { float a = Random.value * Mathf.PI * 2; dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
            dir.Normalize();
            float sp = C.rules.flingSpeed * (1 + Random.Range(-.15f, .15f));
            flingV = new Vector3(dir.x * sp, sp * .95f, dir.y * sp);
            flingSpin = Random.Range(-9f, 9f);
            ai.mode = "fling";
            ai.path = null;
            ai.act = null;
            ui.HideBubble();
            pet.Express(SquishyModel.Mouth.Oh, .9f);
            sfx.Pop();
            Buzz(20);
            SquishFx(PetWorld() + Vector3.up * pet.Scale * .6f, 8, .6f);
        }

        /// <summary>Flying and bouncing: gravity, the floor, the steamer wall and furniture.</summary>
        private void StepFling(float dt, ref float lift, ref float extra)
        {
            flingV.y -= 9f * dt;
            ai.x += flingV.x * dt;
            ai.z += flingV.z * dt;
            ai.y += flingV.y * dt;
            petYawY += flingSpin * dt;
            flingSpin *= Mathf.Exp(-1.5f * dt);
            float rr = Dist(ai.x, ai.z), lim = FLOOR_R - .1f;
            if (rr > lim)
            {
                float nx = ai.x / rr, nz = ai.z / rr, vn = flingV.x * nx + flingV.z * nz;
                if (vn > 0) { flingV.x -= 1.7f * vn * nx; flingV.z -= 1.7f * vn * nz; Bounce(.5f); }
                ai.x = nx * lim;
                ai.z = nz * lim;
            }
            if (ai.y < .45f)
                foreach (var it in items)
                {
                    if (it.a.cat == "Floor" || it.arch == "tomb") continue;
                    float r = it.a.r * 1.1f + pet.Scale * .5f, dx = ai.x - it.tx, dz = ai.z - it.tz, d = Dist(dx, dz);
                    if (d >= r || d < 1e-4f) continue;
                    float nx = dx / d, nz = dz / d, vn = flingV.x * nx + flingV.z * nz;
                    if (vn < 0) { flingV.x -= 1.7f * vn * nx; flingV.z -= 1.7f * vn * nz; it.bv = -5; Bounce(.4f); }
                    ai.x = it.tx + nx * r;
                    ai.z = it.tz + nz * r;
                }
            if (ai.y <= 0)
            {
                ai.y = 0;
                if (flingV.y < -.7f)
                {
                    flingV.y = -flingV.y * .5f;
                    flingV.x *= .75f;
                    flingV.z *= .75f;
                    extra = .4f; // splat on landing
                    Bounce(Mathf.Clamp01(-flingV.y / 3f));
                }
                else
                {
                    flingV.y = 0;
                    float k = Mathf.Exp(-6 * dt);
                    flingV.x *= k;
                    flingV.z *= k;
                    if (Dist(flingV.x, flingV.z) < .06f) EndFling();
                }
            }
            else extra = -.08f; // stretched in the air
        }

        private void Bounce(float strength)
        {
            sfx.Land();
            Buzz(Mathf.RoundToInt(6 + strength * 14));
            var p = PetWorld();
            for (int i = 0; i < 3; i++) HPuff(new Vector3(p.x + Rnd(-.1f, .1f), p.y + .02f, p.z + Rnd(-.1f, .1f)), new Vector3(Rnd(-.3f, .3f), .2f, Rnd(-.3f, .3f)), .03f, .5f);
            SquishFx(p + Vector3.up * pet.Scale * .4f, Mathf.RoundToInt(3 + strength * 6), .45f);
        }

        private void EndFling()
        {
            if (!visiting) TaskEvent("fling");
            ai.mode = "idle";
            ai.idleT = 1.5f;
            ai.y = 0;
            pet.Express(SquishyModel.Mouth.Grin, 1.4f);
            float gain = Condition() < .12f ? C.rules.squishPlayGainCritical : C.rules.squishPlayGain;
            S.needs[Needs.Play] = Mathf.Min(1, S.needs[Needs.Play] + gain);
            DrawNeeds();
            if (visiting) VisitAct("pet"); else TaskEvent("squish");
        }

        // ---------------- rarity particles ----------------

        /// <summary>
        /// Soft, slow motes in the squishy's own style (Rare and better only): glitter sparkles, galaxy stars,
        /// glow motes, holographic rainbow flecks, legendary gold. A calm, ASMR-ish shimmer rather than a burst.
        /// </summary>
        private void SquishFx(Vector3 at, int n, float spread)
        {
            if (fxPool == null || pet.Fin == null) return;
            var f = pet.Fin;
            string rar = C.FinishRarity(f), tier = f.tier ?? "";
            if (rar == "Common") return;
            n = Mathf.Max(1, n / 2); // a gentle shimmer, never a shower
            float big = rar == "Legendary" ? 1.25f : rar == "Epic" ? 1.1f : 1;
            for (int i = 0; i < n; i++)
            {
                Color col;
                if (tier == "Holographic") col = Color.HSVToRGB(Random.value, .5f, 1);
                else if (tier == "UV" && !string.IsNullOrEmpty(f.glow)) col = ThreeMat.Hex(f.glow);
                else if (f.spark != null && f.spark.Length > 0) col = ThreeMat.Hex(f.spark[Random.Range(0, f.spark.Length)]);
                else if (rar == "Legendary") col = ThreeMat.Hex("#FFE08A");
                else col = Color.Lerp(ThreeMat.Hex(string.IsNullOrEmpty(f.color) ? "#FFFFFF" : f.color), Color.white, .5f);
                // Glints lift gently off the surface and drift out, twinkling as they slowly turn: calm, never a burst.
                float a = Random.value * Mathf.PI * 2;
                var outDir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var v = outDir * Rnd(.08f, .22f) * (spread / .3f) + Vector3.up * Rnd(.12f, .3f);
                float size = Rnd(.045f, .085f) * big * (tier == "Glitter" ? .8f : 1);
                float life = tier == "Galaxy" ? Rnd(1.6f, 2.4f) : Rnd(1f, 1.6f);
                fxPool.Spawn(at + outDir * Rnd(.02f, .08f), v, size, life, 1.4f, tier == "Glitter" ? -.05f : .02f,
                    w: new Vector3(Rnd(0, 6), 0, Rnd(-1.5f, 1.5f)), col: col * (tier == "UV" ? 1.2f : 1));
            }
        }
    }
}
