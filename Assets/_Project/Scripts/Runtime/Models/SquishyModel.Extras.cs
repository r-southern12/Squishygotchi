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
                _cowlick.localPosition = ShapeAt(Vector3.up) + new Vector3(0, .02f, 0);
                Node.Mesh(_cowlick, Torus(.07f, .022f, 6, 16, Mathf.PI * 1.5f), Mat, 0, .06f, 0, shadow: false, receive: true).RotY(Mathf.PI / 2);
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
            _hat = hat != null ? Hat(hat) : null;
            _face = face != null ? Face(face) : null;
            _neck = neck != null ? Neck(neck) : null;
            int layer = Pivot.gameObject.layer;
            foreach (var t in new[] { _hat, _face, _neck }) if (t != null) Node.SetLayer(t, layer);
        }

        private const float HatScale = 1.7f;

        private Transform Hat(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            g.localPosition = ShapeAt(Vector3.up) + new Vector3(0, -.04f, 0);
            Material col = M(c.color), cream = M("#FFF7EC"), gold = M("#D9B45A"), green = M("#7DBA5E");
            switch (c.kind)
            {
                case "cone":
                    Node.Mesh(g, Cyl(.01f, .22f, .42f, 16), col, 0, .2f, 0, shadow: false);
                    Node.Mesh(g, Sph(.06f, 10, 8), cream, 0, .43f, 0, shadow: false);
                    g.RotZ(-.18f);
                    break;
                case "flowers":
                    Node.Mesh(g, Torus(.3f, .035f, 6, 24), M("#8FAE7E"), 0, .02f, 0, shadow: false).RotX(Mathf.PI / 2);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2;
                        Node.Mesh(g, Sph(.06f, 8, 6), i % 2 == 0 ? col : M("#FFF1A8"), Mathf.Cos(a) * .3f, .05f, Mathf.Sin(a) * .3f, shadow: false);
                    }
                    break;
                case "beret":
                    Node.Mesh(g, Sph(.34f, 16, 8), col, .05f, .02f, 0, shadow: false).Scale(1, .28f, 1);
                    Node.Mesh(g, Cyl(.02f, .02f, .06f, 6), col, .05f, .1f, 0, shadow: false);
                    g.RotZ(-.2f);
                    break;
                case "chef":
                    Node.Mesh(g, Cyl(.2f, .2f, .16f, 16), col, 0, .06f, 0, shadow: false);
                    Node.Mesh(g, Sph(.26f, 14, 10), col, 0, .26f, 0, shadow: false).Scale(1, .75f, 1);
                    break;
                case "bow":
                    Node.Mesh(g, Sph(.1f, 10, 8), col, -.1f, .05f, .12f, shadow: false).Scale(1.2f, .8f, .6f);
                    Node.Mesh(g, Sph(.1f, 10, 8), col, .1f, .05f, .12f, shadow: false).Scale(1.2f, .8f, .6f);
                    Node.Mesh(g, Sph(.045f, 8, 6), cream, 0, .05f, .14f, shadow: false);
                    break;
                case "beanie":
                    Node.Mesh(g, Sph(.44f, 18, 12), col, 0, -.1f, 0, shadow: false).Scale(1, .62f, 1);
                    Node.Mesh(g, Torus(.42f, .065f, 8, 28), col, 0, -.2f, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.11f, 12, 9), cream, 0, .2f, 0, shadow: false);
                    break;
                case "tophat":
                    Node.Mesh(g, Cyl(.34f, .34f, .03f, 20), col, 0, 0, 0, shadow: false);
                    Node.Mesh(g, Cyl(.2f, .2f, .36f, 18), col, 0, .19f, 0, shadow: false);
                    Node.Mesh(g, Cyl(.205f, .205f, .06f, 18), M("#C8674E"), 0, .06f, 0, shadow: false);
                    break;
                case "sunhat":
                    Node.Mesh(g, Cyl(.46f, .5f, .03f, 24), col, 0, 0, 0, shadow: false);
                    Node.Mesh(g, Sph(.22f, 14, 10), col, 0, .06f, 0, shadow: false).Scale(1, .7f, 1);
                    Node.Mesh(g, Cyl(.225f, .225f, .05f, 18), M("#E86A92"), 0, .05f, 0, shadow: false);
                    break;
                case "cap":
                    Node.Mesh(g, Sph(.42f, 18, 12), col, 0, -.1f, 0, shadow: false).Scale(1, .58f, 1);
                    Node.Mesh(g, RBox(.42f, .035f, .3f, .015f), col, 0, -.15f, .42f, shadow: false).RotX(-.12f);
                    Node.Mesh(g, Sph(.045f, 8, 6), cream, 0, .15f, 0, shadow: false);
                    break;
                case "ears_cat":
                case "ears_bunny":
                case "ears_bear":
                    for (int k = 0; k < 2; k++)
                    {
                        float sx = k == 0 ? -1 : 1;
                        var e = Node.Group(g, "ear", sx * .27f, -.04f, 0);
                        if (c.kind == "ears_cat")
                        {
                            Node.Mesh(e, Cyl(.015f, .15f, .24f, 4), col, 0, .1f, 0, shadow: false);
                            Node.Mesh(e, Cyl(.01f, .08f, .15f, 4), M("#F3A6BD"), 0, .08f, .045f, shadow: false);
                            e.RotZ(-sx * .3f);
                        }
                        else if (c.kind == "ears_bunny")
                        {
                            Node.Mesh(e, Sph(.09f, 10, 8), col, 0, .25f, 0, shadow: false).Scale(1, 2.8f, .6f);
                            Node.Mesh(e, Sph(.05f, 8, 6), M("#F3A6BD"), 0, .25f, .04f, shadow: false).Scale(1, 3.4f, .4f);
                            e.RotZ(-sx * .2f);
                        }
                        else
                        {
                            Node.Mesh(e, Sph(.14f, 12, 9), col, 0, .09f, 0, shadow: false).Scale(1, 1, .65f);
                            Node.Mesh(e, Sph(.08f, 10, 8), M("#E7C9A8"), 0, .09f, .06f, shadow: false).Scale(1, 1, .45f);
                        }
                    }
                    break;
                case "sprout":
                    Node.Mesh(g, Cyl(.012f, .012f, .16f, 5), green, 0, .08f, 0, shadow: false);
                    Node.Mesh(g, Sph(.07f, 8, 6), col, -.06f, .17f, 0, shadow: false).Scale(1.4f, .5f, .8f).RotZ(.4f);
                    Node.Mesh(g, Sph(.07f, 8, 6), col, .06f, .17f, 0, shadow: false).Scale(1.4f, .5f, .8f).RotZ(-.4f);
                    break;
                case "cherry":
                    Node.Mesh(g, Sph(.07f, 10, 8), col, -.1f, 0, .15f, shadow: false);
                    Node.Mesh(g, Sph(.07f, 10, 8), col, .02f, -.02f, .18f, shadow: false);
                    Node.Mesh(g, Cyl(.008f, .008f, .16f, 4), green, -.04f, .08f, .15f, shadow: false).RotZ(.5f);
                    break;
                case "conical":
                    Node.Mesh(g, Cyl(.02f, .48f, .2f, 20), col, 0, .08f, 0, shadow: false);
                    Node.Mesh(g, Torus(.47f, .02f, 5, 24), M("#A97E47"), 0, -.02f, 0, shadow: false).RotX(Mathf.PI / 2);
                    break;
                case "wizard":
                    Node.Mesh(g, Cyl(.36f, .38f, .03f, 20), col, 0, 0, 0, shadow: false);
                    Node.Mesh(g, Cyl(.01f, .2f, .55f, 16), col, 0, .28f, 0, shadow: false).RotZ(-.15f);
                    Node.Mesh(g, Cyl(.05f, .05f, .01f, 5), gold, .05f, .28f, .15f, shadow: false).RotX(Mathf.PI / 2);
                    break;
                case "pirate":
                    Node.Mesh(g, Sph(.34f, 16, 10), col, 0, .04f, 0, shadow: false).Scale(1.25f, .45f, .7f);
                    Node.Mesh(g, Sph(.05f, 8, 6), cream, 0, .1f, .22f, shadow: false);
                    break;
                case "tiara":
                    for (int i = 0; i <= 6; i++)
                    {
                        float a = (i / 6f - .5f) * 2.1f, x = Mathf.Sin(a) * .34f, z = Mathf.Cos(a) * .34f;
                        Node.Mesh(g, Sph(.04f, 8, 6), gold, x, -.04f, z, shadow: false);
                        if (i < 6)
                        {
                            float a2 = ((i + .5f) / 6f - .5f) * 2.1f;
                            Node.Rot(Node.Mesh(g, Cyl(.03f, .03f, .13f, 6), gold, Mathf.Sin(a2) * .34f, -.04f, Mathf.Cos(a2) * .34f, shadow: false), 0, a2, Mathf.PI / 2); // lies along the arc
                        }
                        float h = i == 3 ? .24f : i % 2 == 1 ? .16f : .1f;
                        Node.Mesh(g, Cyl(0, .05f, h, 6), col, x, -.03f + h / 2, z, shadow: false);
                    }
                    Node.Mesh(g, Sph(.06f, 10, 8), M("#F48FB1", "#F48FB1"), 0, .05f, .36f, shadow: false);
                    break;
                case "halo":
                    Node.Mesh(g, Torus(.26f, .035f, 8, 28), M(c.color, c.color), 0, .3f, -.03f, shadow: false).RotX(Mathf.PI / 2 - .45f);
                    break;
                default: // crown
                    Node.Mesh(g, Cyl(.24f, .24f, .12f, 20), col, 0, .04f, 0, shadow: false);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2;
                        Node.Mesh(g, Cyl(.005f, .06f, .12f, 4), col, Mathf.Cos(a) * .22f, .15f, Mathf.Sin(a) * .22f, shadow: false);
                        Node.Mesh(g, Sph(.025f, 6, 5), M(i % 2 == 0 ? "#E8505B" : "#5FA7D9"), Mathf.Cos(a) * .245f, .05f, Mathf.Sin(a) * .245f, shadow: false);
                    }
                    break;
            }
            g.localScale = Vector3.one * HatScale; // hats were modelled at a quarter of the width: too small on the wide dome
            return g;
        }

        private Transform Face(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            var mat = M(c.color);
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
                    var p = ShapeAt(new Vector3(sx * (.48f + (k % 3) * .05f), .03f + (k % 2) * .04f, .86f).normalized);
                    var n = (p - new Vector3(0, -.1f, 0)).normalized;
                    var f = Node.Mesh(g, Circle(.018f, 8), mat, 0, 0, 0, shadow: false);
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
                    Node.Mesh(g, Torus(r * .98f, .08f, 8, 32), mat, 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, RBox(.14f, .3f, .05f, .03f), mat, .18f, front.y - .15f, front.z + .02f, shadow: false).RotZ(-.15f);
                    break;
                case "bowtie":
                    for (int k = 0; k < 2; k++) Node.Mesh(g, Cyl(.015f, .13f, .2f, 4), mat, k == 0 ? -.1f : .1f, front.y, front.z + .06f, shadow: false).RotZ(k == 0 ? -Mathf.PI / 2 : Mathf.PI / 2);
                    Node.Mesh(g, Sph(.06f, 10, 8), mat, 0, front.y, front.z + .09f, shadow: false);
                    break;
                case "pearls":
                    for (int i = 0; i < 26; i++)
                    {
                        float a = i / 26f * Mathf.PI * 2;
                        Node.Mesh(g, Sph(.052f, 10, 8), mat, Mathf.Cos(a) * r * 1.01f, front.y - .02f, Mathf.Sin(a) * r * 1.01f, shadow: false);
                    }
                    break;
                case "bandana":
                    Node.Mesh(g, Torus(r * .98f, .05f, 6, 32), mat, 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Cyl(.01f, .2f, .22f, 3), mat, 0, front.y - .1f, front.z - .02f, shadow: false).RotX(-Mathf.PI / 2 + .3f);
                    break;
                case "bell":
                    Node.Mesh(g, Torus(r * .99f, .055f, 8, 36), M("#C8412F"), 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.1f, 12, 10), mat, 0, front.y - .1f, front.z + .08f, shadow: false);
                    Node.Mesh(g, Cyl(.012f, .012f, .08f, 6), M("#6B4A1E"), 0, front.y - .14f, front.z + .17f, shadow: false).RotZ(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.02f, 6, 5), M("#6B4A1E"), 0, front.y - .17f, front.z + .16f, shadow: false);
                    break;
                default: // lei
                    for (int i = 0; i < 18; i++)
                    {
                        // Little five-petal flowers round the neck, each facing outwards.
                        float a = i / 18f * Mathf.PI * 2;
                        var fl = Node.Group(g, "flower", Mathf.Cos(a) * r * 1.02f, front.y - .02f, Mathf.Sin(a) * r * 1.02f);
                        fl.localRotation = Quaternion.FromToRotation(Vector3.forward, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                        var pm = M(i % 3 == 0 ? "#F3A6BD" : i % 3 == 1 ? c.color : "#FFF1A8");
                        for (int k = 0; k < 5; k++) { float pa = k * Mathf.PI * 2 / 5; Node.Mesh(fl, Sph(.045f, 8, 6), pm, Mathf.Cos(pa) * .05f, Mathf.Sin(pa) * .05f, 0, shadow: false).Scale(1, 1, .5f); }
                        Node.Mesh(fl, Sph(.03f, 8, 6), M("#F2C230"), 0, 0, .015f, shadow: false);
                    }
                    break;
            }
            return g;
        }
    }
}
