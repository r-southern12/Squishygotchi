using Squishy.Runtime.Three;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Plants from around the world (user request, 26 Sep 2026): each style's plant is a species from its region
    /// (StyleData.plant), built chunky and soft to match the furniture. Everything grows from the soil at the origin
    /// of the plant's "top" group, which the game tilts when the plant wilts and sways when it is watered.
    /// </summary>
    public static class PlantModels
    {
        private const float PI = Mathf.PI;

        public static void Build(Transform top, string species)
        {
            switch (species)
            {
                case "bamboo": Bamboo(top); break;
                case "bonsai": Bonsai(top); break;
                case "maple": RoundTree(top, "#6B4A36", .26f, new[] { "#C8553D", "#D9733E", "#B84A36" }, .1f); break;
                case "banana": Banana(top); break;
                case "datepalm": Palm(top, false); break;
                case "cypress": Cypress(top); break;
                case "saguaro": Saguaro(top); break;
                case "snake": Snake(top); break;
                case "olive": Olive(top); break;
                case "spruce": Spruce(top); break;
                case "fuchsia": Fuchsia(top); break;
                case "sunflower": Sunflowers(top); break;
                case "lavender": Lavender(top); break;
                case "fiddle": Fiddle(top); break;
                case "jade": Jade(top); break;
                case "blossom": RoundTree(top, "#6B4A36", .24f, new[] { "#F4B6C8", "#F7C9D6", "#EFA3BA" }, .1f, "#FFFFFF"); break;
                case "coconut": Palm(top, true); break;
                case "airplant": AirPlant(top); break;
                case "flytrap": Flytrap(top); break;
                case "tulip": Tulips(top); break;
                case "monstera": Monstera(top); break;
                case "lemon": RoundTree(top, "#7A5A3E", .2f, new[] { "#6E9C5A", "#5F8E4E", "#7DAA66" }, .085f, null, "#F4D03F"); break;
                case "aloe": Aloe(top); break;
                case "fern": Fern(top); break;
                default: Topiary(top); break;
            }
        }

        // ---------------- helpers ----------------

        /// <summary>A leaf growing from a base point: turned by yaw, leaned out by tilt, then shaped (width, length, thickness).</summary>
        private static Transform Leaf(Transform p, Vector3 at, float yaw, float tilt, float len, float wid, float thick, Material m)
        {
            var yawG = Node.Group(p, "leaf", at.x, at.y, at.z);
            yawG.RotY(yaw);
            var tiltG = Node.Group(yawG, "tilt");
            tiltG.RotX(tilt);
            Node.Mesh(tiltG, Sph(.5f, 10, 6), m, 0, len / 2, 0).Scale(wid, len, thick);
            return tiltG;
        }

        private static Transform Blob(Transform p, float r, Material m, float x, float y, float z, float sy = 1)
        {
            return Node.Mesh(p, Sph(r, 12, 9), m, x, y, z).Scale(1, sy, 1);
        }

        private static Transform Stick(Transform p, float r, float h, Material m, float x, float y, float z, float lean = 0, float yaw = 0)
        {
            var g = Node.Group(p, "stick", x, y, z);
            g.RotY(yaw);
            var t = Node.Group(g, "lean");
            t.RotZ(lean);
            Node.Mesh(t, Cyl(r * .85f, r, h, 8), m, 0, h / 2, 0);
            return t;
        }

        private static Vector3 Tip(float h, float lean, float yaw, Vector3 from)
        {
            var d = new Vector3(-Mathf.Sin(lean) * Mathf.Cos(yaw), Mathf.Cos(lean), Mathf.Sin(lean) * Mathf.Sin(yaw));
            return from + d * h;
        }

        // ---------------- species ----------------

        private static void Topiary(Transform t)
        {
            Node.Mesh(t, RBox(.36f, .22f, .36f, .09f), M("#5F8566"), 0, .11f, 0);
            Node.Mesh(t, RBox(.26f, .2f, .26f, .08f), M("#8FAE7E"), 0, .29f, 0);
            Node.Mesh(t, RBox(.16f, .16f, .16f, .06f), M("#A7C08E"), 0, .44f, 0);
        }

        private static void Bamboo(Transform t)
        {
            Material cane = M("#8DB85A"), node = M("#6E9A45"), leaf = M("#6FAE5A");
            var canes = new[] { new Vector3(-.05f, .52f, .02f), new Vector3(.05f, .42f, -.03f), new Vector3(0, .34f, .06f) };
            for (int i = 0; i < canes.Length; i++)
            {
                var c = canes[i];
                Node.Mesh(t, Cyl(.024f, .026f, c.y, 8), cane, c.x, c.y / 2, c.z);
                for (float y = .1f; y < c.y; y += .11f) Node.Mesh(t, Cyl(.03f, .03f, .014f, 8), node, c.x, y, c.z);
                for (int k = 0; k < 3; k++) Leaf(t, new Vector3(c.x, c.y - .02f - k * .06f, c.z), k * 2.2f + i, 1.1f, .14f, .045f, .01f, leaf);
            }
        }

        private static void Bonsai(Transform t)
        {
            Material bark = M("#6B4A36"), pad = M("#4F7A4A"), pad2 = M("#648C57");
            Stick(t, .035f, .16f, bark, 0, 0, 0, .35f);
            Stick(t, .028f, .16f, bark, -.055f, .14f, 0, -.55f);
            Stick(t, .02f, .12f, bark, -.02f, .2f, 0, .9f, 0);
            Blob(t, .12f, pad, .09f, .33f, 0, .42f);
            Blob(t, .1f, pad2, -.1f, .26f, .03f, .45f);
            Blob(t, .08f, pad, .12f, .2f, -.04f, .45f);
            Blob(t, .07f, pad2, -.02f, .4f, -.02f, .5f);
        }

        private static void RoundTree(Transform t, string barkHex, float trunkH, string[] leaves, float r, string flowers = null, string fruit = null)
        {
            Stick(t, .028f, trunkH, M(barkHex), 0, 0, 0, .08f);
            var spots = new[] { new Vector3(0, trunkH + .08f, 0), new Vector3(.09f, trunkH + .02f, .03f), new Vector3(-.09f, trunkH + .03f, -.02f), new Vector3(.02f, trunkH + .02f, .09f), new Vector3(-.02f, trunkH + .05f, -.09f), new Vector3(.04f, trunkH + .15f, -.02f) };
            for (int i = 0; i < spots.Length; i++) Blob(t, r * (i == 0 ? 1.15f : 1), M(leaves[i % leaves.Length]), spots[i].x, spots[i].y, spots[i].z);
            if (flowers != null)
                for (int i = 0; i < 9; i++) Blob(t, .018f, M(flowers), Mathf.Cos(i * 2.3f) * .13f, trunkH + .02f + (i % 3) * .07f, Mathf.Sin(i * 2.3f) * .13f);
            if (fruit != null)
                for (int i = 0; i < 5; i++) Blob(t, .026f, M(fruit), Mathf.Cos(i * 1.3f) * .14f, trunkH - .01f + (i % 2) * .08f, Mathf.Sin(i * 1.3f) * .14f, 1.3f);
        }

        private static void Banana(Transform t)
        {
            Node.Mesh(t, Cyl(.04f, .05f, .18f, 10), M("#8FA870"), 0, .09f, 0);
            Material a = M("#6FAE5A"), b = M("#85BE6A");
            for (int i = 0; i < 6; i++) Leaf(t, new Vector3(0, .16f, 0), i * PI / 3 + .3f, .55f + (i % 2) * .35f, .34f, .12f, .012f, i % 2 == 0 ? a : b);
        }

        private static void Palm(Transform t, bool coconut)
        {
            Material bark = M(coconut ? "#9C7A55" : "#8A6A48"), bark2 = M(coconut ? "#B08C63" : "#A07C57"), frond = M(coconut ? "#5E9E52" : "#6E9A55");
            float lean = coconut ? .22f : 0;
            var p = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                Stick(t, .038f - i * .002f, .075f, i % 2 == 0 ? bark : bark2, p.x, p.y, p.z, lean * (i / 6f));
                p = Tip(.07f, lean * (i / 6f), 0, p);
            }
            for (int i = 0; i < 8; i++) Leaf(t, p, i * PI / 4, 1.2f + (i % 2) * .25f, .26f, .07f, .01f, frond);
            if (coconut) for (int i = 0; i < 3; i++) Blob(t, .03f, M("#7A5A3A"), p.x + Mathf.Cos(i * 2.1f) * .035f, p.y - .03f, p.z + Mathf.Sin(i * 2.1f) * .035f);
            else for (int i = 0; i < 2; i++) Blob(t, .035f, M("#D98B3A"), p.x + (i == 0 ? .04f : -.04f), p.y - .05f, p.z, 1.4f);
        }

        private static void Cypress(Transform t)
        {
            Node.Mesh(t, Cyl(.02f, .025f, .06f, 6), M("#6B4A36"), 0, .03f, 0);
            Blob(t, .5f, M("#3F6B45"), 0, .33f, 0).Scale(.15f, .6f, .15f);
            Blob(t, .5f, M("#4B7A50"), .02f, .4f, .02f).Scale(.11f, .42f, .11f);
        }

        private static void Saguaro(Transform t)
        {
            Material g = M("#5E9C5A"), g2 = M("#6FAE66");
            Node.Mesh(t, Cyl(.055f, .06f, .4f, 12), g, 0, .2f, 0);
            Blob(t, .055f, g, 0, .4f, 0);
            foreach (float sx in new[] { -1f, 1f })
            {
                float y = sx < 0 ? .16f : .22f;
                Node.Mesh(t, Cyl(.032f, .032f, .07f, 10), g2, sx * .075f, y, 0).RotZ(PI / 2);
                Node.Mesh(t, Cyl(.032f, .032f, .13f, 10), g2, sx * .11f, y + .065f, 0);
                Blob(t, .032f, g2, sx * .11f, y + .13f, 0);
            }
            Blob(t, .02f, M("#F2A7C3"), 0, .46f, 0);
        }

        private static void Snake(Transform t)
        {
            Material a = M("#4E7D4A"), b = M("#7FA85E");
            for (int i = 0; i < 7; i++)
                Leaf(t, new Vector3(Mathf.Cos(i * 2.4f) * .05f, 0, Mathf.Sin(i * 2.4f) * .05f), i * 2.4f, .12f + (i % 3) * .05f, .3f + (i % 3) * .06f, .07f, .02f, i % 2 == 0 ? a : b);
        }

        private static void Olive(Transform t)
        {
            Material bark = M("#8A7A62"), l1 = M("#9CAF88"), l2 = M("#8AA27A"), fruit = M("#4A3A4E");
            Stick(t, .03f, .15f, bark, 0, 0, 0, .3f);
            Stick(t, .024f, .14f, bark, -.045f, .13f, 0, -.45f);
            foreach (var s in new[] { new Vector3(.02f, .34f, 0), new Vector3(-.1f, .3f, .03f), new Vector3(.1f, .28f, -.03f), new Vector3(0, .27f, .09f) })
                Blob(t, .09f, s.x > 0 ? l1 : l2, s.x, s.y, s.z, .7f);
            for (int i = 0; i < 6; i++) Blob(t, .014f, fruit, Mathf.Cos(i * 1.9f) * .12f, .26f + (i % 2) * .05f, Mathf.Sin(i * 1.9f) * .12f, 1.3f);
        }

        private static void Spruce(Transform t)
        {
            Node.Mesh(t, Cyl(.022f, .026f, .08f, 6), M("#6B4A36"), 0, .04f, 0);
            Material a = M("#3E6B4E"), b = M("#4F7D5C");
            Node.Mesh(t, Cyl(0, .17f, .2f, 10), a, 0, .16f, 0);
            Node.Mesh(t, Cyl(0, .13f, .18f, 10), b, 0, .29f, 0);
            Node.Mesh(t, Cyl(0, .09f, .16f, 10), a, 0, .41f, 0);
        }

        private static void Fuchsia(Transform t)
        {
            Material leaf = M("#5F8566"), leaf2 = M("#728F6A"), petal = M("#E0457B"), skirt = M("#7B3F8C");
            Node.Mesh(t, Cyl(.012f, .016f, .14f, 6), M("#6B4A36"), 0, .07f, 0);
            foreach (var s in new[] { new Vector3(0, .3f, 0), new Vector3(-.08f, .24f, .03f), new Vector3(.08f, .25f, -.02f), new Vector3(.01f, .23f, .08f) })
                Blob(t, .085f, s.x < 0 ? leaf2 : leaf, s.x, s.y, s.z, .8f);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.2f, x = Mathf.Cos(a) * .13f, z = Mathf.Sin(a) * .13f, y = .22f + (i % 3) * .04f;
                Node.Mesh(t, Cyl(.004f, .004f, .05f, 4), leaf, x, y - .02f, z);
                Node.Mesh(t, Cyl(.028f, 0, .04f, 8), petal, x, y - .06f, z);
                Node.Mesh(t, Cyl(.018f, .012f, .025f, 8), skirt, x, y - .09f, z);
            }
        }

        private static void Sunflowers(Transform t)
        {
            Material stem = M("#6E9A45"), petals = M("#F2C230"), centre = M("#6B4226");
            var heads = new[] { new Vector3(0, .46f, 0), new Vector3(-.07f, .36f, .04f), new Vector3(.08f, .3f, -.02f) };
            foreach (var h in heads)
            {
                Node.Mesh(t, Cyl(.01f, .012f, h.y, 6), stem, h.x, h.y / 2, h.z);
                Leaf(t, new Vector3(h.x, h.y * .45f, h.z), h.x * 20, 1.1f, .09f, .06f, .01f, stem);
                var head = Node.Group(t, "head", h.x, h.y, h.z + .01f);
                head.RotX(PI / 2 - .35f);
                Node.Mesh(head, Cyl(.075f, .075f, .015f, 14), petals, 0, 0, 0);
                Node.Mesh(head, Cyl(.038f, .038f, .022f, 12), centre, 0, .006f, 0);
            }
        }

        private static void Lavender(Transform t)
        {
            Blob(t, .1f, M("#8FA88F"), 0, .04f, 0, .5f);
            Material stem = M("#8FA88F"), bloom = M("#9A7CC4"), bloom2 = M("#8266B3");
            for (int i = 0; i < 13; i++)
            {
                float a = i * 2.4f, lean = .1f + (i % 4) * .08f, h = .24f + (i % 3) * .05f;
                var s = Stick(t, .005f, h, stem, Mathf.Cos(a) * .03f, .02f, Mathf.Sin(a) * .03f, lean, a);
                Node.Mesh(s, Sph(.5f, 8, 6), i % 2 == 0 ? bloom : bloom2, 0, h, 0).Scale(.025f, .08f, .025f);
            }
        }

        private static void Fiddle(Transform t)
        {
            Material stem = M("#6B5A3E"), leaf = M("#4F7F4F"), leaf2 = M("#5E8E58");
            Node.Mesh(t, Cyl(.016f, .02f, .46f, 8), stem, 0, .23f, 0);
            for (int i = 0; i < 7; i++)
                Leaf(t, new Vector3(0, .12f + i * .055f, 0), i * 2.3f, .75f - i * .06f, .17f, .12f, .015f, i % 2 == 0 ? leaf : leaf2);
        }

        private static void Jade(Transform t)
        {
            Material stem = M("#7A6A4A"), leaf = M("#6FA37A"), tipM = M("#B8575A");
            Stick(t, .022f, .14f, stem, 0, 0, 0, .25f);
            Stick(t, .02f, .12f, stem, 0, .06f, 0, -.5f);
            for (int i = 0; i < 14; i++)
            {
                float a = i * 2.4f, r = .06f + (i % 3) * .035f, y = .12f + (i % 4) * .035f;
                var l = Blob(t, .5f, leaf, Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                l.localScale = new Vector3(.065f, .035f, .085f);
                l.RotY(a);
            }
            Blob(t, .012f, tipM, .08f, .2f, .02f);
        }

        private static void AirPlant(Transform t)
        {
            // A little air plant resting on a pebble: silvery spikes and a pink bloom spike.
            Blob(t, .06f, M("#B8B2A8"), 0, .03f, 0, .6f);
            Material leaf = M("#A9C4A0"), leaf2 = M("#94B28C");
            for (int i = 0; i < 12; i++) Leaf(t, new Vector3(0, .06f, 0), i * .52f, .6f + (i % 3) * .3f, .16f, .025f, .012f, i % 2 == 0 ? leaf : leaf2);
            Node.Mesh(t, Cyl(0, .022f, .12f, 8), M("#E86A92"), 0, .13f, 0);
        }

        private static void Flytrap(Transform t)
        {
            Material stalk = M("#6E9A45"), outer = M("#7FAE55"), inner = M("#C8412F"), teeth = M("#E8F2DA");
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f;
                var s = Stick(t, .008f, .12f + (i % 2) * .05f, stalk, 0, 0, 0, .5f, a);
                float h = .12f + (i % 2) * .05f;
                var trap = Node.Group(s, "trap", 0, h + .02f, 0);
                foreach (float side in new[] { -1f, 1f })
                {
                    var jaw = Node.Group(trap, "jaw");
                    jaw.RotZ(side * .45f);
                    Node.Mesh(jaw, Sph(.5f, 10, 6), outer, side * .025f, 0, 0).Scale(.05f, .018f, .06f);
                    Node.Mesh(jaw, Sph(.5f, 10, 6), inner, side * .022f, .004f, 0).Scale(.04f, .01f, .05f);
                    for (int k = 0; k < 4; k++) Node.Mesh(jaw, Cyl(.003f, .003f, .02f, 4), teeth, side * .05f, .006f, -.022f + k * .015f);
                }
            }
        }

        private static void Tulips(Transform t)
        {
            Material stem = M("#6E9A45"), leaf = M("#7FAE55");
            string[] cols = { "#D8412F", "#F2C230", "#F29CB8", "#E86A92", "#F4EFE4" };
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f, h = .26f + (i % 3) * .06f;
                var s = Stick(t, .007f, h, stem, Mathf.Cos(a) * .04f, 0, Mathf.Sin(a) * .04f, .12f, a);
                Node.Mesh(s, Sph(.5f, 10, 8), M(cols[i]), 0, h + .02f, 0).Scale(.055f, .075f, .055f);
                Leaf(t, new Vector3(Mathf.Cos(a) * .04f, 0, Mathf.Sin(a) * .04f), a, .3f, .15f, .045f, .01f, leaf);
            }
        }

        private static void Monstera(Transform t)
        {
            Material stalk = M("#5E8E58"), leaf = M("#3F7A4F"), leaf2 = M("#4E8A5C"), split = M("#2F5E3C");
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f, lean = .5f + (i % 2) * .3f, h = .22f + (i % 3) * .07f;
                var s = Stick(t, .008f, h, stalk, 0, 0, 0, lean, a);
                var l = Node.Group(s, "leaf", 0, h, 0);
                l.RotX(-.6f);
                Node.Mesh(l, Sph(.5f, 12, 6), i % 2 == 0 ? leaf : leaf2, 0, .06f, 0).Scale(.19f, .15f, .02f);
                for (int k = 0; k < 3; k++) Node.Mesh(l, RBox(.07f, .01f, .022f, .004f), split, (k - 1) * .05f + .04f, .05f + k * .02f, 0).RotZ(.4f);
            }
        }

        private static void Aloe(Transform t)
        {
            Material a = M("#7FA88A"), b = M("#90B89A");
            for (int i = 0; i < 9; i++)
            {
                var s = Node.Group(t, "blade", 0, .02f, 0);
                s.RotY(i * .7f);
                var tl = Node.Group(s, "tilt");
                tl.RotX(.3f + (i % 3) * .18f);
                Node.Mesh(tl, Cyl(0, .03f, .28f - (i % 3) * .04f, 6), i % 2 == 0 ? a : b, 0, .13f, 0).Scale(1, 1, .5f);
            }
        }

        private static void Fern(Transform t)
        {
            Material a = M("#7BB661"), b = M("#6AA553");
            for (int i = 0; i < 12; i++)
            {
                var frond = Leaf(t, new Vector3(0, .05f, 0), i * .52f, .9f + (i % 2) * .35f, .26f, .07f, .01f, i % 2 == 0 ? a : b);
                // Little leaflet bumps along each frond.
                for (int k = 0; k < 4; k++) Node.Mesh(frond, Sph(.5f, 6, 4), i % 2 == 0 ? b : a, 0, .05f + k * .05f, .006f).Scale(.1f - k * .015f, .018f, .01f);
            }
        }
    }
}
