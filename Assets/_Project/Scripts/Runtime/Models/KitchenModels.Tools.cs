using Squishy.Runtime.Three;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Kitchen tool models at the same detail as the ingredients (user feedback 29 Sep 2026: "Copper fork set is just
    /// some bars"). Each tool names its shape in the data; the body takes the tool skin's colour, handles are wood
    /// (gold with the Gold skin). They lie flat, about 0.4 long, handle towards +z.
    /// </summary>
    public static partial class KitchenModels
    {
        /// <summary>A hex colour lightened (f &gt; 1) or darkened (f &lt; 1).</summary>
        private static string Shade(string hex, float f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c = f >= 1 ? Color.Lerp(c, Color.white, f - 1) : c * f;
            c.a = 1;
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        /// <summary>A bowl from a unit sphere: the lower half is the outside, the upper half folds down inside it.</summary>
        private static Mesh Bowl() { return Deformed("toolbowl", 24, 12, v => v.y <= 0 ? v : new Vector3(v.x * .88f, -v.y * .82f, v.z * .88f)); }

        private static void ToolShape(string shape, string body, string wood, Transform g)
        {
            const float Pi = Mathf.PI;
            Material col = M(body), light = M(Shade(body, 1.3f)), dark = M(Shade(body, .6f)), w = M(wood), hole = M("#2A1E17");
            switch (shape)
            {
                case "spatula":
                {
                    // Slotted turner: round wooden handle with a hanging hole and a metal collar, a bent neck, a blade with three slots.
                    P(g, Cyl(.018f, .022f, .24f, 12), w, 0, .026f, .14f, Pi / 2);
                    P(g, Sph(.018f, 12, 8), w, 0, .026f, .26f);
                    P(g, Cyl(.007f, .007f, .05f, 8), hole, 0, .026f, .245f);
                    P(g, Torus(.022f, .006f, 6, 16), light, 0, .026f, .02f);
                    P(g, RBox(.032f, .012f, .08f, .005f), col, 0, .018f, -.03f, -.18f);
                    P(g, RBox(.17f, .014f, .17f, .025f), col, 0, .009f, -.14f);
                    foreach (var x in new[] { -.045f, 0f, .045f }) P(g, RBox(.02f, .004f, .1f, .002f), dark, x, .016f, -.15f);
                    P(g, RBox(.16f, .006f, .016f, .003f), light, 0, .005f, -.222f);
                    break;
                }
                case "cleaver":
                {
                    // A wide blade (bright bevelled edge, thick spine, hanging hole), a bolster and a riveted wooden handle.
                    P(g, RBox(.16f, .016f, .25f, .012f), col, -.02f, .01f, -.09f);
                    P(g, RBox(.03f, .012f, .25f, .006f), light, -.09f, .008f, -.09f);
                    P(g, RBox(.024f, .024f, .25f, .008f), dark, .052f, .012f, -.09f);
                    P(g, Cyl(.017f, .017f, .02f, 16), hole, .02f, .01f, -.175f);
                    P(g, Torus(.019f, .004f, 4, 16), light, .02f, .019f, -.175f, Pi / 2);
                    P(g, RBox(.05f, .03f, .03f, .01f), dark, .035f, .018f, .045f);
                    P(g, RBox(.048f, .036f, .15f, .016f), w, .035f, .02f, .13f);
                    foreach (var z in new[] { .08f, .13f, .18f }) P(g, Sph(.008f, 8, 6), light, .035f, .038f, z);
                    P(g, Sph(.024f, 12, 8), w, .035f, .02f, .205f, 0, 0, 0, 1, .75f, .6f);
                    break;
                }
                case "cutlery":
                {
                    // A fork, a spoon and a knife fanned out, their handles wrapped in a napkin band.
                    var fork = Node.Group(g, "fork", -.075f, 0, 0);
                    fork.RotY(.1f);
                    Handle(fork, col, dark);
                    P(fork, RBox(.016f, .01f, .07f, .004f), col, 0, .008f, -.05f);
                    P(fork, RBox(.052f, .012f, .04f, .01f), col, 0, .008f, -.095f);
                    foreach (var x in new[] { -.019f, -.0065f, .0065f, .019f })
                    {
                        P(fork, RBox(.008f, .009f, .09f, .003f), col, x, .007f, -.155f);
                        P(fork, Sph(.0045f, 8, 6), col, x, .007f, -.2f);
                    }
                    var spoon = Node.Group(g, "spoon", 0, 0, 0);
                    Handle(spoon, col, dark);
                    P(spoon, RBox(.016f, .01f, .06f, .004f), col, 0, .008f, -.05f);
                    P(spoon, Sph(.05f, 18, 10), col, 0, .012f, -.125f, 0, 0, 0, .9f, .24f, 1.25f);
                    P(spoon, Sph(.042f, 18, 10), light, 0, .017f, -.125f, 0, 0, 0, .85f, .12f, 1.18f);
                    var knife = Node.Group(g, "knife", .075f, 0, 0);
                    knife.RotY(-.1f);
                    P(knife, RBox(.032f, .018f, .17f, .008f), col, 0, .012f, .1f);
                    P(knife, Sph(.017f, 12, 8), col, 0, .012f, .185f, 0, 0, 0, 1, .55f, 1);
                    P(knife, RBox(.028f, .012f, .014f, .005f), dark, 0, .01f, .012f);
                    P(knife, RBox(.034f, .006f, .18f, .003f), col, 0, .006f, -.085f);
                    P(knife, Cyl(.017f, .017f, .006f, 18), col, 0, .006f, -.175f);
                    P(knife, RBox(.008f, .007f, .18f, .003f), light, -.012f, .007f, -.09f);
                    P(g, RBox(.25f, .026f, .046f, .012f), M("#EFE3CC"), 0, .018f, .1f);
                    P(g, RBox(.252f, .027f, .012f, .005f), M("#C8674E"), 0, .018f, .1f);
                    break;
                }
                case "wok":
                {
                    // A deep round bowl with a rolled rim, a polished centre, a long wooden handle and a loop handle opposite.
                    P(g, Bowl(), col, 0, .1f, 0, 0, 0, 0, .2f, .1f, .2f);
                    P(g, Sph(.1f, 18, 8), light, 0, .018f, 0, 0, 0, 0, 1, .12f, 1);
                    P(g, Torus(.2f, .01f, 6, 36), light, 0, .1f, 0, Pi / 2);
                    var h = Node.Group(g, "handle", .2f, .1f, 0);
                    h.RotZ(.18f);
                    P(h, Cyl(.016f, .02f, .07f, 10), dark, .035f, 0, 0, 0, 0, Pi / 2);
                    P(h, Torus(.021f, .005f, 4, 14), light, .07f, 0, 0, 0, Pi / 2);
                    P(h, Cyl(.022f, .02f, .17f, 12), w, .155f, 0, 0, 0, 0, Pi / 2);
                    P(h, Sph(.021f, 12, 8), w, .24f, 0, 0);
                    P(g, Torus(.03f, .008f, 6, 14), dark, -.215f, .1f, 0, Pi / 2);
                    break;
                }
                case "rollingpin":
                {
                    // A barrel with domed ends and faint grain rings, on an axle, with turned handles.
                    P(g, Cyl(.06f, .06f, .32f, 22), col, 0, .06f, 0, 0, 0, Pi / 2);
                    foreach (var x in new[] { -.16f, .16f }) P(g, Sph(.06f, 18, 10), col, x, .06f, 0, 0, 0, 0, .2f, 1, 1);
                    foreach (var x in new[] { -.09f, .09f }) P(g, Torus(.0602f, .0022f, 4, 28), M(Shade(body, .85f)), x, .06f, 0, 0, Pi / 2);
                    foreach (var s in new[] { -1f, 1f })
                    {
                        P(g, Cyl(.012f, .012f, .05f, 8), dark, s * .185f, .06f, 0, 0, 0, Pi / 2);
                        P(g, Torus(.024f, .006f, 6, 14), w, s * .205f, .06f, 0, 0, Pi / 2);
                        P(g, Cyl(.02f, .028f, .09f, 12), w, s * .25f, .06f, 0, 0, 0, s * Pi / 2);
                        P(g, Sph(.03f, 12, 8), w, s * .3f, .06f, 0);
                    }
                    break;
                }
                case "steamer":
                {
                    // A little bamboo steamer: two woven tiers with bands, a domed lid with rings and a knob.
                    var band = M(Shade(body, .78f));
                    P(g, Cyl(.18f, .18f, .1f, 28), col, 0, .05f, 0);
                    P(g, Cyl(.175f, .175f, .08f, 28), col, 0, .14f, 0);
                    foreach (var y in new[] { .015f, .085f, .125f, .17f }) P(g, Torus(.183f, .007f, 4, 36), band, 0, y, 0, Pi / 2);
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k / 16f * Pi * 2;
                        P(g, RBox(.008f, .05f, .004f, .002f), band, Mathf.Cos(a) * .181f, .05f, Mathf.Sin(a) * .181f, 0, -a + Pi / 2, 0);
                    }
                    P(g, Sph(.185f, 28, 12), col, 0, .18f, 0, 0, 0, 0, 1, .38f, 1);
                    P(g, Torus(.188f, .01f, 6, 36), band, 0, .18f, 0, Pi / 2);
                    P(g, Torus(.14f, .005f, 4, 32), band, 0, .226f, 0, Pi / 2);
                    P(g, Torus(.08f, .005f, 4, 24), band, 0, .245f, 0, Pi / 2);
                    P(g, Cyl(.024f, .03f, .03f, 12), w, 0, .26f, 0);
                    P(g, Sph(.026f, 12, 8), w, 0, .276f, 0, 0, 0, 0, 1, .6f, 1);
                    break;
                }
                case "ladle":
                {
                    // A deep bowl with a rim and a long handle rising from it, ending in a flattened grip with a hanging hole.
                    P(g, Bowl(), col, 0, .055f, -.15f, 0, 0, 0, .075f, .055f, .075f);
                    P(g, Torus(.075f, .006f, 6, 28), light, 0, .055f, -.15f, Pi / 2);
                    P(g, Cyl(.011f, .011f, .3f, 10), col, 0, .104f, .063f, Pi / 2 - .3f);
                    P(g, RBox(.036f, .012f, .07f, .006f), col, 0, .152f, .215f, -.3f);
                    P(g, Cyl(.007f, .007f, .016f, 8), hole, 0, .155f, .225f, -.3f);
                    break;
                }
                default: // chopsticks
                {
                    // A tapered pair with two bands near the top, their tips raised on a little ceramic rest.
                    foreach (var s in new[] { -1f, 1f })
                    {
                        var st = Node.Group(g, "stick", s * .024f, .03f, 0);
                        Node.Rot(st, -.05f, s * .03f, 0);
                        P(st, Cyl(.008f, .014f, .4f, 8), col, 0, 0, 0, Pi / 2);
                        P(st, Torus(.0135f, .003f, 4, 12), M("#EFE3CC"), 0, 0, -.16f);
                        P(st, Torus(.013f, .0025f, 4, 12), dark, 0, 0, -.14f);
                    }
                    var cream = M("#F4EBDD");
                    P(g, Sph(.03f, 14, 8), cream, 0, .012f, .13f, 0, 0, 0, 2, .45f, .8f);
                    foreach (var x in new[] { -.052f, .052f }) P(g, Sph(.022f, 12, 8), cream, x, .02f, .13f);
                    foreach (var x in new[] { -.052f, .052f }) P(g, Sph(.007f, 8, 6), M("#6E9C9A"), x, .04f, .13f);
                    break;
                }
            }
        }

        /// <summary>A cutlery handle: flat, widening to a rounded end, with an engraved line.</summary>
        private static void Handle(Transform g, Material col, Material dark)
        {
            P(g, RBox(.028f, .012f, .2f, .006f), col, 0, .008f, .08f);
            P(g, Sph(.022f, 12, 8), col, 0, .008f, .18f, 0, 0, 0, 1, .3f, 1.4f);
            P(g, RBox(.006f, .003f, .16f, .0015f), dark, 0, .0145f, .1f);
        }
    }
}
