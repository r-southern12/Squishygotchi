using System;
using System.Collections.Generic;
using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Prestige hats (full review, 1 Oct 2026): smooth turned shapes with rounded edges and proper details, and
    /// anything that covers the head is built over the outside of the dumpling's pleats (the line of the ridges), so
    /// no fold pokes through. Modelled in "hat units": the group is scaled by HatScale.
    /// </summary>
    public sealed partial class SquishyModel
    {
        private const float HatScale = 1.7f;
        private static readonly Dictionary<string, Mesh> HatMeshes = new Dictionary<string, Mesh>();

        private Transform Hat(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            g.localPosition = ShapeAt(Vector3.up) + new Vector3(0, -.04f, 0);
            Material col = M(c.color), cream = M("#FFF7EC"), gold = M("#D9B45A"), green = M("#7DBA5E"), dark = M(Shade(c.color, .78f));
            float size = 1; // a few pieces read small on the wide dome
            switch (c.kind)
            {
                case "cone": // party hat: a smooth cone with a trim, dots and a fluffy pompom
                {
                    var t = Node.Group(g, "tilt");
                    t.RotZ(-.18f);
                    Node.Mesh(t, Lathe("party_cone", P(0, .47f, .016f, .458f, .03f, .43f, .2f, .035f, .19f, .025f, 0, .025f)), col, 0, 0, 0, shadow: false);
                    Node.Mesh(t, Torus(.2f, .026f, 10, 40), cream, 0, .035f, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(t, Fluffy(), cream, 0, .48f, 0, shadow: false).Scale(.07f, .07f, .07f);
                    for (int i = 0; i < 9; i++)
                        OnCone(t, .2f, .035f, .016f, .458f, i * 2.4f, .15f + (i % 3) * .24f + (i / 3) * .03f, Circle(1, 16), cream, .026f);
                    break;
                }
                case "flowers": // a leafy ring of little five-petal flowers
                    Node.Mesh(g, Torus(.33f, .026f, 8, 40), M("#8FAE7E"), 0, .0f, 0, shadow: false).RotX(Mathf.PI / 2);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2, a2 = a + Mathf.PI / 8;
                        var fl = Node.Group(g, "flower", Mathf.Cos(a) * .33f, .025f, Mathf.Sin(a) * .33f);
                        fl.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), .9f, Mathf.Sin(a)).normalized);
                        var pm = i % 2 == 0 ? col : M(c.color == "#FFFFFF" ? "#FFF1A8" : "#FFFFFF");
                        for (int k = 0; k < 5; k++) { float pa = k * Mathf.PI * 2 / 5; Node.Mesh(fl, Sph(.05f, 12, 8), pm, Mathf.Cos(pa) * .056f, Mathf.Sin(pa) * .056f, 0, shadow: false).Scale(1, 1, .45f); }
                        Node.Mesh(fl, Sph(.034f, 10, 8), M("#F2C230"), 0, 0, .016f, shadow: false);
                        var leaf = Node.Mesh(g, Sph(.045f, 12, 6), M("#6E9F5A"), Mathf.Cos(a2) * .34f, .015f, Mathf.Sin(a2) * .34f, shadow: false);
                        leaf.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a2), 0, Mathf.Sin(a2)));
                        leaf.localScale = new Vector3(1.7f, .8f, .35f);
                    }
                    break;
                case "beret": // a soft pancake on a fitted band, tipped to one side, with its little stalk
                {
                    var e = Env(.9f, .03f);
                    Node.Mesh(g, Torus(e.x, .02f, 8, 48), dark, 0, e.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    var t = Node.Group(g, "tilt", .04f, 0, 0);
                    t.RotZ(-.2f);
                    Node.Mesh(t, Lathe("beret", P(0, .15f, .14f, .147f, .27f, .13f, .37f, .1f, .43f, .068f, .455f, .035f, .45f, .008f, .42f, -.012f, .36f, -.022f, .3f, -.02f, 0, -.01f)), col, 0, 0, 0, shadow: false);
                    Node.Mesh(t, Cyl(.016f, .022f, .07f, 10), col, 0, .18f, 0, shadow: false);
                    Node.Mesh(t, Sph(.024f, 10, 8), col, 0, .215f, 0, shadow: false);
                    break;
                }
                case "chef": // a pleated puff over a tall band
                    Node.Mesh(g, Lathe("chef_band", P(.19f, .17f, .212f, .17f, .216f, .155f, .216f, .005f, .21f, -.012f, .19f, -.012f, .19f, .17f)), col, 0, 0, 0, shadow: false);
                    Node.Mesh(g, Torus(.214f, .014f, 8, 40), M("#F1EADF"), 0, .16f, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, ChefPuff(), col, 0, .28f, 0, shadow: false).Scale(.27f, .27f, .27f);
                    break;
                case "bow":
                    Node.Rot(Bow(g, col, .08f, .06f, .1f, 1), -.6f, 0, -.2f); // tipped back along the head, so the tails lie forward over it
                    break;
                case "beanie": // knitted: ribbed crown over the pleats, a turned-up cuff and a fluffy pompom
                {
                    // Pulled well down over the head, domed on top.
                    Node.Mesh(g, Lathe("beanie_shell", Domed(.42f, .06f, .1f), 96, (th, u) => 1 + .012f * Mathf.Cos(30 * th) * Mathf.Clamp01(u * 1.6f)), col, 0, 0, 0, shadow: false);
                    var cuff = new List<Vector2> { Env(.535f, .07f), Env(.525f, .1f), Env(.51f, .115f) };
                    cuff.AddRange(EnvProfile(.49f, .4f, .115f, 5));
                    cuff.Add(Env(.385f, .1f)); cuff.Add(Env(.378f, .07f)); cuff.Add(Env(.385f, .04f));
                    cuff.AddRange(EnvProfile(.4f, .52f, .035f, 5));
                    cuff.Add(Env(.535f, .07f));
                    // Knit ribs on the cuff's face only, fading to smooth, round edges top and bottom (they read as a jagged edge).
                    Node.Mesh(g, Lathe("beanie_cuff", cuff, 160, (th, u) => 1 + .014f * Mathf.Cos(48 * th) * Mathf.Pow(Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01((u - .06f) / .4f) * Mathf.PI)), 1.5f)), col, 0, 0, 0, shadow: false);
                    Node.Mesh(g, Fluffy(), cream, 0, Env(1, .16f).y + .09f, 0, shadow: false).Scale(.12f, .12f, .12f);
                    break;
                }
                case "tophat": // tall crown, curled brim and a ribbon band
                    Node.Mesh(g, Lathe("top_hat", P(0, .42f, .165f, .42f, .19f, .415f, .203f, .4f, .205f, .38f, .19f, .055f, .19f, .045f, .19f, .045f, .28f, .042f, .33f, .048f, .355f, .06f, .368f, .068f, .374f, .06f, .366f, .044f, .345f, .03f, .3f, .024f, .19f, .022f, 0, .02f)), col, 0, 0, 0, shadow: false);
                    Node.Mesh(g, Lathe("top_band", P(.185f, .13f, .2f, .13f, .2f, .13f, .194f, .058f, .194f, .058f, .185f, .058f)), M("#C8674E"), 0, 0, 0, shadow: false);
                    break;
                case "sunhat": // a rounded crown, a wide soft brim with a woven look, a ribbon and a bow
                {
                    Node.Mesh(g, Lathe("sun_hat", P(0, .17f, .1f, .166f, .17f, .148f, .205f, .12f, .22f, .08f, .225f, .045f, .225f, .045f, .3f, .036f, .39f, .018f, .47f, -.011f, .515f, -.03f, .532f, -.042f, .528f, -.054f, .51f, -.056f, .46f, -.03f, .38f, -.002f, .3f, .014f, .23f, .02f, 0, .02f)), col, 0, 0, 0, shadow: false);
                    var weave = M(Shade(c.color, .86f));
                    foreach (var rr in new[] { new Vector2(.3f, .036f), new Vector2(.39f, .018f), new Vector2(.47f, -.011f) })
                        Node.Mesh(g, Torus(rr.x, .005f, 6, 56), weave, 0, rr.y + .004f, 0, shadow: false).RotX(Mathf.PI / 2);
                    var ribbon = M("#E86A92");
                    Node.Mesh(g, Lathe("sun_band", P(.2f, .112f, .222f, .112f, .222f, .112f, .23f, .05f, .23f, .05f, .2f, .05f)), ribbon, 0, 0, 0, shadow: false);
                    float a = 2.2f;
                    var b = Bow(g, ribbon, Mathf.Sin(a) * .235f, .082f, Mathf.Cos(a) * .235f, .42f);
                    b.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)));
                    break;
                }
                case "cap": // six panels over the pleats, a top button, a trim and a rounded peak
                {
                    Node.Mesh(g, Lathe("cap_shell", Domed(.5f, .045f, .08f), 72, (th, u) => 1 - .03f * Mathf.Pow(Mathf.Abs(Mathf.Cos(3 * th)), 80) * Mathf.Clamp01(u * 3)), col, 0, 0, 0, shadow: false);
                    var e = Env(.5f, .045f);
                    Node.Mesh(g, Torus(e.x, .014f, 8, 64), dark, 0, e.y, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.035f, 12, 8), col, 0, Env(1, .125f).y + .012f, 0, shadow: false).Scale(1, .6f, 1);
                    Node.Mesh(g, Sph(.3f, 32, 12), col, 0, e.y + .012f, e.x - .08f, shadow: false).Scale(1.05f, .08f, .8f).RotX(.12f);
                    break;
                }
                case "ears_cat":
                case "ears_bunny":
                case "ears_bear":
                    for (int k = 0; k < 2; k++)
                    {
                        float sx = k == 0 ? -1 : 1;
                        var e = Node.Group(g, "ear", sx * (c.kind == "ears_bunny" ? .3f : .27f), -.04f, 0);
                        if (c.kind == "ears_cat")
                        {
                            e.localRotation = Quaternion.AngleAxis(sx * -12, Vector3.up) * Quaternion.AngleAxis(-sx * 16, Vector3.forward);
                            Node.Mesh(e, CatEar(), col, 0, 0, 0, shadow: false).Scale(sx, 1, 1);
                            Node.Mesh(e, CatEar(), M("#EE9DB5"), 0, .035f, .018f, shadow: false).Scale(sx * .56f, .66f, .5f);
                        }
                        else if (c.kind == "ears_bunny")
                        {
                            e.localRotation = Quaternion.AngleAxis(sx * -10, Vector3.up) * Quaternion.AngleAxis(-sx * 12, Vector3.forward);
                            var flop = Node.Group(e, "flop");
                            AddSpring(flop, 42, 3.2f, 1.2f);
                            Node.Mesh(flop, BunnyEar(), col, 0, 0, 0, shadow: false).Scale(sx, 1, 1);
                            Node.Mesh(flop, BunnyEar(), M("#F3A6BD"), 0, .045f, .02f, shadow: false).Scale(sx * .62f, .84f, .5f);
                        }
                        else
                        {
                            Node.Mesh(e, Sph(.14f, 20, 14), col, 0, .09f, 0, shadow: false).Scale(1, 1, .65f);
                            Node.Mesh(e, Sph(.08f, 16, 12), M("#E7C9A8"), 0, .09f, .06f, shadow: false).Scale(1, 1, .45f);
                        }
                    }
                    break;
                case "sprout":
                {
                    var stem = Node.Group(g, "stem", 0, -.01f, 0);
                    AddSpring(stem, 55, 4.5f, 1.4f);
                    Vector3 s0 = Vector3.zero, s1 = new Vector3(.014f, .09f, .008f), s2 = new Vector3(-.004f, .2f, 0);
                    Stick(stem, s0, s1, .021f, green);
                    Stick(stem, s1, s2, .018f, green);
                    Node.Mesh(stem, Sph(.021f, 10, 8), green, s1.x, s1.y, s1.z, shadow: false);
                    Node.Mesh(stem, Sph(.022f, 10, 8), green, s2.x, s2.y, s2.z, shadow: false);
                    Material under = M(Shade(c.color, 1.18f)), vein = M(Shade(c.color, 1.3f));
                    for (int k = 0; k < 2; k++)
                    {
                        var lp = Node.Group(stem, "leaf", s2.x, s2.y, s2.z);
                        lp.localRotation = Quaternion.AngleAxis(k == 0 ? 180 : 0, Vector3.up) * Quaternion.AngleAxis(30, Vector3.forward) * Quaternion.AngleAxis(k == 0 ? 14 : -14, Vector3.right);
                        AddSpring(lp, 120, 6, 2.6f);
                        LeafOn(lp, col, under, vein, green, .44f);
                    }
                    // A new pair of leaves just opening in the middle.
                    for (int k = 0; k < 2; k++)
                    {
                        var bp = Node.Group(stem, "bud", s2.x, s2.y + .005f, s2.z);
                        bp.localRotation = Quaternion.AngleAxis(k == 0 ? 90 : 270, Vector3.up) * Quaternion.AngleAxis(68, Vector3.forward);
                        LeafOn(bp, col, under, vein, green, .13f);
                    }
                    break;
                }
                case "cherry":
                {
                    var joint = new Vector3(-.01f, .3f, .1f);
                    foreach (var p in new[] { new Vector3(-.12f, .03f, .14f), new Vector3(.11f, 0, .17f) })
                    {
                        Node.Mesh(g, Sph(.12f, 18, 14), col, p.x, p.y, p.z, shadow: false);
                        Node.Mesh(g, Sph(.03f, 8, 6), M("#FFFFFF", "#FFFFFF"), p.x - .04f, p.y + .05f, p.z + .08f, shadow: false); // a shine
                        Stick(g, p + new Vector3(0, .1f, 0), joint, .014f, green);
                    }
                    Node.Mesh(g, Sph(.07f, 12, 8), green, joint.x + .08f, joint.y + .01f, joint.z, shadow: false).Scale(1.6f, .4f, .8f).RotZ(-.4f);
                    break;
                }
                case "conical": // woven bamboo: a shallow cone with ribs, a rim and a top knob
                {
                    Node.Mesh(g, Lathe("bamboo_hat", P(0, .25f, .02f, .247f, .05f, .235f, .49f, .012f, .505f, .002f, .505f, -.008f, .49f, -.014f, .05f, .2f, 0, .205f)), col, 0, 0, 0, shadow: false);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2, sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                        Stick(g, new Vector3(sa * .06f, .236f, ca * .06f), new Vector3(sa * .48f, .024f, ca * .48f), .006f, dark);
                    }
                    Node.Mesh(g, Torus(.5f, .013f, 8, 56), dark, 0, -.003f, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Sph(.035f, 12, 8), dark, 0, .25f, 0, shadow: false);
                    break;
                }
                case "wizard": // a starry cone with a gold band, a crescent moon and stars on the brim
                {
                    Node.Mesh(g, Lathe("wizard_brim", P(0, .022f, .3f, .022f, .36f, .018f, .385f, .006f, .385f, -.004f, .36f, -.012f, .3f, -.012f, 0, -.012f)), col, 0, 0, 0, shadow: false);
                    var cone = Node.Group(g, "cone", 0, .28f, 0);
                    cone.RotZ(-.15f);
                    Node.Mesh(cone, Lathe("wizard_cone", P(0, .285f, .014f, .272f, .2f, -.275f, .2f, -.29f)), col, 0, 0, 0, shadow: false);
                    Node.Mesh(cone, Torus(.19f, .024f, 8, 40), gold, 0, -.245f, 0, shadow: false).RotX(Mathf.PI / 2);
                    var shine = M("#FFE08A", "#B08A2A");
                    // (angle round the cone from the front, how far up it (0 to 1), size)
                    foreach (var s in new[] { new Vector3(-.35f, .26f, .06f), new Vector3(.8f, .44f, .045f), new Vector3(-1f, .56f, .04f), new Vector3(.25f, .72f, .032f), new Vector3(2.4f, .3f, .05f), new Vector3(-2.2f, .5f, .045f), new Vector3(3.14f, .7f, .03f) })
                        OnCone(cone, .2f, -.275f, .014f, .272f, s.x, s.y, PuffyStar(), shine, s.z);
                    OnCone(cone, .2f, -.275f, .014f, .272f, .45f, .12f, Torus(.6f, .22f, 10, 24, Mathf.PI * 1.25f), shine, .05f).localRotation *= Quaternion.AngleAxis(-40, Vector3.forward);
                    foreach (var a in new[] { .9f, -1.3f, 2.8f })
                    {
                        var st = Node.Mesh(g, PuffyStar(), shine, Mathf.Sin(a) * .3f, .034f, Mathf.Cos(a) * .3f, shadow: false);
                        st.localRotation = Quaternion.LookRotation(Vector3.up) * Quaternion.AngleAxis(a * 57f, Vector3.forward);
                        st.localScale = Vector3.one * .045f;
                    }
                    break;
                }
                case "pirate": // a bicorne: upturned brims edged in gold, with a little skull and crossbones
                {
                    // A snug crown over the top, then two half-moon brims standing up front and back, leaning out a little.
                    Node.Mesh(g, Lathe("pirate_crown", Domed(.75f, .05f, .12f)), col, 0, 0, 0, shadow: false);
                    for (int k = 0; k < 2; k++)
                    {
                        float sz = k == 0 ? 1 : -1;
                        var flap = Node.Group(g, "brim", 0, .005f, sz * .09f);
                        flap.RotX(sz * .18f);
                        Node.Mesh(flap, HalfMoon(), gold, 0, -.004f, -sz * .012f, shadow: false).Scale(1.04f, 1.08f, 1);
                        Node.Mesh(flap, HalfMoon(), col, 0, 0, 0, shadow: false);
                    }
                    var bone = M("#FFF7EC");
                    var skull = Node.Group(g, "skull", 0, .005f, .09f);
                    skull.RotX(.18f);
                    Node.Mesh(skull, Sph(.058f, 16, 12), bone, 0, .16f, .045f, shadow: false).Scale(1, .95f, .45f);
                    Node.Mesh(skull, RBox(.052f, .03f, .02f, .01f), bone, 0, .118f, .045f, shadow: false);
                    for (int k = 0; k < 2; k++)
                    {
                        float sx = k == 0 ? -1 : 1;
                        Node.Mesh(skull, Sph(.015f, 8, 6), M("#1E1A1F"), sx * .022f, .165f, .07f, shadow: false);
                        Node.Mesh(skull, RBox(.16f, .022f, .02f, .01f), bone, 0, .085f, .04f, shadow: false).RotZ(sx * .55f);
                    }
                    break;
                }
                case "tiara":
                    size = 1.15f;
                    for (int i = 0; i <= 6; i++)
                    {
                        float a = (i / 6f - .5f) * 2.1f, x = Mathf.Sin(a) * .34f, z = Mathf.Cos(a) * .34f;
                        Node.Mesh(g, Sph(.04f, 10, 8), gold, x, -.02f, z, shadow: false);
                        if (i < 6)
                        {
                            float a2 = ((i + .5f) / 6f - .5f) * 2.1f;
                            Node.Rot(Node.Mesh(g, Cyl(.03f, .03f, .13f, 10), gold, Mathf.Sin(a2) * .34f, -.02f, Mathf.Cos(a2) * .34f, shadow: false), 0, a2, Mathf.PI / 2); // lies along the arc
                        }
                        float h = i == 3 ? .24f : i % 2 == 1 ? .16f : .1f;
                        Node.Mesh(g, Cyl(0, .05f, h, 10), col, x, -.01f + h / 2, z, shadow: false);
                    }
                    Node.Mesh(g, Sph(.06f, 14, 10), M("#F48FB1", "#F48FB1"), 0, .07f, .36f, shadow: false);
                    break;
                case "halo":
                    Node.Mesh(g, Torus(.26f, .035f, 12, 48), M(c.color, c.color), 0, .32f, 0, shadow: false).RotX(Mathf.PI / 2 - .2f);
                    break;
                default: // crown: a rimmed band with six balled points and jewels
                    size = 1.25f;
                    Node.Mesh(g, Lathe("crown_band", P(.225f, .11f, .245f, .11f, .245f, .11f, .245f, -.005f, .245f, -.005f, .225f, -.005f, .225f, -.005f, .225f, .11f)), col, 0, 0, 0, shadow: false);
                    Node.Mesh(g, Torus(.246f, .014f, 8, 48), col, 0, .11f, 0, shadow: false).RotX(Mathf.PI / 2);
                    Node.Mesh(g, Torus(.247f, .016f, 8, 48), col, 0, -.004f, 0, shadow: false).RotX(Mathf.PI / 2);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        Node.Mesh(g, Cyl(.006f, .06f, .12f, 12), col, ca * .235f, .165f, sa * .235f, shadow: false);
                        Node.Mesh(g, Sph(.026f, 10, 8), col, ca * .235f, .228f, sa * .235f, shadow: false); // a ball on each point
                        Node.Mesh(g, Sph(.032f, 12, 10), M(i % 2 == 0 ? "#E8505B" : "#5FA7D9"), ca * .25f, .052f, sa * .25f, shadow: false).Scale(1, 1.2f, 1);
                    }
                    break;
            }
            g.localScale = Vector3.one * HatScale * size;
            return g;
        }

        // ---------------- springy bits and hats that bend with the body ----------------

        private sealed class AccSpring { public Transform t; public Quaternion rest; public float ax, vx, az, vz, k, d, gain; }
        private readonly List<AccSpring> _springs = new List<AccSpring>();
        private float _accPrevSquash, _accPrevYaw;

        private void AddSpring(Transform t, float k, float d, float gain) { _springs.Add(new AccSpring { t = t, rest = t.localRotation, k = k, d = d, gain = gain }); }

        /// <summary>Springy accessories (sprout leaves, bunny ears): bounces and turns set them swaying, then they settle.</summary>
        private void StepAccessories(float dt, float squash)
        {
            float yaw = Yaw != null ? Yaw.localEulerAngles.y : 0;
            if (dt <= 0 || _springs.Count == 0) { _accPrevSquash = squash; _accPrevYaw = yaw; return; }
            float jolt = (squash - _accPrevSquash) / dt, turn = Mathf.Clamp(Mathf.DeltaAngle(_accPrevYaw, yaw) * Mathf.Deg2Rad / dt, -10, 10);
            _accPrevSquash = squash;
            _accPrevYaw = yaw;
            for (int i = _springs.Count - 1; i >= 0; i--)
            {
                var s = _springs[i];
                if (s.t == null) { _springs.RemoveAt(i); continue; }
                s.vx += (-s.k * s.ax - s.d * s.vx + jolt * s.gain) * dt;
                s.ax = Mathf.Clamp(s.ax + s.vx * dt, -.8f, .8f);
                s.vz += (-s.k * s.az - s.d * s.vz - turn * s.gain * .35f + jolt * s.gain * .3f * Mathf.Sin(Time.time * 3 + i)) * dt;
                s.az = Mathf.Clamp(s.az + s.vz * dt, -.8f, .8f);
                s.t.localRotation = s.rest * Quaternion.Euler(s.ax * Mathf.Rad2Deg, 0, s.az * Mathf.Rad2Deg);
            }
        }

        private sealed class HatSkin { public Mesh mesh; public Vector3[] basis, work; public Matrix4x4 toBody, fromBody; }
        private readonly List<HatSkin> _hatSkins = new List<HatSkin>();
        private readonly HashSet<Transform> _skinRoots = new HashSet<Transform>();

        /// <summary>
        /// An accessory gets its own copy of its meshes, so presses and pinches bend it exactly like the body beneath
        /// (not carried as one stiff piece, which let the body show through). Springy ones ride the surface instead.
        /// </summary>
        private void SkinHat(Transform g)
        {
            _skinRoots.Add(g);
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                var m = UnityEngine.Object.Instantiate(mf.sharedMesh);
                mf.sharedMesh = m;
                var toBody = Body.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var v = m.vertices;
                _hatSkins.Add(new HatSkin { mesh = m, basis = v, work = new Vector3[v.Length], toBody = toBody, fromBody = toBody.inverse });
            }
        }

        private bool Skinned(Transform t) { return _skinRoots.Contains(t); }

        private void ClearHatSkins()
        {
            foreach (var s in _hatSkins) if (s.mesh != null) UnityEngine.Object.Destroy(s.mesh);
            _hatSkins.Clear();
            _skinRoots.Clear();
        }

        /// <summary>Moves a skinned hat with the body's dents and pinch (rest: back to its own shape).</summary>
        private void DeformHat(bool rest)
        {
            foreach (var s in _hatSkins)
            {
                if (s.mesh == null) continue;
                if (rest) s.mesh.vertices = s.basis;
                else
                {
                    for (int i = 0; i < s.basis.Length; i++) s.work[i] = s.fromBody.MultiplyPoint3x4(Displaced(s.toBody.MultiplyPoint3x4(s.basis[i])));
                    s.mesh.vertices = s.work;
                }
                s.mesh.RecalculateBounds();
            }
        }

        // ---------------- shapes ----------------

        private static List<Vector2> P(params float[] xy)
        {
            var l = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new Vector2(xy[i], xy[i + 1]));
            return l;
        }

        /// <summary>
        /// The head's outline at a direction's height, taken along a pleat's ridge (the outside of the folds), lifted by
        /// margin (body units), in hat units: x is the radius, y the height above the hat's base.
        /// </summary>
        private static Vector2 Env(float dy, float margin)
        {
            dy = Mathf.Clamp(dy, -.99f, 1);
            float h = Mathf.Sqrt(Mathf.Max(0, 1 - dy * dy)), th = Mathf.PI / 10 - Twist(dy); // on a ridge: |cos 5(th + twist)| = 0
            var p = ShapeAt(new Vector3(h * Mathf.Cos(th), dy, h * Mathf.Sin(th)));
            float r = new Vector2(p.x, p.z).magnitude;
            var n = new Vector2(r, p.y + .1f).normalized;
            float top = ShapeAt(Vector3.up).y - .04f;
            return new Vector2((r + n.x * margin) / HatScale, (p.y + n.y * margin - top) / HatScale);
        }

        /// <summary>A shell over the head from the top down to dyTo, lifted by margin, plus a little extra (dome) on top.</summary>
        private static List<Vector2> Domed(float dyTo, float margin, float dome)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float dy = Mathf.Lerp(1, dyTo, i / 24f);
                l.Add(Env(dy, margin + dome * Sstep(.5f, 1, dy)));
            }
            return l;
        }

        /// <summary>
        /// A pirate hat's brim: a thick half-moon standing up, one unit being the hat's width. Its bottom edge follows the
        /// head down each side (it sat flat, leaving the ends floating).
        /// </summary>
        private static Mesh HalfMoon()
        {
            return Deformed("half_moon2", 48, 24, v =>
            {
                float x = v.x * .44f;
                if (v.y >= 0) return new Vector3(x, v.y * .3f, v.z * .045f);
                float bottom = Mathf.Min(0, HeadY(Mathf.Sqrt(x * x + .0081f)) - .02f);
                return new Vector3(x, bottom * Mathf.Clamp01(-v.y * 3), v.z * .045f);
            });
        }

        /// <summary>The head's height (over the pleats) at a distance from its middle, in hat units.</summary>
        private static float HeadY(float radius)
        {
            var prev = Env(1, 0);
            for (float dy = .98f; dy >= .1f; dy -= .02f)
            {
                var e = Env(dy, 0);
                if (e.x >= radius) return Mathf.Lerp(prev.y, e.y, (radius - prev.x) / Mathf.Max(1e-5f, e.x - prev.x));
                prev = e;
            }
            return prev.y;
        }

        private static List<Vector2> EnvProfile(float dyFrom, float dyTo, float margin, int steps)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= steps; i++) l.Add(Env(Mathf.Lerp(dyFrom, dyTo, i / (float)steps), margin));
            return l;
        }

        /// <summary>
        /// A turned shape from a profile of (radius, height) points, run from the top outwards and down (so the outside
        /// faces out); a repeated point makes a crisp edge. rMul(angle, 0..1 down the profile) can ripple the radius
        /// (knit ribs, panel seams).
        /// </summary>
        private static Mesh Lathe(string key, List<Vector2> pts, int seg = 56, Func<float, float, float> rMul = null)
        {
            if (HatMeshes.TryGetValue(key, out var cached) && cached != null) return cached;
            int n = pts.Count;
            var n2 = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                bool dupPrev = i > 0 && (pts[i] - pts[i - 1]).sqrMagnitude < 1e-10f, dupNext = i < n - 1 && (pts[i + 1] - pts[i]).sqrMagnitude < 1e-10f;
                Vector2 t = i == 0 || dupPrev ? pts[Mathf.Min(i + 1, n - 1)] - pts[i] : i == n - 1 || dupNext ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1];
                n2[i] = new Vector2(-t.y, t.x).normalized;
            }
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= seg; j++)
                {
                    float th = j / (float)seg * Mathf.PI * 2, c = Mathf.Cos(th), s = Mathf.Sin(th);
                    float r = pts[i].x * (rMul != null ? rMul(th, i / (float)(n - 1)) : 1);
                    v.Add(new Vector3(r * c, pts[i].y, r * s));
                    nn.Add(new Vector3(n2[i].x * c, n2[i].y, n2[i].x * s));
                    uv.Add(new Vector2(j / (float)seg, i / (float)(n - 1)));
                }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + 1, c = a + seg + 1, d = c + 1;
                    var sum = nn[a] + nn[b] + nn[c] + nn[d];
                    float face = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), sum) + Vector3.Dot(Vector3.Cross(v[d] - v[b], v[c] - v[b]), sum);
                    if (face >= 0) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c); }
                    else { tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d); }
                }
            var m = new Mesh { name = key };
            m.SetVertices(v);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            if (rMul != null)
            {
                // Rippled: normals from the surface itself, matched across the seam.
                m.RecalculateNormals();
                var ns = m.normals;
                for (int i = 0; i < n; i++)
                {
                    int a = i * (seg + 1), b = a + seg;
                    ns[a] = ns[b] = (ns[a] + ns[b]).normalized;
                }
                m.normals = ns;
            }
            m.RecalculateBounds();
            return HatMeshes[key] = m;
        }

        /// <summary>A fluffy yarn pompom, one unit across.</summary>
        private static Mesh Fluffy()
        {
            return Deformed("fluffy_ball", 32, 22, v => v * (1 + .09f * Mathf.Sin(v.x * 9 + 1) * Mathf.Sin(v.y * 9) * Mathf.Sin(v.z * 9 + 2) + .06f * Mathf.Sin(v.x * 15) * Mathf.Sin(v.y * 13 + 1) * Mathf.Sin(v.z * 14)));
        }

        /// <summary>The chef hat's puff: a flattened ball with eight soft pleats round its sides.</summary>
        private static Mesh ChefPuff()
        {
            return Deformed("chef_puff", 48, 24, v =>
            {
                float side = Mathf.Sqrt(Mathf.Max(0, 1 - v.y * v.y)), k = 1 + .07f * Mathf.Cos(8 * Mathf.Atan2(v.z, v.x)) * side;
                return new Vector3(v.x * k, v.y * .72f, v.z * k);
            });
        }

        /// <summary>A plump five-pointed star, one unit across, facing +z.</summary>
        private static Mesh PuffyStar()
        {
            return Deformed("puffy_star", 40, 24, v =>
            {
                float a = Mathf.Atan2(v.y, v.x), k = Mathf.Lerp(.48f, 1f, Mathf.Pow((Mathf.Cos(5 * (a - Mathf.PI / 2)) + 1) / 2, 1.6f));
                return new Vector3(v.x * k, v.y * k, v.z * .32f);
            });
        }

        /// <summary>A bow facing +z: two folded ribbon loops, a wrapped knot and two V-cut ribbon tails.</summary>
        private static Transform Bow(Transform parent, Material mat, float x, float y, float z, float scale)
        {
            var b = Node.Group(parent, "bow", x, y, z);
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1 : 1;
                Node.Mesh(b, BowLoop(), mat, sx * .02f, .02f, 0, shadow: false).Scale(sx, 1, 1).RotZ(sx * .22f);
                Node.Mesh(b, RibbonTail(), mat, sx * .012f, -.01f, .012f, shadow: false).Scale(sx, 1, 1);
            }
            Node.Mesh(b, RBox(.075f, .095f, .07f, .03f), mat, 0, .015f, .018f, shadow: false);
            b.localScale = Vector3.one * scale;
            return b;
        }

        /// <summary>One loop of a bow (+x from the knot): pinched at the knot, full and rounded at the end, folded down the middle.</summary>
        private static Mesh BowLoop()
        {
            return Deformed("bow_loop2", 48, 28, v =>
            {
                float u = (v.x + 1) / 2, c = Mathf.Sqrt(Mathf.Max(1e-4f, 1 - v.x * v.x)), cy = v.y / c, cz = v.z / c;
                float pinch = .3f + .7f * Mathf.SmoothStep(0, 1, Mathf.Clamp01(u / .55f));
                float end = Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow(Mathf.Max(0, (u - .6f) / .4f), 2)));
                float y = cy * .13f * pinch * end, z = cz * .065f * pinch * end;
                z *= 1 - .45f * Mathf.Exp(-(cy / .32f) * (cy / .32f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((u - .1f) / .5f)); // the fold
                return new Vector3(u * .3f, y + .06f * u * u, z);
            });
        }

        /// <summary>A ribbon tail hanging from a bow's knot (+x side, facing +z): a gentle wave, a slight taper and a V-cut end.</summary>
        private static Mesh RibbonTail()
        {
            if (HatMeshes.TryGetValue("ribbon_tail", out var cached) && cached != null) return cached;
            const int N = 16;
            const float thick = .012f;
            var p = new Vector3[N + 1];
            for (int i = 0; i <= N; i++) { float t = i / (float)N; p[i] = new Vector3(.05f * t + .05f * t * t, -.22f * t, .018f * Mathf.Sin(t * Mathf.PI) - .012f * t); }
            var L = new Vector3[N + 1];
            var Cn = new Vector3[N + 1];
            var R = new Vector3[N + 1];
            for (int i = 0; i <= N; i++)
            {
                var tg = (p[Mathf.Min(N, i + 1)] - p[Mathf.Max(0, i - 1)]).normalized;
                var side = new Vector3(tg.y, -tg.x, 0).normalized;
                float w = .075f * (1 - .12f * i / N) / 2;
                L[i] = p[i] + side * w;
                R[i] = p[i] - side * w;
                Cn[i] = i == N ? p[i] - tg * w * 1.1f : p[i]; // the V-cut
            }
            var v = new List<Vector3>();
            var tri = new List<int>();
            var zf = new Vector3(0, 0, thick / 2);
            void Strip(Vector3[] a, Vector3[] b, Vector3 offA, Vector3 offB, Vector3 expect)
            {
                int s = v.Count;
                for (int i = 0; i <= N; i++) { v.Add(a[i] + offA); v.Add(b[i] + offB); }
                for (int i = 0; i < N; i++)
                {
                    int i0 = s + i * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                    float f = Vector3.Dot(Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]), expect) + Vector3.Dot(Vector3.Cross(v[i3] - v[i1], v[i2] - v[i1]), expect);
                    if (f >= 0) { tri.Add(i0); tri.Add(i1); tri.Add(i2); tri.Add(i1); tri.Add(i3); tri.Add(i2); }
                    else { tri.Add(i0); tri.Add(i2); tri.Add(i1); tri.Add(i1); tri.Add(i2); tri.Add(i3); }
                }
            }
            Strip(L, Cn, zf, zf, Vector3.forward);
            Strip(Cn, R, zf, zf, Vector3.forward);
            Strip(L, Cn, -zf, -zf, Vector3.back);
            Strip(Cn, R, -zf, -zf, Vector3.back);
            var mid = (L[N / 2] - R[N / 2]).normalized;
            Strip(L, L, zf, -zf, mid);
            Strip(R, R, zf, -zf, -mid);
            var m = new Mesh { name = "ribbon_tail" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return HatMeshes["ribbon_tail"] = m;
        }

        /// <summary>A cat ear (+y up, facing +z): a rounded triangle, cupped in front, the tip tipped back a touch.</summary>
        private static Mesh CatEar()
        {
            return Deformed("cat_ear", 32, 28, v =>
            {
                float u = (v.y + 1) / 2, c = Mathf.Sqrt(Mathf.Max(1e-4f, 1 - v.y * v.y)), cx = v.x / c, cz = v.z / c;
                float w = .2f * Mathf.Pow(1 - u, .8f) + .016f, d = w * .42f;
                float z = cz * d;
                if (cz > 0) z -= .6f * d * Mathf.Exp(-(cx / .6f) * (cx / .6f)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.1f)); // the hollow
                return new Vector3(cx * w + .02f * u * u, u * .3f, z - .05f * u * u);
            });
        }

        /// <summary>A bunny ear (+y up, facing +z): long and rounded, hollowed in front, flopping out and back at the tip.</summary>
        private static Mesh BunnyEar()
        {
            return Deformed("bunny_ear", 28, 36, v =>
            {
                float u = (v.y + 1) / 2, c = Mathf.Sqrt(Mathf.Max(1e-4f, 1 - v.y * v.y)), cx = v.x / c, cz = v.z / c;
                float tip = Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow(Mathf.Max(0, (u - .72f) / .28f), 2)));
                float w = .125f * (.6f + .4f * Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.2f))) * tip, d = w * .42f;
                float z = cz * d;
                if (cz > 0) z -= .55f * d * Mathf.Exp(-(cx / .55f) * (cx / .55f)) * Mathf.Sin(Mathf.PI * u);
                return new Vector3(cx * w + .08f * u * u * u, u * .6f, z - .07f * u * u);
            });
        }

        /// <summary>Puts a leaf (its stalk, blade, paler underside and vein) on a pivot, pointing along +x.</summary>
        private static void LeafOn(Transform pivot, Material top, Material under, Material vein, Material stalk, float size)
        {
            var l = Node.Group(pivot, "blade");
            l.localScale = Vector3.one * size;
            Stick(l, new Vector3(0, 0, 0), new Vector3(.14f, .03f, 0), .035f, stalk);
            var blade = Node.Group(l, "b", .13f, .03f, 0);
            Node.Mesh(blade, LeafSurface(true), top, 0, 0, 0, shadow: false);
            Node.Mesh(blade, LeafSurface(false), under, 0, 0, 0, shadow: false);
            for (int i = 0; i < 9; i++)
            {
                float u0 = .02f + i * .1f, u1 = u0 + .1f;
                Stick(blade, new Vector3(u0, LeafMidY(u0) + .004f, 0), new Vector3(u1, LeafMidY(u1) + .004f, 0), .014f * (1 - u0 * .8f), vein);
            }
        }

        private static float LeafW(float u) { return .44f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(u), .72f))), .85f) * (1 + .03f * Mathf.Sin(u * 40)); }
        private static float LeafLift(float u, float z) { return .55f * z * z + .16f * u - .34f * u * u; } // cupped, arching up then drooping to the tip
        private static float LeafHalf(float u, float s) { return (.006f + .03f * LeafW(u)) * Mathf.Sqrt(Mathf.Max(0, 1 - s * s)); }
        private static float LeafMidY(float u) { return LeafLift(u, 0) + LeafHalf(u, 0) - .012f * Mathf.Sin(Mathf.PI * u); }

        /// <summary>One side of a leaf blade (length 1 along +x): the top has a sunken midrib and side veins.</summary>
        private static Mesh LeafSurface(bool top)
        {
            string key = top ? "leaf_top" : "leaf_under";
            if (HatMeshes.TryGetValue(key, out var cached) && cached != null) return cached;
            const int N = 48, W = 28;
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= W; j++)
                {
                    float u = i / (float)N, s = j / (float)W * 2 - 1, w = LeafW(u), z = s * w;
                    float y = LeafLift(u, z) + (top ? 1 : -1) * LeafHalf(u, s);
                    if (top)
                    {
                        float g = .012f * Mathf.Exp(-(z / .022f) * (z / .022f)) * Mathf.Sin(Mathf.PI * u); // the midrib's groove
                        for (int k = 0; k < 5; k++)
                        {
                            float along = u - (.12f + k * .15f);
                            if (along <= 0) continue;
                            float dist = Mathf.Abs(Mathf.Abs(z) - along * 1.05f);
                            g += .006f * Mathf.Exp(-(dist / .016f) * (dist / .016f)) * Mathf.Clamp01(1.2f - Mathf.Abs(s));
                        }
                        y -= g;
                    }
                    v.Add(new Vector3(u, y, z));
                }
            var expect = top ? Vector3.up : Vector3.down;
            for (int i = 0; i < N; i++)
                for (int j = 0; j < W; j++)
                {
                    int a = i * (W + 1) + j, b = a + 1, c = a + W + 1, d = c + 1;
                    float f = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), expect) + Vector3.Dot(Vector3.Cross(v[d] - v[b], v[c] - v[b]), expect);
                    if (f >= 0) { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c); }
                    else { tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d); }
                }
            var m = new Mesh { name = key };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return HatMeshes[key] = m;
        }

        /// <summary>A rounded rod from a to b.</summary>
        private static Transform Stick(Transform parent, Vector3 a, Vector3 b, float r, Material mat)
        {
            var t = Node.Mesh(parent, Cyl(r, r, Vector3.Distance(a, b), 8), mat, (a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2, shadow: false);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return t;
        }

        /// <summary>Lays a small shape flat on a cone (base radius and height to top radius and height), facing outwards.</summary>
        private static Transform OnCone(Transform parent, float rBase, float yBase, float rTop, float yTop, float angle, float up, Mesh mesh, Material mat, float scale)
        {
            float r = Mathf.Lerp(rBase, rTop, up), y = Mathf.Lerp(yBase, yTop, up);
            var outward = new Vector3(Mathf.Sin(angle), (rBase - rTop) / (yTop - yBase), Mathf.Cos(angle)).normalized;
            var t = Node.Mesh(parent, mesh, mat, Mathf.Sin(angle) * r, y, Mathf.Cos(angle) * r, shadow: false);
            t.localPosition += outward * .01f;
            t.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            t.localScale = Vector3.one * scale;
            return t;
        }

        /// <summary>A colour a little darker (or lighter, above 1).</summary>
        private static string Shade(string hex, float f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return "#" + ColorUtility.ToHtmlStringRGB(new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f)));
        }
    }
}
