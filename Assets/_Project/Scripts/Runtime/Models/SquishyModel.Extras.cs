using System.Collections.Generic;
using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Life stages (baby cowlick, elder eyebrows, tint, size, bounce) and prestige accessories.
    /// Accessories are data (game_content.json: slot hat/face/neck, a model "kind" and a colour), so new ones
    /// usually need no code: reuse a kind with a new colour.
    /// </summary>
    public sealed partial class SquishyModel
    {
        public float StageScale = 1, StageBounce = 1;
        private float _eyeW = 1;
        private GameRules.Life _stage = GameRules.Life.Young;
        private Transform _cowlick, _brows, _hat, _face, _neck;
        private Transform _cowSpring;
        private float _cowA, _cowV, _cowB, _cowBV, _prevSquash;

        /// <summary>The cowlick's spring: jolts from squashing and bouncing make it wobble, then settle.</summary>
        private void StepCowlick(float dt, float squash)
        {
            if (_cowSpring == null || !_cowlick.gameObject.activeSelf || dt <= 0) { _prevSquash = squash; return; }
            float jolt = (squash - _prevSquash) / dt;
            _prevSquash = squash;
            _cowV += (-90 * _cowA - 7 * _cowV + jolt * 2.2f) * dt;
            _cowA += _cowV * dt;
            _cowBV += (-70 * _cowB - 6 * _cowBV + jolt * .9f * Mathf.Sin(Time.time * 3)) * dt;
            _cowB += _cowBV * dt;
            Node.Rot(_cowSpring, Mathf.Clamp(_cowA, -.9f, .9f), 0, Mathf.Clamp(_cowB, -.7f, .7f));
        }
        private static readonly Color Pale = Color.white, Faded = Lin("#D8CFC2");

        public GameRules.Life Stage { get { return _stage; } }

        /// <summary>Stage tint: babies are paler, elders softly faded.</summary>
        private Color StageTint(Color c)
        {
            if (_stage == GameRules.Life.Baby) return Color.Lerp(c, Pale, .22f);
            if (_stage == GameRules.Life.Elder) return Color.Lerp(c, Faded, .25f);
            return c;
        }

        public void SetStage(GameRules.Life stage)
        {
            _stage = stage;
            StageScale = stage == GameRules.Life.Baby ? .85f : stage == GameRules.Life.Elder ? .96f : 1f;
            StageBounce = stage == GameRules.Life.Baby ? 1.35f : stage == GameRules.Life.Elder ? .75f : 1f;
            if (_cowlick == null)
            {
                // A little curl on top for babies.
                _cowlick = Node.Group(Body, "cowlick");
                // A proper curl you can see (it was a few pixels wide), on a spring so it bobs as the squishy bounces.
                _cowlick.localPosition = ShapeAt(Vector3.up) + new Vector3(0, -.03f, 0);
                _cowSpring = Node.Group(_cowlick, "spring");
                Node.Mesh(_cowSpring, Cyl(.05f, .075f, .2f, 10), Mat, 0, .09f, 0, shadow: false, receive: true);
                Node.Mesh(_cowSpring, Torus(.14f, .055f, 8, 20, Mathf.PI * 1.5f), Mat, .06f, .3f, 0, shadow: false, receive: true);
                // Soft eyebrows for elders.
                _brows = Node.Group(Body, "brows");
                var browMat = M("#EDE6DA");
                for (int k = 0; k < 2; k++)
                {
                    float sx = k == 0 ? -1 : 1;
                    var p = ShapeAt(new Vector3(sx * .3f, .42f, .86f).normalized);
                    var n = (p - new Vector3(0, -.1f, 0)).normalized;
                    var b = Node.Mesh(_brows, RBox(.16f, .035f, .05f, .015f), browMat, 0, 0, 0, shadow: false);
                    b.localPosition = p + n * .015f;
                    b.localRotation = Quaternion.FromToRotation(Vector3.forward, n) * Quaternion.AngleAxis(sx * -12, Vector3.forward);
                }
            }
            Node.SetLayer(_cowlick, Body.gameObject.layer); // made after the room set its layer: the camera never drew it
            Node.SetLayer(_brows, Body.gameObject.layer);
            _cowlick.gameObject.SetActive(stage == GameRules.Life.Baby);
            _brows.gameObject.SetActive(stage == GameRules.Life.Elder);
            _eyeW = stage == GameRules.Life.Baby ? 1.2f : 1f; // bigger eyes for babies
            ThreeMat.SetOpacity(_blush, stage == GameRules.Life.Elder ? .7f : Fin != null && Fin.tier == "Galaxy" ? .35f : .5f);
            _g = -1;
        }

        /// <summary>Wears the equipped accessories: one hat, one face piece, one neck piece.</summary>
        public void SetCosmetics(CosmeticData hat, CosmeticData face, CosmeticData neck = null)
        {
            foreach (var t in new[] { _hat, _face, _neck }) if (t != null) { t.gameObject.SetActive(false); Node.Destroy(t); } // hidden now: Destroy waits for the frame end
            _springs.Clear();
            ClearHatSkins();
            int springs = _springs.Count;
            _hat = hat != null ? Hat(hat) : null;
            bool springy = _springs.Count > springs;
            _face = face != null ? Face(face) : null;
            _neck = neck != null ? Neck(neck) : null;
            int layer = Pivot.gameObject.layer;
            foreach (var t in new[] { _hat, _face, _neck }) if (t != null) Node.SetLayer(t, layer);
            // Worn things bend with presses and pinches like the body (the springy sprout and bunny ears ride it instead).
            if (_hat != null && !springy) SkinHat(_hat);
            if (_face != null) SkinHat(_face);
            if (_neck != null) SkinHat(_neck);
            if (Fin != null && Fin.clear) FrontParts(true); // accessories draw over a clear body too
        }

        private Transform Face(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            var mat = M(c.color);
            if (c.kind == "eyepatch")
            {
                // A patch over one eye, on a strap that runs up across the forehead and round the head (over the folds).
                var dp = new Vector3(.36f, .1f, .93f).normalized;
                var p = ShapeAt(dp);
                var n = p - new Vector3(0, -.1f, 0);
                n.y *= 1.3f;
                n.Normalize();
                var patch = Node.Mesh(g, Sph(.1f, 24, 16), mat, 0, 0, 0, shadow: false);
                patch.localPosition = p + n * .022f;
                patch.localRotation = Quaternion.FromToRotation(Vector3.forward, n);
                patch.localScale = new Vector3(1.7f, 1.5f, .34f);
                var side = Vector3.Cross(Vector3.up, dp).normalized;
                var upPerp = (Vector3.up - dp * Vector3.Dot(Vector3.up, dp)).normalized;
                var bb = (side * Mathf.Cos(35 * Mathf.Deg2Rad) - upPerp * Mathf.Sin(35 * Mathf.Deg2Rad)).normalized;
                Vector3? prevQ = null;
                for (int s = 0; s <= 72; s++)
                {
                    float t = s / 72f * Mathf.PI * 2;
                    if (t < .2f || t > Mathf.PI * 2 - .2f) { prevQ = null; continue; } // under the patch
                    var dir = (dp * Mathf.Cos(t) + bb * Mathf.Sin(t)).normalized;
                    var q = RidgePoint(dir);
                    q += (q - new Vector3(0, -.1f, 0)).normalized * .016f;
                    if (prevQ.HasValue) Stick(g, prevQ.Value, q, .014f, mat);
                    prevQ = q;
                }
                return g;
            }
            if (c.kind == "moustache")
            {
                var p = ShapeAt(new Vector3(0, .075f, 1).normalized);
                for (int k = 0; k < 2; k++) Node.Mesh(g, Sph(.1f, 10, 8), mat, k == 0 ? -.09f : .09f, p.y, p.z + .02f, shadow: false).Scale(1.35f, .45f, .5f).RotZ(k == 0 ? .25f : -.25f);
                return g;
            }
            if (c.kind == "freckles")
            {
                for (int k = 0; k < 6; k++)
                {
                    float sx = k < 3 ? -1 : 1;
                    var p = ShapeAt(new Vector3(sx * (.46f + (k % 3) * .07f), .02f + (k % 2) * .05f, .86f).normalized);
                    var n = (p - new Vector3(0, -.1f, 0)).normalized;
                    var f = Node.Mesh(g, Circle(.03f, 10), mat, 0, 0, 0, shadow: false);
                    f.localPosition = p + n * .007f;
                    f.localRotation = Quaternion.FromToRotation(Vector3.forward, n);
                }
                return g;
            }
            // Glasses are one rigid, chunky pair worn across the face (user, 26 Sep 2026: cuter, properly sized, not
            // pinned to the eyes): two big frames that wrap slightly round the bun, a curved bridge and little arms.
            float faceY = .1f, lensX = .36f, lensR = c.kind == "thick" ? .21f : .2f, tube = c.kind == "thick" ? .038f : .026f;
            var tint = ThreeMat.Basic(ThreeMat.Lin(c.kind == "shades" ? "#2A2D45" : "#E8F4FF"), c.kind == "shades" ? .92f : .22f, ThreeMat.Blend.Alpha, depthWrite: false);
            var lensCentres = new Vector3[2];
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1 : 1;
                if (c.kind == "monocle" && k == 0) continue;
                var p = ShapeAt(new Vector3(sx * lensX, faceY, .93f).normalized);
                var n = (p - new Vector3(0, -.1f, 0)).normalized;
                var lens = Node.Group(g, "lens");
                lens.localPosition = p + n * .07f;
                // Face mostly forward, turned a little with the bun's curve.
                lens.localRotation = Quaternion.FromToRotation(Vector3.forward, Vector3.Lerp(Vector3.forward, new Vector3(n.x, 0, n.z).normalized, .45f).normalized);
                lensCentres[k] = lens.localPosition;
                Outline(lens, LensShape(c.kind, lensR), tube, mat);
                if (c.kind == "round" || c.kind == "thick" || c.kind == "shades" || c.kind == "monocle")
                    Node.Mesh(lens, Circle(lensR * .96f, 24), tint, 0, 0, -.002f, shadow: false);
                // An arm back along the side of the head.
                var arm0 = new Vector3(sx * lensR, lensR * .25f, 0);
                var armGo = Node.Mesh(lens, Cyl(tube * .7f, tube * .7f, .22f, 6), mat, arm0.x + sx * .02f, arm0.y, -.1f, shadow: false);
                armGo.localRotation = Quaternion.AngleAxis(90, Vector3.right) * Quaternion.AngleAxis(sx * -20, Vector3.forward);
                if (c.kind == "monocle")
                {
                    // A little chain hanging from the monocle.
                    Vector3 prev = new Vector3(-lensR * .7f, -lensR * .7f, 0);
                    for (int s = 1; s <= 6; s++)
                    {
                        var q = new Vector3(-lensR * .7f - s * .02f, -lensR * .7f - s * .045f - Mathf.Sin(s / 6f * Mathf.PI) * .02f, -.01f * s);
                        var link = Node.Mesh(lens, Sph(.012f, 6, 5), M("#D9B45A"), q.x, q.y, q.z, shadow: false);
                        prev = q;
                    }
                    return g;
                }
            }
            // A cute arched bridge between the frames.
            Vector3 l0 = lensCentres[0] + new Vector3(lensR * .92f, lensR * .15f, 0), l1 = lensCentres[1] - new Vector3(lensR * .92f, -lensR * .15f, 0);
            var mid = (l0 + l1) / 2 + new Vector3(0, .035f, .03f);
            Vector3 last = l0;
            for (int s = 1; s <= 8; s++)
            {
                float u = s / 8f;
                var q = (1 - u) * (1 - u) * l0 + 2 * (1 - u) * u * mid + u * u * l1;
                var seg = Node.Mesh(g, Cyl(tube * .8f, tube * .8f, Vector3.Distance(last, q) + .004f, 6), mat, (last.x + q.x) / 2, (last.y + q.y) / 2, (last.z + q.z) / 2, shadow: false);
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, q - last);
                last = q;
            }
            return g;
        }

        /// <summary>The outline of one frame in its own plane (x right, y up), by glasses style.</summary>
        private static List<Vector2> LensShape(string kind, float r)
        {
            var pts = new List<Vector2>();
            if (kind == "heart")
            {
                for (int i = 0; i < 28; i++)
                {
                    float t = i / 28f * Mathf.PI * 2;
                    float x = 16 * Mathf.Pow(Mathf.Sin(t), 3), y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                    pts.Add(new Vector2(x, y + 2.5f) * (r / 15.5f));
                }
            }
            else if (kind == "star")
            {
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2 + i * Mathf.PI / 5, rr = i % 2 == 0 ? r * 1.12f : r * .55f;
                    pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
                }
            }
            else if (kind == "square")
            {
                float h = r * .9f, cr = r * .35f;
                for (int q = 0; q < 4; q++)
                {
                    float cx = q == 0 || q == 3 ? h - cr : -(h - cr), cy = q < 2 ? h - cr : -(h - cr), a0 = q * Mathf.PI / 2;
                    for (int s = 0; s <= 4; s++) { float a = a0 + s / 4f * Mathf.PI / 2; pts.Add(new Vector2(cx + Mathf.Cos(a) * cr, cy + Mathf.Sin(a) * cr)); }
                }
            }
            else for (int i = 0; i < 28; i++) { float a = i / 28f * Mathf.PI * 2; pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            return pts;
        }

        /// <summary>A chunky closed frame: rounded tube segments with ball joints.</summary>
        private static void Outline(Transform parent, List<Vector2> pts, float tube, Material mat)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
                var seg = Node.Mesh(parent, Cyl(tube, tube, Vector3.Distance(a, b), 6), mat, (a.x + b.x) / 2, (a.y + b.y) / 2, 0, shadow: false);
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
                Node.Mesh(parent, Sph(tube, 6, 5), mat, a.x, a.y, 0, shadow: false);
            }
        }

        /// <summary>Neck pieces sit in a band just below the face.</summary>
        private Transform Neck(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            var mat = M(c.color);
            float r = ShapeAt(new Vector3(1, -.16f, 0).normalized).x;
            var front = ShapeAt(new Vector3(0, -.16f, 1).normalized);
            switch (c.kind)
            {
                case "scarf":
                {
                    // A soft knitted wrap, knotted at the front to one side, with two fringed ends hanging out over the body.
                    Node.Mesh(g, Torus(r * .99f, .075f, 10, 48), mat, 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2).Scale(1, 1, 1.25f);
                    float a = .5f;
                    var outward = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    var knot = Node.Group(g, "knot", outward.x * r * 1.06f, front.y - .01f, outward.z * r * 1.06f);
                    knot.localRotation = Quaternion.LookRotation(outward) * Quaternion.AngleAxis(-18, Vector3.right);
                    knot.localScale = Vector3.one * 1.4f;
                    Node.Mesh(knot, Sph(.085f, 14, 10), mat, 0, 0, .02f, shadow: false).Scale(1.1f, 1, .8f);
                    var fringe = M(Shade(c.color, .85f));
                    foreach (var tail in new[] { new Vector3(-.05f, -.02f, .12f), new Vector3(.06f, .03f, -.3f) }) // (x, z, tilt)
                    {
                        var tg = Node.Group(knot, "tail", tail.x, -.03f, tail.y);
                        tg.RotZ(tail.z);
                        Node.Mesh(tg, RBox(.13f, .26f, .045f, .02f), mat, 0, -.13f, 0, shadow: false);
                        for (int k = 0; k < 4; k++) Node.Mesh(tg, Cyl(.011f, .011f, .06f, 6), fringe, -.045f + k * .03f, -.29f, 0, shadow: false);
                    }
                    break;
                }
                case "bowtie":
                    for (int k = 0; k < 2; k++) Node.Mesh(g, Cyl(.015f, .13f, .2f, 4), mat, k == 0 ? -.1f : .1f, front.y, front.z + .06f, shadow: false).RotZ(k == 0 ? -Mathf.PI / 2 : Mathf.PI / 2);
                    Node.Mesh(g, Sph(.06f, 10, 8), mat, 0, front.y, front.z + .09f, shadow: false);
                    break;
                case "pearls":
                    for (int i = 0; i < 52; i++)
                    {
                        float a = i / 52f * Mathf.PI * 2;
                        Node.Mesh(g, Sph(.052f, 10, 8), mat, Mathf.Cos(a) * r * 1.01f, front.y - .02f, Mathf.Sin(a) * r * 1.01f, shadow: false);
                    }
                    break;
                case "bandana":
                {
                    Node.Mesh(g, Torus(r * 1.0f, .045f, 8, 36), mat, 0, front.y + .01f, 0, shadow: false).RotX(Mathf.PI / 2).Scale(1, 1, 1.7f);
                    Node.Mesh(g, Kerchief(), mat, 0, 0, 0, shadow: false);
                    var dot = M("#FFF7EC");
                    foreach (var d in new[] { new Vector2(.12f, -.55f), new Vector2(.12f, 0), new Vector2(.12f, .55f), new Vector2(.4f, -.3f), new Vector2(.4f, .3f), new Vector2(.68f, 0), new Vector2(.14f, -.95f), new Vector2(.14f, .95f) })
                    {
                        var p = KerchiefPoint(d.x, d.y, .043f, out var n);
                        var f = Node.Mesh(g, Circle(.028f, 10), dot, 0, 0, 0, shadow: false);
                        f.localPosition = p;
                        f.localRotation = Quaternion.FromToRotation(Vector3.forward, n);
                    }
                    // The knot at the back, with two short ends.
                    var back = ShapeAt(new Vector3(0, -.16f, -1).normalized);
                    Node.Mesh(g, Sph(.065f, 10, 8), mat, 0, back.y + .01f, back.z - .06f, shadow: false);
                    for (int k = 0; k < 2; k++) Node.Mesh(g, RBox(.07f, .15f, .025f, .012f), mat, k == 0 ? -.05f : .05f, back.y - .07f, back.z - .07f, shadow: false).RotZ(k == 0 ? -.4f : .4f);
                    break;
                }
                case "tie": // user favourite (the old bandana look, 1 Oct 2026): a band with a pointed blade hanging at the front
                    Node.Mesh(g, Torus(r * .98f, .05f, 8, 40), mat, 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Cyl(.01f, .2f, .22f, 3), mat, 0, front.y - .1f, front.z - .02f, shadow: false).RotX(-Mathf.PI / 2 + .3f);
                    break;
                case "bell":
                    Node.Mesh(g, Torus(r * .99f, .055f, 8, 36), M("#C8412F"), 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.1f, 12, 10), mat, 0, front.y - .1f, front.z + .08f, shadow: false);
                    Node.Mesh(g, Cyl(.012f, .012f, .08f, 6), M("#6B4A1E"), 0, front.y - .14f, front.z + .17f, shadow: false).RotZ(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.02f, 6, 5), M("#6B4A1E"), 0, front.y - .17f, front.z + .16f, shadow: false);
                    break;
                default: // lei
                    for (int i = 0; i < 22; i++)
                    {
                        // Little five-petal flowers round the neck, each facing outwards.
                        float a = i / 22f * Mathf.PI * 2;
                        var fl = Node.Group(g, "flower", Mathf.Cos(a) * r * 1.02f, front.y - .02f, Mathf.Sin(a) * r * 1.02f);
                        fl.localRotation = Quaternion.FromToRotation(Vector3.forward, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                        var pm = M(i % 3 == 0 ? "#F3A6BD" : i % 3 == 1 ? c.color : "#FFF1A8");
                        for (int k = 0; k < 5; k++) { float pa = k * Mathf.PI * 2 / 5; Node.Mesh(fl, Sph(.052f, 8, 6), pm, Mathf.Cos(pa) * .058f, Mathf.Sin(pa) * .058f, 0, shadow: false).Scale(1, 1, .5f); }
                        Node.Mesh(fl, Sph(.034f, 8, 6), M("#F2C230"), 0, 0, .015f, shadow: false);
                    }
                    break;
            }
            return g;
        }

        /// <summary>A point on the outside of the folds (the ridges) in a direction, so straps bridge the pleats.</summary>
        private static Vector3 RidgePoint(Vector3 dir)
        {
            dir.Normalize();
            float hz = Mathf.Sqrt(Mathf.Max(0, 1 - dir.y * dir.y));
            if (hz < 1e-3f) return ShapeAt(dir);
            float th = Mathf.PI / 10 - Twist(dir.y);
            var r = ShapeAt(new Vector3(hz * Mathf.Cos(th), dir.y, hz * Mathf.Sin(th)));
            float R = new Vector2(r.x, r.z).magnitude;
            return new Vector3(dir.x / hz * R, r.y, dir.z / hz * R);
        }

        /// <summary>
        /// A point on the bandana's kerchief: t runs from the band (0) down to the point (1), u across (-1 to 1).
        /// It lies on the body, lifted a little, and narrows to the point.
        /// </summary>
        private Vector3 KerchiefPoint(float t, float u, float lift, out Vector3 n)
        {
            float half = .95f * (1 - t), yd = -.16f - .38f * t;
            float th = u * half;
            var p = ShapeAt(new Vector3(Mathf.Sin(th), yd, Mathf.Cos(th)).normalized);
            n = (p - new Vector3(0, -.1f, 0)).normalized;
            return p + n * lift;
        }

        /// <summary>The bandana's folded triangle, both sides (built for this body's shape).</summary>
        private Mesh Kerchief()
        {
            const int R = 8, Cc = 16;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = verts.Count;
                for (int i = 0; i <= R; i++)
                    for (int j = 0; j <= Cc; j++)
                        verts.Add(KerchiefPoint(i / (float)R, j / (float)Cc * 2 - 1, .035f, out _));
                for (int i = 0; i < R; i++)
                    for (int j = 0; j < Cc; j++)
                    {
                        int a = start + i * (Cc + 1) + j, b = a + 1, c2 = a + Cc + 1, d = c2 + 1;
                        if (side == 0) { tris.Add(a); tris.Add(c2); tris.Add(b); tris.Add(b); tris.Add(c2); tris.Add(d); }
                        else { tris.Add(a); tris.Add(b); tris.Add(c2); tris.Add(b); tris.Add(d); tris.Add(c2); }
                    }
            }
            var m = new Mesh { name = "kerchief" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
