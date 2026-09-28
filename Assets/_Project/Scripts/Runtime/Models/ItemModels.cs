using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>Named parts of an item that animate (the prototype's `parts`).</summary>
    public sealed class ItemParts
    {
        public Transform pot, pan, door, water, top, ball, mat, rack, curtain, shade, pom, wand;
        public Transform[] bars;
        public Transform[] cups; // tea table: a cup and saucer for each seat
        public Transform spout; // tea table: the tip of the teapot's spout (where the tea pours from)
        public float potY;
        public Material shadeMat;
        public Mesh curtainMesh;
        public Vector3[] curtainBase, curtainWork;
        public float open = 1, openT = 1;
        public float topWiltX, topSwayZ; // plant top rotation pieces
    }

    /// <summary>The 19 item types (and the tombstone) built exactly as the prototype's ARCH[].build.</summary>
    public static class ItemModels
    {
        private const float PI = Mathf.PI;

        /// <summary>
        /// A proper little teapot (user feedback, 29 Sep 2026): a round body on a foot ring with a painted band, a lid
        /// with a knob, a curved tapering spout and a loop handle. Returns the spout tip. Sits on y = 0, spout towards +x.
        /// </summary>
        public static Transform Teapot(Transform pot, Material body, Material trim)
        {
            Node.Mesh(pot, Cyl(.05f, .056f, .014f, 20), trim, 0, .007f, 0);
            Node.Mesh(pot, Sph(.08f, 22, 14), body, 0, .066f, 0).localScale = new Vector3(1, .8f, 1);
            Node.Mesh(pot, Torus(.0795f, .005f, 6, 32), trim, 0, .068f, 0).RotX(Mathf.PI / 2);
            Node.Mesh(pot, Cyl(.05f, .054f, .012f, 20), trim, 0, .126f, 0);
            Node.Mesh(pot, Sph(.047f, 18, 10), body, 0, .13f, 0).localScale = new Vector3(1, .45f, 1);
            Node.Mesh(pot, Cyl(.008f, .012f, .012f, 10), trim, 0, .153f, 0);
            Node.Mesh(pot, Sph(.016f, 12, 8), trim, 0, .164f, 0);
            // Spout: tapering segments along a curve from low on the body up and out.
            Vector2 p0 = new Vector2(.062f, .045f), p1 = new Vector2(.12f, .042f), p2 = new Vector2(.162f, .1f); // ends rising at about 55°, so a pour tips it level
            System.Func<float, Vector2> bez = u => (1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * p1 + u * u * p2;
            const int N = 6;
            for (int k = 0; k < N; k++)
            {
                Vector2 a = bez(k / (float)N), b = bez((k + 1) / (float)N), m = (a + b) / 2, d = b - a;
                float ra = Mathf.Lerp(.02f, .009f, k / (float)N), rb = Mathf.Lerp(.02f, .009f, (k + 1) / (float)N);
                Node.Mesh(pot, Cyl(rb, ra, d.magnitude + .004f, 12), body, m.x, m.y, 0).RotZ(Mathf.Atan2(d.y, d.x) - Mathf.PI / 2);
                if (k > 0) Node.Mesh(pot, Sph(ra, 10, 6), body, a.x, a.y, 0);
            }
            Node.Mesh(pot, Torus(.0095f, .0025f, 4, 12), trim, p2.x, p2.y, 0).RotX(Mathf.PI / 2);
            var tip = Node.Group(pot, "spoutTip", p2.x + .004f, p2.y, 0);
            // Handle: a C-shaped loop on the far side, open towards the body, both ends set into it.
            Node.Mesh(pot, Torus(.042f, .009f, 8, 22, Mathf.PI * 1.5f), body, -.094f, .072f, 0).RotZ(Mathf.PI * .25f);
            return tip;
        }

        /// <summary>
        /// The teapot mid-pour towards a cup (cupLocal: the cup's place on the table): turned to face it, tipped by
        /// tilt (0 to 1), and moved so the tipped spout ends right over the middle of the cup, a little above its rim.
        /// Worked out from where the spout tip sits on the pot, so it lines up whatever the cup's distance.
        /// </summary>
        public static void PourPose(ItemParts p, Vector3 cupLocal, float tilt)
        {
            if (p.pot == null || p.spout == null) return;
            float dist = Mathf.Sqrt(cupLocal.x * cupLocal.x + cupLocal.z * cupLocal.z);
            float yaw = Mathf.Atan2(-cupLocal.z, cupLocal.x), th = .9f * tilt; // tipped enough that the spout points level
            Node.Rot(p.pot, 0, yaw, -th);
            var tp = p.spout.localPosition;
            float tipR = tp.x * Mathf.Cos(th) + tp.y * Mathf.Sin(th), tipY = -tp.x * Mathf.Sin(th) + tp.y * Mathf.Cos(th);
            const float Rim = .05f, Above = .035f;
            float reach = (dist - .004f - tipR) * tilt, lift = Mathf.Max(0, Rim + Above - tipY) * tilt;
            p.pot.localPosition = new Vector3(Mathf.Cos(yaw) * reach, p.potY + lift, -Mathf.Sin(yaw) * reach);
        }

        /// <summary>Render checks only: build tea tables with the pot mid-pour (-1 = off).</summary>
        public static float PreviewPour = -1;

        /// <summary>A teacup on its saucer with tea in it and a little handle.</summary>
        public static Transform TeaCup(Transform parent, Material china, Material trim, float x, float y, float z)
        {
            var c = Node.Group(parent, "cup", x, y, z);
            Node.Mesh(c, Cyl(.044f, .038f, .008f, 20), china, 0, .004f, 0);
            Node.Mesh(c, Torus(.041f, .003f, 4, 24), trim, 0, .008f, 0).RotX(Mathf.PI / 2);
            Node.Mesh(c, Cyl(.031f, .022f, .042f, 18), china, 0, .03f, 0);
            Node.Mesh(c, Torus(.031f, .003f, 4, 22), trim, 0, .05f, 0).RotX(Mathf.PI / 2);
            Node.Mesh(c, Cyl(.027f, .027f, .004f, 18), M("#A8683A"), 0, .046f, 0, shadow: false);
            Node.Mesh(c, Torus(.012f, .0045f, 6, 12, Mathf.PI * 1.3f), china, .034f, .03f, 0).RotZ(Mathf.PI * 1.35f);
            return c;
        }

        /// <summary>
        /// A fluffy yarn pom-pom: a soft core covered in small tufts, in the style's yarn colours, with a tie on top.
        /// (It was one patterned sphere, which read as a golf ball.)
        /// </summary>
        public static void Pompom(Transform pom, StyleData s, float r)
        {
            var main = M(s.pal[1]);
            var mix = M(s.pal[4]);
            Node.Mesh(pom, Sph(r * .9f, 14, 10), main);
            const int N = 120; // many fine tufts: fuzzy, not lumpy
            var rnd = new System.Random(s.id.GetHashCode());
            for (int i = 0; i < N; i++)
            {
                // Fibonacci sphere: tufts spread evenly all round, each a little different in size.
                float y = 1 - 2 * (i + .5f) / N, rr = Mathf.Sqrt(1 - y * y), a = i * 2.39996f;
                var d = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                float tuft = r * (.13f + .06f * (float)rnd.NextDouble()), out1 = r * (.92f + .06f * (float)rnd.NextDouble());
                var t = Node.Mesh(pom, Sph(tuft, 6, 4), rnd.NextDouble() < .15 ? mix : main, d.x * out1, d.y * out1, d.z * out1, shadow: false);
                t.localRotation = Quaternion.FromToRotation(Vector3.up, d);
                t.localScale = new Vector3(1, 1.7f, 1); // yarn ends poke outward
            }
            Node.Mesh(pom, Cyl(r * .18f, r * .22f, r * .3f, 8), mix, 0, r * .95f, 0); // the tie where the string meets it
        }

        public static Transform Build(GameContent content, string arch, string styleId, Transform parent, ItemParts p)
        {
            var s = content.Style(styleId) ?? content.Style("minimal");
            var g = Node.Group(parent, arch + ":" + styleId);
            Material W = M(s.pal[0]), A = M(s.pal[1]), S = M(s.pal[2]), T = M(s.pal[3]), L = M(s.pal[4]), P = Pattern(s);
            switch (arch)
            {
                case "bed":
                {
                    float rr = .04f + (s.round ? .03f : 0f), top = s.low ? .1f : .18f;
                    Node.Mesh(g, RBox(.78f, s.low ? .1f : .18f, .56f, rr), W, 0, s.low ? .05f : .09f, 0);
                    Node.Mesh(g, RBox(.72f, .1f, .5f, rr), L, 0, top + .05f, 0);
                    Node.Mesh(g, RBox(.46f, .05f, .52f, .02f), P, .12f, top + .12f, 0);
                    Node.Mesh(g, RBox(.2f, .08f, .36f, .04f), S, -.24f, top + .13f, 0);
                    if (!s.low) Node.Mesh(g, RBox(.06f, .34f, .56f, .03f), W, -.39f, .25f, 0);
                    break;
                }
                case "chair":
                    if (s.low) { Node.Mesh(g, RBox(.3f, .08f, .3f, .04f), P, 0, .04f, 0); break; }
                    foreach (var q in new[] { new Vector2(-.1f, -.1f), new Vector2(.1f, -.1f), new Vector2(-.1f, .1f), new Vector2(.1f, .1f) })
                        Node.Mesh(g, Cyl(.02f, .025f, .2f, 6), W, q.x, .1f, q.y);
                    Node.Mesh(g, RBox(.3f, .06f, .3f, .03f), W, 0, .22f, 0);
                    Node.Mesh(g, RBox(.26f, .04f, .26f, .02f), P, 0, .27f, 0);
                    break;
                case "teatable":
                {
                    float h = s.low ? .16f : .3f;
                    Node.Mesh(g, Cyl(.08f, .14f, h, 10), W, 0, h / 2, 0);
                    Node.Mesh(g, Cyl(.34f, .34f, .05f, 22), W, 0, h + .02f, 0);
                    Node.Mesh(g, Cyl(.26f, .26f, .012f, 22), P, 0, h + .05f, 0, shadow: false);
                    var pot = Node.Group(g, "pot", 0, h + .056f, 0);
                    p.spout = Teapot(pot, A, T);
                    p.pot = pot;
                    p.potY = h + .056f;
                    // Cups and saucers: moved in front of whichever seats are nearby (SteamerGame.ArrangeTeaCups).
                    p.cups = new Transform[2];
                    for (int k = 0; k < 2; k++) p.cups[k] = TeaCup(g, L, T, k == 0 ? -.16f : .15f, h + .056f, k == 0 ? .1f : -.12f);
                    if (PreviewPour >= 0) PourPose(p, p.cups[0].localPosition, PreviewPour);
                    break;
                }
                case "stove":
                {
                    Node.Mesh(g, RBox(.6f, .46f, .4f, .05f), L, 0, .23f, 0);
                    Node.Mesh(g, RBox(.5f, .03f, .14f, .01f), M("#3B2A20"), 0, .47f, -.06f);
                    Node.Mesh(g, RBox(.44f, .2f, .03f, .02f), W, 0, .2f, .2f);
                    Node.Mesh(g, Cyl(.03f, .03f, .03f, 8), A, -.15f, .39f, .21f);
                    Node.Mesh(g, Cyl(.03f, .03f, .03f, 8), A, .15f, .39f, .21f);
                    var pan = Node.Group(g, "pan", -.1f, .5f, .05f);
                    Node.Mesh(pan, Cyl(.13f, .11f, .05f, 14), T);
                    Node.Mesh(pan, RBox(.22f, .025f, .04f, .01f), W, .22f, .01f, 0);
                    p.pan = pan;
                    break;
                }
                case "fridge":
                    Node.Mesh(g, RBox(.42f, .8f, .34f, .06f), s.pal[2] == s.pal[4] ? W : L, 0, .4f, 0);
                    p.door = Node.Mesh(g, RBox(.36f, .44f, .02f, .01f), P, 0, .5f, .17f);
                    Node.Mesh(g, RBox(.04f, .18f, .04f, .02f), T, .14f, .5f, .19f);
                    Node.Mesh(g, RBox(.36f, .2f, .02f, .01f), A, 0, .15f, .17f);
                    break;
                case "sink":
                    Node.Mesh(g, RBox(.52f, .38f, .36f, .05f), W, 0, .19f, 0);
                    Node.Mesh(g, RBox(.52f, .05f, .36f, .03f), L, 0, .4f, 0);
                    Node.Mesh(g, RBox(.3f, .02f, .2f, .01f), M("#8CC3D1"), 0, .43f, .02f, shadow: false);
                    Node.Mesh(g, Cyl(.015f, .015f, .14f, 6), T, 0, .49f, -.12f);
                    Node.Mesh(g, Cyl(.015f, .015f, .08f, 6), T, 0, .55f, -.08f).RotX(PI / 2);
                    Node.Mesh(g, RBox(.46f, .2f, .02f, .01f), P, 0, .18f, .18f);
                    break;
                case "tub":
                    // An open tub (it was a solid block, so the water and the squishy were hidden): four soft walls
                    // round a hollow, water just below the rim, foam and a rubber duck.
                    Node.Mesh(g, RBox(.56f, .27f, .07f, .03f), L, 0, .135f, .185f);
                    Node.Mesh(g, RBox(.56f, .27f, .07f, .03f), L, 0, .135f, -.185f);
                    Node.Mesh(g, RBox(.07f, .27f, .36f, .03f), L, .245f, .135f, 0);
                    Node.Mesh(g, RBox(.07f, .27f, .36f, .03f), L, -.245f, .135f, 0);
                    Node.Mesh(g, RBox(.5f, .06f, .38f, .02f), L, 0, .03f, 0);
                    Node.Mesh(g, RBox(.58f, .07f, .46f, .03f), A, 0, .05f, 0);
                    p.water = Node.Mesh(g, RBox(.44f, .02f, .32f, .01f), M("#4FB8D8", "#2E8FB0"), 0, .185f, 0, shadow: false);
                    var foam = M("#FFFFFF");
                    foreach (var q in new[] { new Vector3(-.17f, .2f, -.11f), new Vector3(-.13f, .205f, -.13f), new Vector3(.17f, .2f, .1f), new Vector3(-.18f, .2f, .09f), new Vector3(.15f, .2f, -.12f), new Vector3(.12f, .205f, -.1f) })
                        Node.Mesh(g, Sph(.035f, 8, 6), foam, q.x, q.y, q.z, shadow: false).Scale(1, .55f, 1);
                    var duck = M("#F6D14B");
                    Node.Mesh(g, Sph(.032f, 10, 8), duck, .15f, .215f, .09f, shadow: false).Scale(1.2f, .85f, 1);
                    Node.Mesh(g, Sph(.022f, 10, 8), duck, .17f, .25f, .09f, shadow: false);
                    Node.Mesh(g, Cyl(0, .01f, .025f, 6), M("#F08A3A"), .195f, .248f, .09f, shadow: false).RotZ(-PI / 2);
                    foreach (var q in new[] { new Vector2(-.22f, -.16f), new Vector2(.22f, -.16f), new Vector2(-.22f, .16f), new Vector2(.22f, .16f) })
                        Node.Mesh(g, Sph(.035f, 6, 5), T, q.x, .02f, q.y);
                    break;
                case "shower":
                {
                    Node.Mesh(g, RBox(.56f, .06f, .56f, .03f), L, 0, .03f, 0);
                    Node.Mesh(g, Cyl(.018f, .018f, 1f, 6), T, -.26f, .5f, -.26f);
                    Node.Mesh(g, Cyl(.018f, .018f, 1f, 6), T, .26f, .5f, -.26f);
                    Node.Mesh(g, Cyl(.014f, .014f, .56f, 6), T, 0, .98f, .26f).RotZ(PI / 2);
                    Node.Mesh(g, Cyl(.014f, .014f, .56f, 6), T, 0, .98f, -.26f).RotZ(PI / 2);
                    Node.Mesh(g, Cyl(.014f, .014f, .52f, 6), T, -.26f, .98f, 0).RotX(PI / 2);
                    Node.Mesh(g, Cyl(.014f, .014f, .52f, 6), T, .26f, .98f, 0).RotX(PI / 2);
                    Node.Mesh(g, RBox(.36f, .3f, .03f, .01f), P, 0, .55f, -.27f);
                    Node.Mesh(g, Cyl(.06f, .04f, .04f, 10), T, 0, .9f, -.16f);
                    p.curtainMesh = PlaneDynamic(.54f, .8f, 12, 4, -.4f);
                    p.curtainBase = p.curtainMesh.vertices;
                    p.curtainWork = (Vector3[])p.curtainBase.Clone();
                    p.curtain = Node.Mesh(g, p.curtainMesh, Pattern(s, true), 0, .97f, .27f, shadow: true, receive: false);
                    p.open = 1; p.openT = 1;
                    break;
                }
                case "lamp":
                    Node.Mesh(g, Cyl(.1f, .12f, .05f, 10), T, 0, .025f, 0);
                    Node.Mesh(g, Cyl(.022f, .022f, .7f, 6), T, 0, .37f, 0);
                    p.shadeMat = Lambert(Lin(s.pal[1]), Lin("#FFB65C") * .6f);
                    p.shade = Node.Mesh(g, Cyl(.1f, .17f, .18f, 12), p.shadeMat, 0, .78f, 0);
                    break;
                case "plant":
                {
                    Node.Mesh(g, Cyl(.15f, .12f, .2f, 12), A, 0, .1f, 0); // ends below the rim: coplanar tops flickered
                    Node.Mesh(g, Cyl(.155f, .155f, .04f, 12), T, 0, .2f, 0);
                    Node.Mesh(g, Cyl(.128f, .128f, .012f, 12), M("#5A3E2B"), 0, .221f, 0); // soil
                    var top = Node.Group(g, "top", 0, .22f, 0);
                    PlantModels.Build(top, s.plant); // each style grows a plant from its part of the world
                    p.top = top;
                    break;
                }
                // ---- extra decor (user request, 29 Sep 2026: a bigger room needs more to fill it) ----
                case "vase":
                {
                    // A tall floor vase in the style's pattern, a few stems and soft round blooms.
                    Node.Mesh(g, Sph(.12f, 16, 12), P, 0, .15f, 0).Scale(1, 1.25f, 1);
                    Node.Mesh(g, Cyl(.05f, .07f, .12f, 14), P, 0, .32f, 0);
                    Node.Mesh(g, Torus(.052f, .012f, 6, 16), A, 0, .38f, 0).RotX(PI / 2);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * 1.7f, lean = .22f + .06f * (k % 2);
                        var stem = Node.Group(g, "stem" + k, 0, .36f, 0);
                        Node.Rot(stem, lean * Mathf.Sin(a), 0, lean * Mathf.Cos(a));
                        Node.Mesh(stem, Cyl(.006f, .007f, .22f, 5), M("#6F9A74"), 0, .11f, 0);
                        Node.Mesh(stem, Sph(.045f, 10, 8), k % 2 == 0 ? L : A, 0, .23f, 0);
                        Node.Mesh(stem, Sph(.02f, 8, 6), S, 0, .26f, 0);
                    }
                    break;
                }
                case "lantern":
                {
                    // A standing paper lantern: a soft ribbed globe glowing on a slim pole.
                    Node.Mesh(g, Cyl(.08f, .1f, .03f, 12), T, 0, .015f, 0);
                    Node.Mesh(g, Cyl(.012f, .012f, .5f, 6), T, 0, .26f, 0);
                    var glow = ThreeMat.Basic(ThreeMat.Lin(s.pal[4]));
                    Node.Mesh(g, Sph(.13f, 16, 12), glow, 0, .6f, 0).Scale(1, .9f, 1);
                    for (int k = 0; k < 5; k++) Node.Mesh(g, Torus(.128f * Mathf.Sin(Mathf.PI * (k + 1) / 6f), .006f, 4, 20), A, 0, .6f + .117f * Mathf.Cos(Mathf.PI * (k + 1) / 6f) * .9f, 0).RotX(PI / 2);
                    Node.Mesh(g, Cyl(.04f, .05f, .03f, 10), A, 0, .72f, 0);
                    Node.Mesh(g, Cyl(.05f, .04f, .03f, 10), A, 0, .48f, 0);
                    Node.Mesh(g, Cyl(.004f, .004f, .1f, 4), S, 0, .41f, 0); // the tassel
                    Node.Mesh(g, Sph(.015f, 6, 5), S, 0, .36f, 0);
                    break;
                }
                case "sidetable":
                {
                    // A small round side table with a teacup and a little stack of books.
                    Node.Mesh(g, Cyl(.16f, .16f, .035f, 18), W, 0, .3f, 0);
                    Node.Mesh(g, Cyl(.03f, .035f, .28f, 8), T, 0, .14f, 0);
                    Node.Mesh(g, Cyl(.1f, .11f, .025f, 14), T, 0, .012f, 0);
                    Node.Mesh(g, Cyl(.03f, .025f, .04f, 10), M("#FFF7EC"), .06f, .337f, .03f);
                    Node.Mesh(g, Torus(.014f, .004f, 4, 10), M("#FFF7EC"), .092f, .34f, .03f).RotY(PI / 2);
                    Node.Mesh(g, RBox(.1f, .02f, .07f, .005f), A, -.05f, .328f, -.02f, shadow: false).RotY(.3f);
                    Node.Mesh(g, RBox(.09f, .02f, .065f, .005f), L, -.05f, .348f, -.02f, shadow: false).RotY(-.1f);
                    break;
                }
                case "easel":
                {
                    // A little easel holding a painting in the style's pattern.
                    foreach (var sx in new[] { -1f, 1f }) Node.Mesh(g, Cyl(.01f, .012f, .62f, 6), T, sx * .1f, .3f, .03f).RotZ(sx * .12f);
                    Node.Mesh(g, Cyl(.01f, .012f, .6f, 6), T, 0, .28f, -.1f).RotX(-.3f);
                    Node.Mesh(g, RBox(.26f, .02f, .03f, .008f), T, 0, .2f, .05f);
                    Node.Mesh(g, RBox(.3f, .24f, .03f, .012f), A, 0, .34f, .05f);
                    Node.Mesh(g, RBox(.26f, .2f, .02f, .006f), P, 0, .34f, .066f, shadow: false);
                    break;
                }
                case "pouf":
                {
                    // A round floor pouf: soft, patterned, with a button in the middle.
                    Node.Mesh(g, Sph(.17f, 18, 12), P, 0, .1f, 0).Scale(1, .62f, 1);
                    Node.Mesh(g, Torus(.16f, .018f, 6, 22), A, 0, .08f, 0).RotX(PI / 2);
                    Node.Mesh(g, Sph(.02f, 8, 6), S, 0, .205f, 0).Scale(1, .6f, 1);
                    break;
                }
                case "books":
                {
                    // A stack of books on the floor topped with a tiny potted sprout.
                    string[] bc = { s.pal[0], s.pal[1], s.pal[2], s.pal[3] };
                    float y = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        float h = .045f + .01f * (k % 2);
                        Node.Mesh(g, RBox(.24f - k * .02f, h, .17f - k * .01f, .008f), M(bc[k]), 0, y + h / 2, 0).RotY(k * .18f - .2f);
                        Node.Mesh(g, RBox(.225f - k * .02f, h * .7f, .16f - k * .01f, .004f), M("#FBF4E6"), .006f, y + h / 2, 0, shadow: false).RotY(k * .18f - .2f);
                        y += h;
                    }
                    Node.Mesh(g, Cyl(.035f, .03f, .05f, 10), W, 0, y + .025f, 0);
                    for (int k = 0; k < 3; k++) Node.Mesh(g, Sph(.025f, 8, 6), M("#6F9A74"), Mathf.Cos(k * 2.1f) * .02f, y + .07f, Mathf.Sin(k * 2.1f) * .02f).Scale(.6f, 1.3f, .6f);
                    break;
                }
                case "shelf":
                    ShelfModels.Build(g, s); // open bookcase with things themed to the style
                    break;
                case "wardrobe":
                    Node.Mesh(g, RBox(.52f, .9f, .3f, .05f), W, 0, .45f, 0);
                    Node.Mesh(g, RBox(.23f, .78f, .02f, .01f), P, -.12f, .47f, .15f);
                    Node.Mesh(g, RBox(.23f, .78f, .02f, .01f), P, .12f, .47f, .15f);
                    Node.Mesh(g, RBox(.03f, .1f, .03f, .01f), T, -.02f, .5f, .17f);
                    Node.Mesh(g, RBox(.03f, .1f, .03f, .01f), T, .02f, .5f, .17f);
                    Node.Mesh(g, RBox(.56f, .06f, .34f, .02f), A, 0, .92f, 0);
                    break;
                case "cushion":
                    Node.Mesh(g, RBox(.36f, .12f, .36f, .06f), P, 0, .06f, 0);
                    foreach (var q in new[] { new Vector2(-.17f, -.17f), new Vector2(.17f, -.17f), new Vector2(-.17f, .17f), new Vector2(.17f, .17f) })
                        Node.Mesh(g, Sph(.025f, 6, 5), A, q.x, .06f, q.y);
                    break;
                case "rug":
                    Node.Mesh(g, Cyl(.62f, .62f, .025f, 32), A, 0, .012f, 0, shadow: false, receive: true);
                    Node.Mesh(g, Cyl(.56f, .56f, .03f, 32), P, 0, .016f, 0, shadow: false, receive: true);
                    break;
                case "wall":
                    Node.Mesh(g, RBox(1.1f, .8f, .08f, .02f), P, 0, .42f, 0);
                    Node.Mesh(g, RBox(1.14f, .06f, .12f, .02f), W, 0, .84f, 0);
                    Node.Mesh(g, RBox(1.14f, .05f, .12f, .02f), W, 0, .025f, 0);
                    break;
                case "screen":
                {
                    float[] xs = { -.34f, 0f, .34f };
                    for (int i = 0; i < 3; i++)
                    {
                        var pn = Node.Group(g, "panel", xs[i], 0, i == 1 ? -.05f : 0);
                        pn.RotY(i == 1 ? 0 : (i != 0 ? -.35f : .35f));
                        Node.Mesh(pn, RBox(.34f, .72f, .035f, .012f), P, 0, .4f, 0);
                        Node.Mesh(pn, RBox(.36f, .03f, .05f, .01f), W, 0, .77f, 0);
                        Node.Mesh(pn, RBox(.03f, .76f, .05f, .01f), W, -.17f, .39f, 0);
                    }
                    break;
                }
                case "ball":
                {
                    var b = Node.Group(g, "ball", 0, .1f, 0);
                    Node.Mesh(b, Sph(.1f, 14, 10), M(s.pal[1]));
                    Node.Mesh(b, Torus(.1f, .018f, 6, 18), M(s.pal[4])).RotY(.6f);
                    p.ball = b;
                    break;
                }
                case "pomwand":
                {
                    // A little stand with an arm; the pom-pom hangs from a string until play time.
                    Node.Mesh(g, Cyl(.12f, .14f, .05f, 12), W, 0, .025f, 0);
                    Node.Mesh(g, Cyl(.015f, .018f, .42f, 6), T, 0, .23f, 0);
                    Node.Mesh(g, Cyl(.012f, .012f, .26f, 6), T, .12f, .44f, 0).RotZ(PI / 2);
                    Node.Mesh(g, Cyl(.004f, .004f, .14f, 4), M("#EFE2C9"), .25f, .37f, 0);
                    var pom = Node.Group(g, "pom", .25f, .3f, 0);
                    Pompom(pom, s, .065f);
                    p.pom = pom;
                    break;
                }
                case "bubbles":
                {
                    Node.Mesh(g, Cyl(.1f, .09f, .14f, 14), L, 0, .07f, 0);
                    Node.Mesh(g, Cyl(.102f, .102f, .04f, 14), P, 0, .08f, 0);
                    Node.Mesh(g, Cyl(.085f, .085f, .01f, 14), M("#BFE6F0", "#9FD3E0"), 0, .14f, 0, shadow: false);
                    var wand = Node.Group(g, "wand", .04f, .14f, 0);
                    Node.Mesh(wand, Cyl(.008f, .008f, .26f, 6), T, 0, .13f, 0);
                    Node.Mesh(wand, Torus(.045f, .009f, 6, 16), A, 0, .3f, 0);
                    wand.RotZ(-.35f);
                    p.wand = wand;
                    break;
                }
                case "xylophone":
                {
                    Node.Mesh(g, RBox(.52f, .05f, .26f, .02f), W, 0, .025f, 0);
                    Node.Mesh(g, RBox(.5f, .03f, .03f, .01f), T, 0, .065f, .09f);
                    Node.Mesh(g, RBox(.5f, .03f, .03f, .01f), T, 0, .065f, -.09f);
                    string[] cols = { "#E86A5A", "#F2A03D", "#F2D34C", "#8FC46A", "#5FA7D9", "#9C7BD1" };
                    p.bars = new Transform[6];
                    for (int i = 0; i < 6; i++) p.bars[i] = Node.Mesh(g, RBox(.065f, .022f, .24f - i * .022f, .008f), M(cols[i]), -.2f + i * .08f, .09f, 0);
                    Node.Mesh(g, Cyl(.006f, .006f, .2f, 5), M("#8A5D3B"), .1f, .07f, .18f).RotZ(PI / 2);
                    Node.Mesh(g, Sph(.02f, 8, 6), A, .2f, .07f, .18f);
                    break;
                }
                case "slide":
                {
                    // Ladder at the back, ramp down to the front (+z faces the room centre).
                    Node.Mesh(g, RBox(.26f, .04f, .2f, .02f), W, 0, .45f, -.12f);
                    foreach (var x in new[] { -.11f, .11f })
                    {
                        Node.Mesh(g, Cyl(.014f, .014f, .45f, 6), T, x, .225f, -.2f);
                        Node.Mesh(g, Cyl(.014f, .014f, .45f, 6), T, x, .225f, -.04f);
                    }
                    for (int k = 0; k < 4; k++) Node.Mesh(g, Cyl(.008f, .008f, .22f, 5), T, 0, .09f + k * .1f, -.23f).RotZ(PI / 2);
                    var ramp = Node.Group(g, "ramp", 0, .24f, .15f);
                    ramp.RotX(.87f);
                    Node.Mesh(ramp, RBox(.2f, .025f, .56f, .01f), P);
                    Node.Mesh(ramp, RBox(.02f, .06f, .56f, .01f), A, -.1f, .02f, 0);
                    Node.Mesh(ramp, RBox(.02f, .06f, .56f, .01f), A, .1f, .02f, 0);
                    break;
                }
                case "trampoline":
                    for (int i = 0; i < 6; i++)
                    {
                        float an = i / 6f * PI * 2;
                        Node.Mesh(g, Cyl(.02f, .02f, .18f, 6), T, Mathf.Cos(an) * .3f, .09f, Mathf.Sin(an) * .3f);
                    }
                    Node.Mesh(g, Torus(.33f, .04f, 8, 28), A, 0, .19f, 0, shadow: true, receive: false).RotX(PI / 2);
                    p.mat = Node.Mesh(g, Cyl(.3f, .3f, .015f, 24), M("#3B2A20"), 0, .18f, 0);
                    break;
                case "beanbag":
                    Node.Mesh(g, Sph(.34f, 18, 12), P, 0, .16f, 0).Scale(1, .5f, 1);
                    Node.Mesh(g, Sph(.2f, 12, 8), M(s.pal[2]), -.1f, .3f, 0).Scale(1, .7f, 1);
                    break;
                case "tomb":
                {
                    // Neglect leaves a grey tombstone; a full life leaves a golden keepsake with a star.
                    bool gold = styleId == "gold";
                    Node.Mesh(g, RBox(.26f, .34f, .1f, .1f), M(gold ? "#E2BE5E" : "#A9A39C"), 0, .17f, 0);
                    Node.Mesh(g, RBox(.34f, .05f, .18f, .02f), M(gold ? "#B8913A" : "#8F8983"), 0, .025f, 0);
                    Node.Mesh(g, Sph(.05f, 10, 8), M(gold ? "#FFF1A8" : "#F3A6BD"), 0, .24f, .06f).ScaleY(.7f);
                    if (gold) Node.Mesh(g, Cyl(.07f, .07f, .02f, 5), M("#FFF7EC", "#FFE08A"), 0, .42f, 0).RotX(Mathf.PI / 2);
                    break;
                }
                    break;
            }
            return g;
        }
    }
}
