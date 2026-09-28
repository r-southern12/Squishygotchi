using Squishy.Runtime.Three;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Ingredient models with enough detail to read at a glance (user feedback: "tiny cubes and circles"; the prawn
    /// wasn't a prawn). Chunky and soft like the rest of the room. Each ingredient names its shape in the data.
    /// </summary>
    public static partial class KitchenModels
    {
        private static Transform P(Transform g, Mesh m, Material mat, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1)
        {
            var t = Node.Mesh(g, m, mat, x, y, z);
            if (rx != 0 || ry != 0 || rz != 0) Node.Rot(t, rx, ry, rz);
            if (sx != 1 || sy != 1 || sz != 1) t.localScale = new Vector3(sx, sy, sz);
            return t;
        }

        private static bool Detailed(string shape, string hex, Transform g)
        {
            const float Pi = Mathf.PI;
            var col = M(hex);
            switch (shape)
            {
                case "prawn":
                {
                    // A curled prawn lying on its side: tapering shell segments round a C, banded, fanned tail, head, eye, feelers.
                    var shell = M("#F08A6A");
                    var band = M("#FFC3A8");
                    int n = 14; // closely overlapped so the body reads as one smooth curl, banded every other segment
                    for (int k = 0; k < n; k++)
                    {
                        float a = Pi * (.15f + 1.2f * k / (n - 1)), r = .08f, s = Mathf.Lerp(.052f, .024f, k / (float)(n - 1));
                        float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                        P(g, Sph(s, 14, 10), shell, x, s * .9f, z, 0, -a, 0, 1.1f, .92f, .9f);
                        if (k % 2 == 1) P(g, Torus(s * .95f, s * .1f, 4, 16), band, x, s * .9f, z, 0, -a + Pi / 2, 0);
                    }
                    float ta = Pi * 1.42f;
                    for (int k = -1; k <= 1; k++)
                        P(g, Sph(.035f, 10, 8), M("#E8674C"), Mathf.Cos(ta) * .075f + k * .015f, .02f, Mathf.Sin(ta) * .075f - .02f, 0, ta + k * .5f, 0, .45f, .25f, 1.1f);
                    float ha = Pi * .08f, hx = Mathf.Cos(ha) * .09f, hz = Mathf.Sin(ha) * .09f;
                    P(g, Sph(.055f, 12, 10), shell, hx, .05f, hz - .02f, 0, 0, 0, 1, .9f, 1.3f);
                    P(g, Sph(.012f, 8, 6), M("#2A1C18"), hx + .035f, .075f, hz - .045f);
                    P(g, Cyl(.004f, .004f, .22f, 4), shell, hx + .03f, .07f, hz - .15f, Pi / 2 - .25f, .3f, 0);
                    P(g, Cyl(.004f, .004f, .2f, 4), shell, hx - .01f, .07f, hz - .14f, Pi / 2 - .2f, -.2f, 0);
                    for (int k = 0; k < 4; k++) P(g, Cyl(.004f, .003f, .05f, 4), band, Mathf.Cos(Pi * (.35f + k * .15f)) * .045f, .015f, Mathf.Sin(Pi * (.35f + k * .15f)) * .045f, .6f, 0, 0);
                    return true;
                }
                case "flour_sack":
                {
                    // A paper flour sack, top rolled over, a green label, a puff of flour at its feet.
                    P(g, RBox(.2f, .22f, .15f, .04f), M("#F1E4C8"), 0, .11f, 0);
                    P(g, RBox(.21f, .05f, .16f, .02f), M("#E3D0AC"), 0, .235f, 0);
                    P(g, RBox(.14f, .08f, .01f, .01f), M("#6F9A74"), 0, .12f, .078f);
                    P(g, Sph(.022f, 8, 6), M("#F4EFE4"), -.03f, .135f, .085f, 0, 0, 0, 1, 1, .3f);
                    P(g, Sph(.05f, 10, 8), M("#FBF7EE"), .1f, .015f, .08f, 0, 0, 0, 1.4f, .3f, 1);
                    return true;
                }
                case "sugar_cubes":
                {
                    var w = M("#FFFFFF");
                    P(g, RBox(.08f, .08f, .08f, .012f), w, -.045f, .04f, 0, 0, .2f, 0);
                    P(g, RBox(.08f, .08f, .08f, .012f), w, .045f, .04f, .01f, 0, -.15f, 0);
                    P(g, RBox(.08f, .08f, .08f, .012f), w, 0, .12f, 0, 0, .5f, 0);
                    P(g, RBox(.08f, .08f, .08f, .012f), w, .02f, .04f, -.08f, 0, .6f, 0);
                    return true;
                }
                case "cabbage":
                {
                    // Round pale heart, loose outer leaves cupping it, pale veins.
                    P(g, Sph(.12f, 16, 12), M("#C9E0A6"), 0, .12f, 0);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * Pi * 2 / 5, sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                        P(g, Sph(.12f, 12, 10), col, ca * .05f, .1f, sa * .05f, .5f * sa, -a, -.5f * ca, .55f, 1, 1);
                        P(g, Cyl(.006f, .006f, .16f, 4), M("#E6F0D0"), ca * .115f, .11f, sa * .115f, .5f * sa, 0, -.5f * ca);
                    }
                    return true;
                }
                case "chives":
                {
                    // A bunch of long thin stalks tied with a band, a couple of purple flower tufts.
                    var gr = M("#5E9E48");
                    for (int k = 0; k < 9; k++)
                    {
                        float a = k * 2.4f, r = .012f * (k % 3);
                        P(g, Cyl(.006f, .007f, .36f, 5), gr, Mathf.Cos(a) * r, .02f, Mathf.Sin(a) * r, Pi / 2 + .08f * Mathf.Sin(a), .1f * k, 0);
                    }
                    P(g, Torus(.024f, .008f, 5, 12), M("#C8674E"), 0, .02f, .02f);
                    P(g, Sph(.018f, 8, 6), M("#B58BD0"), .01f, .02f, .19f);
                    P(g, Sph(.015f, 8, 6), M("#B58BD0"), -.012f, .025f, .18f);
                    return true;
                }
                case "ginger":
                {
                    // Knobbly branching root with growth rings, one cut end showing pale flesh.
                    var skin = M("#D4A55C");
                    P(g, Sph(.06f, 12, 10), skin, 0, .045f, 0, 0, 0, 0, 1.6f, .75f, 1);
                    P(g, Sph(.045f, 10, 8), skin, .08f, .04f, .04f, 0, .6f, 0, 1.5f, .8f, 1);
                    P(g, Sph(.04f, 10, 8), skin, -.07f, .04f, .05f, 0, -.5f, 0, 1.4f, .8f, 1);
                    P(g, Sph(.035f, 10, 8), skin, .03f, .075f, -.03f, 0, 0, 0, 1, 1.2f, 1);
                    P(g, Cyl(.04f, .04f, .005f, 12), M("#F3DE9A"), .145f, .045f, .065f, 0, 0, Pi / 2 - .3f);
                    for (int k = 0; k < 4; k++) P(g, Torus(.035f, .004f, 4, 12), M("#B9894A"), -.02f + k * .03f, .045f, 0, 0, Pi / 2, 0, 1, .75f, 1);
                    return true;
                }
                case "shiitake":
                {
                    // Brown domed cap with pale cracks, cream gills, pale stem; a second one lying on its side.
                    P(g, Cyl(.028f, .036f, .08f, 10), M("#F1E6D0"), 0, .04f, 0);
                    P(g, Sph(.09f, 16, 10), col, 0, .085f, 0, 0, 0, 0, 1, .55f, 1);
                    P(g, Cyl(.085f, .085f, .01f, 16), M("#E9D9BC"), 0, .08f, 0);
                    for (int k = 0; k < 5; k++) P(g, RBox(.05f, .006f, .012f, .003f), M("#E9D9BC"), Mathf.Cos(k * 1.3f) * .04f, .133f, Mathf.Sin(k * 1.3f) * .04f, .15f, k * 1.3f, 0);
                    // A smaller one standing beside it.
                    P(g, Cyl(.02f, .026f, .05f, 8), M("#F1E6D0"), .12f, .025f, .05f);
                    P(g, Sph(.06f, 14, 10), col, .12f, .06f, .05f, 0, 0, 0, 1, .55f, 1);
                    P(g, Cyl(.057f, .057f, .008f, 14), M("#E9D9BC"), .12f, .056f, .05f);
                    return true;
                }
                case "tofu":
                {
                    // A soft block and a cut cube on a little wooden board, faint press lines.
                    P(g, RBox(.28f, .02f, .2f, .01f), M("#C9955E"), 0, .01f, 0);
                    P(g, RBox(.16f, .1f, .14f, .02f), col, -.04f, .07f, 0);
                    P(g, RBox(.06f, .06f, .06f, .012f), col, .09f, .05f, .03f, 0, .4f, 0);
                    for (int k = 0; k < 3; k++) P(g, RBox(.16f, .004f, .004f, .001f), M("#E8DFC6"), -.04f, .05f + k * .025f, .071f);
                    return true;
                }
                case "eggs":
                {
                    // Two eggs in a straw nest, one cracked open beside them showing the yolk.
                    P(g, Torus(.1f, .03f, 6, 20), M("#D9B26A"), 0, .025f, 0, Pi / 2, 0, 0, 1, 1, .6f);
                    P(g, Sph(.065f, 14, 10), col, -.035f, .08f, 0, 0, 0, .3f, 1, 1.25f, 1);
                    P(g, Sph(.065f, 14, 10), col, .06f, .065f, .03f, 0, 0, -.5f, 1, 1.25f, 1);
                    P(g, Sph(.06f, 14, 10), M("#FFFFFF"), .03f, .03f, -.12f, 0, 0, 0, 1.3f, .35f, 1.1f);
                    P(g, Sph(.03f, 12, 8), M("#F2B233"), .03f, .045f, -.12f, 0, 0, 0, 1, .6f, 1);
                    return true;
                }
                case "sesame_bowl":
                {
                    // A deep little bowl heaped high with black seeds (a tall mound, seeds all over it), a few spilt beside.
                    P(g, Cyl(.09f, .062f, .1f, 16), M("#EFE2C9"), 0, .05f, 0);
                    P(g, Torus(.088f, .008f, 5, 18), M("#E3D0AC"), 0, .1f, 0, Pi / 2, 0, 0);
                    P(g, Sph(.085f, 16, 12), col, 0, .105f, 0, 0, 0, 0, 1, 1.05f, 1);
                    for (int k = 0; k < 40; k++)
                    {
                        float u = (k + .5f) / 40f, ph = k * 2.39996f, cy = 1 - u * .95f, rr = Mathf.Sqrt(1 - cy * cy);
                        P(g, Sph(.01f, 5, 4), M(k % 3 == 0 ? "#5A4B44" : "#2A221F"), Mathf.Cos(ph) * rr * .088f, .105f + cy * .092f, Mathf.Sin(ph) * rr * .088f, 0, ph, 0, 1.6f, .7f, 1);
                    }
                    for (int k = 0; k < 6; k++) P(g, Sph(.009f, 5, 4), col, .13f + .02f * Mathf.Cos(k * 2), .005f, .04f + .025f * Mathf.Sin(k * 2), 0, k, 0, 1.6f, .6f, 1);
                    return true;
                }
                case "pork_belly":
                {
                    // Pork belly: stacked pink meat and cream fat layers under a golden rind, a slice cut off.
                    float y = 0;
                    string[] layer = { "#E8A09A", "#F8EDE6", "#D98580", "#F8EDE6", "#E8A09A" };
                    float[] h = { .035f, .018f, .03f, .015f, .025f };
                    for (int k = 0; k < layer.Length; k++) { P(g, RBox(.24f, h[k] + .004f, .15f, .006f), M(layer[k]), 0, y + h[k] / 2, 0); y += h[k]; }
                    P(g, RBox(.24f, .014f, .15f, .006f), M("#D9A64A"), 0, y + .007f, 0);
                    P(g, RBox(.04f, y, .15f, .006f), M("#E8A09A"), .17f, y / 2, .02f, 0, .3f, 0);
                    return true;
                }
                case "bok_choy":
                {
                    // Pale fat stems fanning up into dark green spoon-shaped leaves.
                    for (int k = 0; k < 5; k++)
                    {
                        var s = Node.Group(g, "stem" + k, 0, .02f, 0);
                        Node.Rot(s, .12f * ((k + 1) % 2), 0, (k - 2) * .2f); // standing up, fanned out
                        P(s, RBox(.05f, .2f, .025f, .012f), M("#EEF4DD"), 0, .1f, 0);
                        P(s, Sph(.07f, 12, 8), col, 0, .25f, 0, 0, 0, 0, .75f, 1.3f, .25f);
                        P(s, Cyl(.004f, .004f, .16f, 4), M("#DDEBC8"), 0, .24f, .018f);
                    }
                    P(g, Cyl(.035f, .03f, .03f, 10), M("#E8F2DA"), 0, .02f, -.02f, Pi / 2, 0, 0);
                    return true;
                }
                case "chilies":
                {
                    // Two glossy curved chili pods with green caps and stalks.
                    for (int c = 0; c < 2; c++)
                    {
                        var p = Node.Group(g, "chili" + c, c * .06f - .03f, .03f, c * .05f);
                        Node.Rot(p, 0, c * .7f, 0);
                        for (int k = 0; k < 16; k++)
                        {
                            float u = k / 15f; // tightly overlapped so the pod reads as one smooth curve
                            P(p, Sph(Mathf.Lerp(.03f, .007f, u * u), 12, 10), c == 0 ? col : M("#B8322A"), u * .2f - .1f, .024f * Mathf.Sin(u * Pi) - .01f * u, .03f * u * u);
                        }
                        P(p, Cyl(.02f, .026f, .018f, 8), M("#4F8A3C"), -.115f, 0, 0, 0, 0, Pi / 2);
                        P(p, Cyl(.005f, .005f, .04f, 5), M("#4F8A3C"), -.14f, .012f, 0, 0, 0, Pi / 2 + .6f);
                    }
                    return true;
                }
                case "red_bean_jar":
                {
                    // A jar of dark red paste with a wooden spoon in it, its lid set aside, a few beans in front.
                    P(g, Cyl(.075f, .075f, .15f, 16), M("#E7E0D6"), 0, .075f, 0);
                    P(g, Cyl(.07f, .07f, .12f, 16), col, 0, .062f, 0);
                    P(g, Cyl(.078f, .078f, .02f, 16), M("#C8674E"), .12f, .01f, .06f);
                    P(g, Cyl(.008f, .008f, .2f, 6), M("#C9955E"), .02f, .17f, 0, 0, 0, -.3f);
                    for (int k = 0; k < 4; k++) P(g, Sph(.016f, 8, 6), M("#7A2E24"), -.09f + k * .025f, .012f, .1f, 0, k, 0, 1.4f, .8f, 1);
                    return true;
                }
                case "lotus_seeds":
                {
                    // A small heap of pale oval seeds, each with a tiny brown tip.
                    for (int k = 0; k < 11; k++)
                    {
                        float a = k * 2.4f, r = .02f + .012f * (k % 4);
                        float sx = Mathf.Cos(a) * r, sy = .028f + (k < 4 ? .035f : 0), sz = Mathf.Sin(a) * r;
                        P(g, Sph(.03f, 10, 8), col, sx, sy, sz, 0, a, 0, 1, .85f, .85f);
                        P(g, Sph(.008f, 6, 5), M("#8A5D3B"), sx + Mathf.Cos(a) * .028f, sy, sz - Mathf.Sin(a) * .028f);
                    }
                    return true;
                }
                case "soy_bottle":
                {
                    // A tall dark glass bottle, cream label with a red mark, red pouring cap.
                    P(g, Cyl(.055f, .06f, .2f, 14), col, 0, .1f, 0);
                    P(g, Cyl(.03f, .05f, .05f, 12), col, 0, .225f, 0);
                    P(g, Cyl(.061f, .061f, .08f, 14), M("#F4EFE4"), 0, .09f, 0);
                    P(g, RBox(.05f, .03f, .004f, .004f), M("#C8674E"), 0, .09f, .062f);
                    P(g, Cyl(.028f, .03f, .035f, 10), M("#C8412F"), 0, .265f, 0);
                    P(g, Cyl(.008f, .012f, .025f, 6), M("#C8412F"), .02f, .29f, 0, 0, 0, -.8f);
                    return true;
                }
                case "potatoes":
                {
                    // Two lumpy potatoes with dark eyes.
                    P(g, Sph(.075f, 12, 10), col, -.03f, .06f, 0, 0, .3f, 0, 1.35f, .85f, 1);
                    P(g, Sph(.06f, 12, 10), col, .08f, .05f, .05f, 0, -.6f, 0, 1.3f, .85f, 1);
                    for (int k = 0; k < 6; k++) P(g, Sph(.008f, 5, 4), M("#7A5A34"), -.03f + Mathf.Cos(k * 1.7f) * .09f, .06f + Mathf.Sin(k * 2.1f) * .03f, Mathf.Sin(k * 1.7f) * .065f);
                    return true;
                }
                case "cheese_wedge":
                {
                    // A proper wedge with a darker rind and holes on its faces.
                    P(g, Cyl(.14f, .14f, .1f, 3), col, 0, .05f, 0, 0, Pi / 6, 0);
                    P(g, Cyl(.141f, .141f, .012f, 3), M("#E0A92E"), 0, .104f, 0, 0, Pi / 6, 0);
                    foreach (var h in new[] { new Vector3(.03f, .06f, .065f), new Vector3(-.04f, .03f, .06f), new Vector3(.07f, .03f, .03f), new Vector3(-.01f, .104f, .01f) })
                        P(g, Sph(.016f, 8, 6), M("#D9A83A"), h.x, h.y, h.z, 0, 0, 0, 1, 1, .4f);
                    return true;
                }
                case "kimchi_jar":
                {
                    // A squat open jar heaped with red cabbage pieces, its green lid set aside.
                    P(g, Cyl(.085f, .08f, .13f, 16), M("#EFE7DA"), 0, .065f, 0);
                    P(g, Cyl(.078f, .078f, .02f, 16), M("#C94B32"), 0, .128f, 0);
                    for (int k = 0; k < 8; k++) P(g, RBox(.05f, .03f, .012f, .006f), M(k % 2 == 0 ? "#D9573B" : "#E9795A"), Mathf.Cos(k * 1.9f) * .04f * (k % 3), .145f + (k % 3) * .012f, Mathf.Sin(k * 1.9f) * .04f * (k % 3), .6f, k, .3f);
                    P(g, Cyl(.088f, .088f, .025f, 16), M("#6F9A74"), .13f, .012f, .05f);
                    return true;
                }
            }
            return false;
        }
    }
}
