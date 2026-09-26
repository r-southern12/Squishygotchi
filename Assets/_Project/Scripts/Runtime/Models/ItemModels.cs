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
                    var pot = Node.Group(g, "pot", 0, h + .05f, 0);
                    Node.Mesh(pot, Sph(.08f), A, 0, .07f, 0).ScaleY(.85f);
                    Node.Mesh(pot, Cyl(.015f, .02f, .08f, 6), A, .09f, .08f, 0).RotZ(-.9f);
                    Node.Mesh(pot, Sph(.035f, 8, 6), T, 0, .14f, 0);
                    p.pot = pot;
                    Node.Mesh(g, Cyl(.03f, .026f, .04f, 8), L, -.15f, h + .07f, .1f);
                    Node.Mesh(g, Cyl(.03f, .026f, .04f, 8), L, .14f, h + .07f, -.12f);
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
                    Node.Mesh(g, RBox(.56f, .24f, .44f, .09f), L, 0, .14f, 0);
                    Node.Mesh(g, RBox(.58f, .07f, .46f, .03f), A, 0, .08f, 0);
                    p.water = Node.Mesh(g, RBox(.46f, .03f, .34f, .015f), M("#8CC3D1", "#5FA7BB"), 0, .24f, 0, shadow: false);
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
                    Node.Mesh(g, Cyl(.15f, .12f, .22f, 12), A, 0, .11f, 0);
                    Node.Mesh(g, Cyl(.155f, .155f, .04f, 12), T, 0, .2f, 0);
                    Node.Mesh(g, Cyl(.128f, .128f, .012f, 12), M("#5A3E2B"), 0, .221f, 0); // soil
                    var top = Node.Group(g, "top", 0, .22f, 0);
                    PlantModels.Build(top, s.plant); // each style grows a plant from its part of the world
                    p.top = top;
                    break;
                }
                case "shelf":
                    Node.Mesh(g, RBox(.46f, .8f, .22f, .04f), W, 0, .4f, 0);
                    Node.Mesh(g, RBox(.4f, .02f, .18f, .01f), L, 0, .3f, .02f);
                    Node.Mesh(g, RBox(.4f, .02f, .18f, .01f), L, 0, .56f, .02f);
                    Node.Mesh(g, Cyl(.05f, .05f, .12f, 8), A, -.12f, .38f, .03f);
                    Node.Mesh(g, Cyl(.05f, .05f, .12f, 8), S, .1f, .38f, .03f);
                    Node.Mesh(g, Cyl(.05f, .05f, .12f, 8), T, -.08f, .64f, .03f);
                    Node.Mesh(g, Cyl(.05f, .05f, .12f, 8), A, .12f, .64f, .03f);
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
                    Node.Mesh(pom, Sph(.065f, 12, 10), P);
                    Node.Mesh(pom, Sph(.03f, 8, 6), A, .04f, .03f, .03f);
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
