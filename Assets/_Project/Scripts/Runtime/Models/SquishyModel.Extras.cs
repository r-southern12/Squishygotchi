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
                    Node.Mesh(g, Sph(.3f, 16, 10), col, 0, -.02f, 0, shadow: false).Scale(1, .7f, 1);
                    Node.Mesh(g, Torus(.28f, .04f, 6, 24), col, 0, -.1f, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.08f, 10, 8), cream, 0, .2f, 0, shadow: false);
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
                    Node.Mesh(g, Sph(.28f, 16, 10), col, 0, -.02f, 0, shadow: false).Scale(1, .6f, 1);
                    Node.Mesh(g, RBox(.3f, .025f, .22f, .01f), col, 0, -.06f, .3f, shadow: false);
                    Node.Mesh(g, Sph(.03f, 8, 6), cream, 0, .15f, 0, shadow: false);
                    break;
                case "ears_cat":
                case "ears_bunny":
                case "ears_bear":
                    for (int k = 0; k < 2; k++)
                    {
                        float sx = k == 0 ? -1 : 1;
                        var e = Node.Group(g, "ear", sx * .3f, -.12f, 0);
                        if (c.kind == "ears_cat")
                        {
                            Node.Mesh(e, Cyl(.01f, .13f, .22f, 4), col, 0, .1f, 0, shadow: false);
                            e.RotZ(-sx * .35f);
                        }
                        else if (c.kind == "ears_bunny")
                        {
                            Node.Mesh(e, Sph(.09f, 10, 8), col, 0, .25f, 0, shadow: false).Scale(1, 2.8f, .6f);
                            Node.Mesh(e, Sph(.05f, 8, 6), M("#F3A6BD"), 0, .25f, .04f, shadow: false).Scale(1, 3.4f, .4f);
                            e.RotZ(-sx * .2f);
                        }
                        else
                        {
                            Node.Mesh(e, Sph(.11f, 10, 8), col, 0, .06f, 0, shadow: false).Scale(1, 1, .6f);
                            Node.Mesh(e, Sph(.06f, 8, 6), M("#E7C9A8"), 0, .06f, .05f, shadow: false).Scale(1, 1, .4f);
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
                    Node.Mesh(g, Torus(.26f, .02f, 5, 24, Mathf.PI), col, 0, -.05f, .02f, shadow: false).RotX(-.3f);
                    for (int i = 0; i < 5; i++) Node.Mesh(g, Cyl(.005f, .035f, .09f, 4), col, -.16f + i * .08f, .05f + (i == 2 ? .04f : 0), .2f, shadow: false);
                    Node.Mesh(g, Sph(.035f, 8, 6), M("#9BE7FF", "#9BE7FF"), 0, .12f, .21f, shadow: false);
                    break;
                case "halo":
                    Node.Mesh(g, Torus(.26f, .03f, 6, 28), M(c.color, c.color), 0, .22f, 0, shadow: false).RotX(Mathf.PI / 2);
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
            return g;
        }

        private Transform Face(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            var mat = M(c.color);
            if (c.kind == "moustache")
            {
                var p = ShapeAt(new Vector3(0, .1f, 1).normalized);
                for (int k = 0; k < 2; k++) Node.Mesh(g, Sph(.08f, 10, 8), mat, k == 0 ? -.07f : .07f, p.y, p.z + .02f, shadow: false).Scale(1.3f, .45f, .5f).RotZ(k == 0 ? .25f : -.25f);
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
            var centres = new Vector3[2];
            float rimR = c.kind == "star" ? .18f : c.kind == "square" ? .17f : .16f; // lenses clearly bigger than the eyes
            for (int k = 0; k < 2; k++)
            {
                if (c.kind == "monocle" && k == 0) continue;
                var p = ShapeAt(new Vector3((k == 0 ? -1 : 1) * .36f, .1f, .93f).normalized);
                var n = (p - new Vector3(0, -.1f, 0)).normalized;
                Mesh rimMesh = c.kind == "star" ? Torus(.18f, .024f, 4, 5) : c.kind == "heart" ? Torus(.16f, .024f, 4, 6)
                    : c.kind == "square" ? Torus(.17f, .022f, 4, 4) : c.kind == "thick" ? Torus(.16f, .034f, 6, 20) : Torus(.16f, .018f, 6, 20);
                var rim = Node.Mesh(g, rimMesh, mat, 0, 0, 0, shadow: false);
                rim.localPosition = p + n * .065f;
                centres[k] = rim.localPosition;
                float spin = c.kind == "star" ? 18 : c.kind == "square" ? 45 : c.kind == "heart" ? 30 : 0;
                rim.localRotation = Quaternion.FromToRotation(Vector3.forward, n) * Quaternion.AngleAxis(spin, Vector3.forward);
                if (c.kind == "shades")
                {
                    var lens = Node.Mesh(g, Circle(.16f, 16), M("#2A2D45"), 0, 0, 0, shadow: false);
                    lens.localPosition = p + n * .07f;
                    lens.localRotation = Quaternion.FromToRotation(Vector3.forward, n);
                }
                if (c.kind == "monocle")
                {
                    var top = p + n * .05f + new Vector3(.12f, -.02f, 0);
                    Node.Mesh(g, Cyl(.004f, .004f, .3f, 4), mat, top.x + .02f, top.y - .15f, top.z - .05f, shadow: false).RotZ(.2f);
                    return g;
                }
            }
            // The bridge spans rim to rim, curving over the face (a straight bar sank into the bun's bulge).
            Vector3 dir = (centres[1] - centres[0]).normalized, a = centres[0] + dir * rimR * .92f, b = centres[1] - dir * rimR * .92f;
            Vector3 prev = a;
            for (int s = 1; s <= 6; s++)
            {
                var q = Vector3.Lerp(a, b, s / 6f);
                var sd = (q - new Vector3(0, -.1f, 0)).normalized;
                var sp = ShapeAt(new Vector3(sd.x, sd.y, Mathf.Max(.2f, sd.z)).normalized);
                var sn = (sp - new Vector3(0, -.1f, 0)).normalized;
                q = s == 6 ? b : new Vector3(q.x, q.y + .02f * Mathf.Sin(s / 6f * Mathf.PI), Mathf.Max(q.z, (sp + sn * .06f).z));
                var seg = Node.Mesh(g, Cyl(.013f, .013f, Vector3.Distance(prev, q) + .008f, 6), mat, (prev.x + q.x) / 2, (prev.y + q.y) / 2, (prev.z + q.z) / 2, shadow: false);
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, q - prev);
                prev = q;
            }
            return g;
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
                    for (int k = 0; k < 2; k++) Node.Mesh(g, Cyl(.01f, .09f, .14f, 4), mat, k == 0 ? -.07f : .07f, front.y, front.z + .04f, shadow: false).RotZ(k == 0 ? -Mathf.PI / 2 : Mathf.PI / 2);
                    Node.Mesh(g, Sph(.04f, 8, 6), mat, 0, front.y, front.z + .06f, shadow: false);
                    break;
                case "pearls":
                    for (int i = 0; i < 26; i++)
                    {
                        float a = i / 26f * Mathf.PI * 2;
                        Node.Mesh(g, Sph(.035f, 8, 6), mat, Mathf.Cos(a) * r, front.y - .02f, Mathf.Sin(a) * r, shadow: false);
                    }
                    break;
                case "bandana":
                    Node.Mesh(g, Torus(r * .98f, .05f, 6, 32), mat, 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Cyl(.01f, .2f, .22f, 3), mat, 0, front.y - .1f, front.z - .02f, shadow: false).RotX(-Mathf.PI / 2 + .3f);
                    break;
                case "bell":
                    Node.Mesh(g, Torus(r * .98f, .035f, 6, 32), M("#C8412F"), 0, front.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.07f, 10, 8), mat, 0, front.y - .07f, front.z + .04f, shadow: false);
                    break;
                default: // lei
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2;
                        Node.Mesh(g, Sph(.065f, 8, 6), M(i % 3 == 0 ? "#F3A6BD" : i % 3 == 1 ? c.color : "#FFF1A8"), Mathf.Cos(a) * r, front.y - .02f, Mathf.Sin(a) * r, shadow: false);
                    }
                    break;
            }
            return g;
        }
    }
}
