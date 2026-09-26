using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// The shelf: an open bookcase (it used to be a solid block that hid its shelves) holding small things themed
    /// to its style (StyleData.shelf, e.g. a teapot and cups for the Teahouse, candles and potions for Spooky Manor).
    /// </summary>
    public static class ShelfModels
    {
        private const float PI = Mathf.PI;

        public static void Build(Transform g, StyleData s)
        {
            Material W = M(s.pal[0]), A = M(s.pal[1]), S = M(s.pal[2]), T = M(s.pal[3]);
            Node.Mesh(g, RBox(.46f, .8f, .03f, .012f), W, 0, .4f, -.095f);
            foreach (var x in new[] { -.215f, .215f }) Node.Mesh(g, RBox(.03f, .8f, .22f, .012f), W, x, .4f, 0);
            Node.Mesh(g, RBox(.48f, .04f, .24f, .015f), A, 0, .79f, 0);
            Node.Mesh(g, RBox(.46f, .04f, .22f, .015f), W, 0, .02f, 0);
            foreach (var y in new[] { .29f, .55f }) Node.Mesh(g, RBox(.42f, .022f, .2f, .008f), W, 0, y, 0);

            var things = s.shelf != null && s.shelf.Length > 0 ? s.shelf : new[] { "books", "jar", "vase" };
            float[] levels = { .04f, .301f, .561f };
            Material[] cols = { A, S, T };
            int n = 0;
            for (int lv = 0; lv < levels.Length; lv++)
            {
                // Two things per shelf, left and right, cycling through the style's list.
                for (int side = 0; side < 2; side++, n++)
                {
                    var spot = Node.Group(g, "thing", side == 0 ? -.1f : .1f, levels[lv], .01f);
                    spot.localScale = Vector3.one * 1.2f;
                    Thing(spot, things[n % things.Length], cols[(n + lv) % 3], cols[(n + lv + 1) % 3]);
                }
            }
        }

        /// <summary>One small object, sitting on the shelf at the group's origin (about 0.16 wide, up to 0.2 tall).</summary>
        private static void Thing(Transform t, string kind, Material a, Material b)
        {
            switch (kind)
            {
                case "books":
                    for (int i = 0; i < 4; i++)
                    {
                        float h = .14f + (i % 3) * .025f;
                        Node.Mesh(t, RBox(.03f, h, .12f, .006f), i % 2 == 0 ? a : b, -.05f + i * .034f, h / 2, 0).RotZ(i == 3 ? -.2f : 0);
                    }
                    break;
                case "teapot":
                    Node.Mesh(t, Sph(.055f, 12, 9), a, 0, .05f, 0).ScaleY(.85f);
                    Node.Mesh(t, Cyl(.012f, .016f, .06f, 6), a, .06f, .06f, 0).RotZ(-.9f);
                    Node.Mesh(t, Torus(.025f, .007f, 5, 10, PI), b, -.055f, .06f, 0).RotZ(PI / 2);
                    Node.Mesh(t, Sph(.012f, 6, 5), b, 0, .1f, 0);
                    break;
                case "cups":
                    for (int i = 0; i < 2; i++) Node.Mesh(t, Cyl(.025f, .02f, .04f, 10), i == 0 ? a : b, -.03f + i * .06f, .02f, 0);
                    break;
                case "jar":
                    Node.Mesh(t, Cyl(.04f, .04f, .09f, 12), M("#EFE7D8"), 0, .045f, 0);
                    Node.Mesh(t, Cyl(.042f, .042f, .02f, 12), a, 0, .1f, 0);
                    Node.Mesh(t, Cyl(.038f, .038f, .05f, 12), b, 0, .03f, 0);
                    break;
                case "bowl":
                    Node.Mesh(t, Cyl(.06f, .03f, .04f, 14), a, 0, .02f, 0);
                    Node.Mesh(t, Sph(.02f, 6, 5), M("#E8505B"), -.015f, .045f, 0);
                    Node.Mesh(t, Sph(.02f, 6, 5), M("#F2C230"), .018f, .045f, .01f);
                    break;
                case "vase":
                    Node.Mesh(t, Sph(.04f, 10, 8), a, 0, .045f, 0).ScaleY(1.2f);
                    Node.Mesh(t, Cyl(.018f, .022f, .06f, 8), a, 0, .1f, 0);
                    Node.Mesh(t, Sph(.02f, 6, 5), M("#F3A6BD"), -.01f, .15f, 0);
                    Node.Mesh(t, Sph(.018f, 6, 5), M("#FFF1A8"), .015f, .14f, .005f);
                    break;
                case "candle":
                    for (int i = 0; i < 2; i++)
                    {
                        float h = i == 0 ? .1f : .07f, x = -.025f + i * .05f;
                        Node.Mesh(t, Cyl(.018f, .018f, h, 8), M("#F6EFE2"), x, h / 2, 0);
                        Node.Mesh(t, Sph(.01f, 6, 5), M("#FFB14D", "#FF9A3D"), x, h + .012f, 0).ScaleY(1.5f);
                    }
                    break;
                case "lantern":
                    Node.Mesh(t, RBox(.07f, .1f, .07f, .01f), a, 0, .05f, 0);
                    Node.Mesh(t, RBox(.05f, .07f, .072f, .005f), M("#FFD58A", "#FFB14D"), 0, .05f, 0);
                    Node.Mesh(t, Cyl(0, .045f, .04f, 4), b, 0, .12f, 0);
                    break;
                case "basket":
                    Node.Mesh(t, Cyl(.06f, .05f, .07f, 12), M("#C9A36B"), 0, .035f, 0);
                    Node.Mesh(t, Torus(.045f, .006f, 5, 12, PI), M("#A87E4A"), 0, .07f, 0);
                    break;
                case "plant":
                    Node.Mesh(t, Cyl(.035f, .028f, .05f, 10), a, 0, .025f, 0);
                    for (int i = 0; i < 4; i++) Node.Mesh(t, Sph(.025f, 8, 6), M(i % 2 == 0 ? "#6FAE5A" : "#8CC56A"), Mathf.Cos(i * 1.6f) * .018f, .07f + (i % 2) * .02f, Mathf.Sin(i * 1.6f) * .018f);
                    break;
                case "shell":
                    Node.Mesh(t, Sph(.04f, 10, 6), M("#F7D6C8"), -.02f, .02f, 0).Scale(1.2f, .5f, 1);
                    Node.Mesh(t, Cyl(0, .025f, .05f, 8), M("#F2E6D8"), .04f, .025f, 0);
                    break;
                case "bottle":
                    Node.Mesh(t, Cyl(.028f, .028f, .08f, 10), a, 0, .04f, 0);
                    Node.Mesh(t, Cyl(.01f, .02f, .04f, 8), a, 0, .1f, 0);
                    Node.Mesh(t, Cyl(.012f, .012f, .015f, 6), b, 0, .125f, 0);
                    break;
                case "potion":
                    Node.Mesh(t, Sph(.035f, 10, 8), M("#8BD47A", "#4FAF3F"), 0, .035f, 0);
                    Node.Mesh(t, Cyl(.01f, .012f, .04f, 6), M("#EDE6DA"), 0, .08f, 0);
                    Node.Mesh(t, Sph(.012f, 6, 5), M("#8A5D3B"), 0, .105f, 0);
                    break;
                case "globe":
                    Node.Mesh(t, Cyl(.03f, .035f, .015f, 10), b, 0, .008f, 0);
                    Node.Mesh(t, Sph(.045f, 12, 9), M("#6E9C9A"), 0, .065f, 0);
                    Node.Mesh(t, Torus(.052f, .004f, 4, 16), M("#D9B45A"), 0, .065f, 0).RotZ(.4f);
                    break;
                case "cupcake":
                    Node.Mesh(t, Cyl(.035f, .028f, .04f, 10), a, 0, .02f, 0);
                    Node.Mesh(t, Sph(.038f, 10, 8), M("#F7C9D6"), 0, .05f, 0).ScaleY(.8f);
                    Node.Mesh(t, Sph(.01f, 6, 5), M("#E8505B"), 0, .08f, 0);
                    break;
                case "bread":
                    Node.Mesh(t, Sph(.06f, 12, 8), M("#D9A25E"), 0, .03f, 0).Scale(1.3f, .6f, .8f);
                    break;
                case "cube":
                    Node.Mesh(t, RBox(.06f, .06f, .06f, .005f), a, -.02f, .03f, 0);
                    Node.Mesh(t, RBox(.045f, .045f, .045f, .005f), b, .035f, .0225f, .01f);
                    break;
                case "bonsai":
                    Node.Mesh(t, RBox(.09f, .025f, .05f, .008f), a, 0, .012f, 0);
                    Node.Mesh(t, Cyl(.006f, .008f, .05f, 5), M("#6B4A36"), 0, .045f, 0).RotZ(.3f);
                    Node.Mesh(t, Sph(.03f, 8, 6), M("#4F7A4A"), -.01f, .08f, 0).ScaleY(.5f);
                    break;
                default:
                    Node.Mesh(t, Cyl(.04f, .04f, .09f, 12), a, 0, .045f, 0);
                    break;
            }
        }
    }
}
