using UnityEngine;
using Random = UnityEngine.Random;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Happy tricks (user request, 4 Oct 2026): now and then, while Happy and idle, the squishy does a bounce, a bounce
    /// with a full spin, or a bounce with a flip.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private int trickKind = -1; // -1 none, 0 bounce, 1 spin, 2 flip
        private float trickT, trickWait = 8, trickSpin, trickFlip;
        private bool trickLanded;
        private const float TrickCrouch = .14f, TrickSettle = .25f;

        private static float TrickAir(int kind) { return kind == 0 ? .45f : .62f; }

        /// <summary>Idle: maybe start a trick; while one runs, its lift, spin and flip. True while tricking.</summary>
        private bool StepTrick(float dt, float cond, ref float lift)
        {
            if (trickKind < 0)
            {
                if (cond <= .5f || S.asleep || S.tucked || reduce) { trickWait = Mathf.Max(trickWait, 4); return false; }
                trickWait -= dt;
                if (trickWait > 0) return false;
                trickWait = Rnd(12, 25);
                StartTrick(Random.Range(0, 3));
            }
            trickT += dt;
            float air = TrickAir(trickKind), a = Mathf.Clamp01((trickT - TrickCrouch) / air);
            if (trickT < TrickCrouch) return true; // a little crouch first (the squash comes from the kick below)
            float h = pet.Scale * pet.StageBounce * (trickKind == 0 ? .45f : .7f);
            lift = h * 4 * a * (1 - a);
            float turn = Mathf.PI * 2 * Mathf.SmoothStep(0, 1, a);
            if (trickKind == 1) trickSpin = turn;
            if (trickKind == 2) trickFlip = turn;
            if (a >= 1 && !trickLanded) { trickLanded = true; pet.V += 3.5f; sfx.Hop(); }
            if (trickT > TrickCrouch + air + TrickSettle) trickKind = -1;
            return true;
        }

        // ---- on the trampoline (user request, 4 Oct 2026: the tricks belong here too) ----

        private float bncT, bncP = .8f;
        private int bncKind, bncN;

        /// <summary>
        /// Bouncing on the trampoline: a couple of plain bounces, then a trick every other bounce, a full spin or a
        /// flip at the top of a bigger, longer bounce. The mat dips as it lands.
        /// </summary>
        private void StepTrampoline(Activity A, float dt, float t, ref float lift, ref float extra)
        {
            if (t <= dt * 1.5f) { bncT = 0; bncN = 0; bncKind = 0; bncP = .8f; } // a fresh go
            bncT += dt;
            if (bncT >= bncP)
            {
                bncT -= bncP;
                bncN++;
                bncKind = bncN >= 2 && bncN % 2 == 0 && !reduce ? Random.Range(1, 3) : 0;
                bncP = bncKind == 0 ? .8f : 1.05f;
                pet.V += 2.5f;
                if (A.it != null) A.it.bv = -4; // the frame gives a little as it lands
                sfx.Hop();
            }
            float u = bncT / bncP, hgt = bncKind == 0 ? .5f : .8f;
            lift = hgt * 4 * u * (1 - u);
            float turn = Mathf.PI * 2 * Mathf.SmoothStep(0, 1, Mathf.Clamp01((u - .12f) / .76f));
            if (bncKind == 1) trickSpin = turn;
            else if (bncKind == 2) trickFlip = turn;
            if (u < .08f || u > .92f) extra = .4f; // squashed on the mat
            if (A.it != null && A.it.parts.mat != null)
            {
                float dip = 1 - Mathf.Clamp01(lift / .12f);
                A.it.parts.mat.localPosition = new Vector3(0, .18f - .05f * dip, 0);
            }
        }

        private void StartTrick(int kind)
        {
            trickKind = kind;
            trickT = 0;
            trickLanded = false;
            pet.V += 2.5f; // the crouch and push off
        }
    }
}
