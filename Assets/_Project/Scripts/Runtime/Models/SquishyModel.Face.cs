using System.Collections.Generic;
using Squishy.Runtime.Three;
using UnityEngine;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// The squishy's changing face (user request, 26 Sep 2026: the mouth is never one fixed "w"). It follows mood
    /// and what is happening: a content :3 or a smile when all is well (drifting between them), a big open grin with
    /// happy ^^ eyes for fun moments, a surprised "o" with squeezed &gt;&lt; eyes while squished, a flat line when
    /// needs are low, a frown when sad, a sleepy little "o", and chomping while it eats.
    /// </summary>
    public sealed partial class SquishyModel
    {
        public enum Mouth { Cat, Smile, Grin, Oh, Flat, Frown, Sleep }
        private enum EyeMode { Beads, Squeeze, Happy }

        /// <summary>Set by the game while the squishy is eating or snacking.</summary>
        public bool Chewing;

        private readonly Dictionary<Mouth, GameObject> _mouths = new Dictionary<Mouth, GameObject>();
        private readonly GameObject[] _squeeze = new GameObject[2], _happy = new GameObject[2];
        private Mouth _shown = Mouth.Cat, _expr, _idle = Mouth.Cat;
        private EyeMode _eyeMode = EyeMode.Beads;
        private float _exprT, _idleT = 4, _faceT;

        /// <summary>Shows an expression for a moment (a grin when playing, a smile after a cuddle...).</summary>
        public void Express(Mouth m, float seconds)
        {
            _expr = m;
            _exprT = seconds;
        }

        /// <summary>A point on the face and the rotation that lays a flat part onto the surface there.</summary>
        private static void FaceFrame(Vector3 dir, float depth, out Vector3 p, out Quaternion q)
        {
            p = ShapeAt(dir.normalized);
            var n = p - new Vector3(0, -.1f, 0);
            n.y *= 1.3f;
            n.Normalize();
            q = Quaternion.FromToRotation(Vector3.forward, n);
            p += n * depth;
        }

        private void BuildFace(Material ink)
        {
            var tongue = ThreeMat.Basic(ThreeMat.Lin("#FF7A8C"));
            var flip = Quaternion.AngleAxis(180, Vector3.forward); // arcs are ∩ by default; flipped they smile
            FaceFrame(new Vector3(0, .03f, 1), -.004f, out var mp, out var mq);
            var mouth = Node.Group(Body, "mouth");
            mouth.localPosition = mp;
            mouth.localRotation = mq;
            Transform Part(Mouth m) { var g = Node.Group(mouth, m.ToString()); _mouths[m] = g.gameObject; return g; }

            var cat = Part(Mouth.Cat); // :3 (a little "w")
            foreach (float sx in new[] { -1f, 1f })
                Node.Mesh(cat, ThreeGeo.Torus(.034f, .011f, 6, 12, Mathf.PI), ink, sx * .034f, 0, 0, shadow: false).localRotation = flip;

            var smile = Part(Mouth.Smile);
            Node.Mesh(smile, ThreeGeo.Torus(.05f, .012f, 6, 16, Mathf.PI), ink, 0, .012f, 0, shadow: false).localRotation = flip;

            var grin = Part(Mouth.Grin); // open, laughing
            Node.Mesh(grin, ThreeGeo.Sph(.065f, 14, 10), ink, 0, -.01f, 0, shadow: false).localScale = new Vector3(1.2f, .85f, .3f);
            Node.Mesh(grin, ThreeGeo.Sph(.038f, 10, 8), tongue, 0, -.032f, .01f, shadow: false).localScale = new Vector3(1.2f, .6f, .3f);

            var oh = Part(Mouth.Oh);
            Node.Mesh(oh, ThreeGeo.Sph(.045f, 12, 10), ink, 0, -.012f, 0, shadow: false).localScale = new Vector3(.9f, 1.15f, .3f);

            var flat = Part(Mouth.Flat);
            Node.Mesh(flat, ThreeGeo.Cyl(.011f, .011f, .07f, 8), ink, 0, -.005f, 0, shadow: false).localRotation = Quaternion.AngleAxis(90, Vector3.forward);

            var frown = Part(Mouth.Frown);
            Node.Mesh(frown, ThreeGeo.Torus(.042f, .011f, 6, 14, Mathf.PI), ink, 0, -.035f, 0, shadow: false);

            var sleep = Part(Mouth.Sleep);
            Node.Mesh(sleep, ThreeGeo.Sph(.017f, 10, 8), ink, 0, -.006f, 0, shadow: false).localScale = new Vector3(1, 1.1f, .35f);

            foreach (var kv in _mouths) kv.Value.SetActive(kv.Key == Mouth.Cat);

            // Alternative eyes: squeezed > < and happy ^ ^.
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1 : 1;
                FaceFrame(new Vector3(sx * .36f, .1f, .93f), -.002f, out var ep, out var eq);
                var sq = Node.Group(Body, "eyeSqueeze");
                sq.localPosition = ep;
                sq.localRotation = eq;
                float tip = -sx * .055f, end = sx * .055f;
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector2 a = new Vector2(tip, 0), b = new Vector2(end, s * .034f), mid = (a + b) / 2, dv = b - a;
                    var arm = Node.Mesh(sq, ThreeGeo.Cyl(.02f, .02f, dv.magnitude, 8), ink, mid.x, mid.y, 0, shadow: false);
                    arm.localRotation = Quaternion.AngleAxis(Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg - 90, Vector3.forward);
                    Node.Mesh(sq, ThreeGeo.Sph(.02f, 8, 6), ink, b.x, b.y, 0, shadow: false);
                }
                Node.Mesh(sq, ThreeGeo.Sph(.02f, 8, 6), ink, tip, 0, 0, shadow: false);
                sq.gameObject.SetActive(false);
                _squeeze[k] = sq.gameObject;

                var hp = Node.Group(Body, "eyeHappy");
                hp.localPosition = ep;
                hp.localRotation = eq;
                Node.Mesh(hp, ThreeGeo.Torus(.07f, .02f, 6, 16, Mathf.PI), ink, 0, -.03f, 0, shadow: false);
                hp.gameObject.SetActive(false);
                _happy[k] = hp.gameObject;
            }
        }

        /// <summary>Picks this frame's mouth and eyes from what is happening and how the squishy feels.</summary>
        private void StepFace(float dt, float squash, bool closed, float droop)
        {
            _faceT += dt;
            _exprT -= dt;
            _idleT -= dt;
            Mouth m;
            var e = EyeMode.Beads;
            if (Held || squash > .5f) { m = Mouth.Oh; e = EyeMode.Squeeze; }
            else if (_exprT > 0) { m = _expr; if (m == Mouth.Grin) e = EyeMode.Happy; }
            else if (Chewing) m = Mathf.Repeat(_faceT * 4.5f, 1) < .5f ? Mouth.Oh : Mouth.Cat;
            else if (droop > .6f || Grey > .3f) m = Mouth.Frown;
            else if (closed) m = Mouth.Sleep;
            else if (droop > .25f) m = Mouth.Flat;
            else
            {
                if (_idleT <= 0)
                {
                    // Contentment drifts between :3 and a smile, with the odd happy grin.
                    _idleT = Random.Range(3f, 7f);
                    float r = Random.value;
                    _idle = r < .5f ? Mouth.Cat : Mouth.Smile;
                    if (r > .88f) Express(Mouth.Grin, .9f);
                }
                m = _idle;
            }
            if (m != _shown)
            {
                foreach (var kv in _mouths) kv.Value.SetActive(kv.Key == m);
                _shown = m;
            }
            if (e != _eyeMode)
            {
                _eyeMode = e;
                for (int k = 0; k < 2; k++)
                {
                    _squeeze[k].SetActive(e == EyeMode.Squeeze);
                    _happy[k].SetActive(e == EyeMode.Happy);
                }
            }
        }
    }
}
