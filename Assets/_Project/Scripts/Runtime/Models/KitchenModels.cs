using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>toolModel, foodModel, dishModel, snackModel and the stove's utensil rack.</summary>
    public static class KitchenModels
    {
        private const float PI = Mathf.PI;

        public static Transform Tool(GameContent c, GameState s, int i, int? skinIdx, Transform parent)
        {
            var t = c.tools[i];
            var sk = c.toolSkins[skinIdx ?? s.toolSkin[i]];
            var g = Node.Group(parent, "tool" + i);
            Material col = M(string.IsNullOrEmpty(sk.col) ? t.color : sk.col), w = M(sk.name == "Gold" ? "#B8913A" : "#8A5D3B");
            switch (i)
            {
                case 0:
                    Node.Mesh(g, RBox(.05f, .02f, .4f, .01f), w, 0, .02f, .1f);
                    Node.Mesh(g, RBox(.16f, .02f, .16f, .02f), col, 0, .02f, -.16f);
                    break;
                case 1:
                    Node.Mesh(g, RBox(.28f, .02f, .18f, .01f), col, 0, .02f, -.05f);
                    Node.Mesh(g, RBox(.06f, .03f, .18f, .015f), w, 0, .02f, .16f);
                    break;
                case 2:
                    foreach (var x in new[] { -.05f, 0f, .05f }) Node.Mesh(g, RBox(.025f, .015f, .34f, .006f), col, x, .02f, 0);
                    break;
                case 3:
                    Node.Mesh(g, Sph(.2f, 16, 8), col, 0, .2f, 0).ScaleY(.45f);
                    Node.Mesh(g, RBox(.3f, .03f, .04f, .01f), w, .3f, .14f, 0);
                    break;
                case 4:
                    Node.Mesh(g, Cyl(.06f, .06f, .4f, 12), col, 0, .06f, 0).RotZ(PI / 2);
                    foreach (var x in new[] { -.25f, .25f }) Node.Mesh(g, Cyl(.025f, .025f, .12f, 8), w, x, .06f, 0).RotZ(PI / 2);
                    break;
                case 5:
                    Node.Mesh(g, Cyl(.18f, .18f, .12f, 18), col, 0, .06f, 0);
                    Node.Mesh(g, Cyl(.19f, .16f, .08f, 18), M("#A97E47"), 0, .16f, 0);
                    break;
                case 6:
                    Node.Mesh(g, RBox(.04f, .02f, .36f, .01f), col, 0, .1f, .06f);
                    Node.Mesh(g, Sph(.08f, 12, 8), col, 0, .06f, -.16f).ScaleY(.5f);
                    break;
                default:
                    foreach (var x in new[] { -.03f, .03f }) Node.Mesh(g, Cyl(.012f, .016f, .4f, 6), col, x, .02f, 0).RotX(PI / 2);
                    break;
            }
            return g;
        }

        public static Transform Food(GameContent c, int i, Transform parent)
        {
            var f = c.pantry[i];
            var g = Node.Group(parent, "food" + i);
            Material col = M(f.color), gr = M("#7DBA5E");
            // Shapes come from the data, so new ingredients reuse them without code.
            switch (f.shape)
            {
                case "bag":
                    Node.Mesh(g, RBox(.2f, .24f, .14f, .05f), col, 0, .12f, 0);
                    Node.Mesh(g, RBox(.14f, .06f, .02f, .01f), M("#C8674E"), 0, .14f, .075f);
                    break;
                case "head":
                    Node.Mesh(g, Sph(.14f, 14, 10), col, 0, .13f, 0);
                    Node.Mesh(g, Sph(.1f, 12, 8), M("#C9E0A6"), 0, .2f, .04f);
                    break;
                case "stalks":
                    for (int k = 0; k < 3; k++) Node.Mesh(g, Cyl(.015f, .015f, .34f, 6), gr, -.03f + k * .03f, .17f, 0);
                    Node.Mesh(g, Cyl(.035f, .03f, .06f, 8), M("#F4EFE4"), 0, .02f, 0);
                    break;
                case "knobs":
                    foreach (var q in new[] { new Vector2(0, 0), new Vector2(.07f, .03f), new Vector2(-.06f, .04f) }) Node.Mesh(g, Sph(.06f, 10, 8), col, q.x, .05f, q.y).Scale(1.2f, .8f, 1);
                    break;
                case "cap":
                    Node.Mesh(g, Cyl(.035f, .045f, .1f, 8), M("#F1E6D0"), 0, .05f, 0);
                    Node.Mesh(g, Sph(.1f, 14, 10), col, 0, .12f, 0).ScaleY(.6f);
                    break;
                case "prawn":
                    Node.Mesh(g, Torus(.09f, .04f, 8, 16, PI * 1.4f), col, 0, .05f, 0).RotX(PI / 2);
                    break;
                case "block":
                    Node.Mesh(g, RBox(.18f, .12f, .18f, .02f), col, 0, .06f, 0);
                    break;
                case "egg":
                    Node.Mesh(g, Sph(.08f, 14, 10), col, 0, .1f, 0).ScaleY(1.25f);
                    break;
                case "pot":
                    Node.Mesh(g, Cyl(.1f, .08f, .1f, 14), M("#8A5D3B"), 0, .05f, 0);
                    Node.Mesh(g, Cyl(.09f, .09f, .02f, 14), col, 0, .1f, 0);
                    break;
                case "root":
                    Node.Mesh(g, Cyl(.01f, .05f, .3f, 10), col, 0, .05f, 0).RotZ(PI / 2);
                    Node.Mesh(g, Sph(.04f, 6, 5), gr, -.17f, .05f, 0);
                    break;
                case "leafy":
                    for (int k = 0; k < 4; k++)
                    {
                        var l = Node.Mesh(g, Sph(.07f, 10, 8), k % 2 != 0 ? gr : M("#A7D08E"), Mathf.Cos(k * 1.6f) * .04f, .12f, Mathf.Sin(k * 1.6f) * .04f);
                        l.Scale(.7f, 1.5f, .4f).RotY(k * 1.6f);
                    }
                    Node.Mesh(g, Cyl(.06f, .05f, .06f, 10), M("#E8F2DA"), 0, .03f, 0);
                    break;
                case "meat":
                    Node.Mesh(g, RBox(.24f, .08f, .16f, .03f), col, 0, .04f, 0);
                    Node.Mesh(g, RBox(.24f, .025f, .16f, .01f), M("#F8EDE6"), 0, .09f, 0);
                    break;
                case "bottle":
                    Node.Mesh(g, Cyl(.06f, .06f, .18f, 12), col, 0, .09f, 0);
                    Node.Mesh(g, Cyl(.025f, .04f, .06f, 10), col, 0, .21f, 0);
                    Node.Mesh(g, Cyl(.028f, .028f, .03f, 10), M("#C8412F"), 0, .255f, 0);
                    Node.Mesh(g, Cyl(.061f, .061f, .06f, 12), M("#F4EFE4"), 0, .09f, 0);
                    break;
                case "wedge":
                    Node.Mesh(g, Cyl(.13f, .13f, .1f, 3), col, 0, .05f, 0);
                    Node.Mesh(g, Sph(.02f, 6, 5), M("#D9A83A"), .03f, .1f, .01f);
                    break;
                case "beans":
                    for (int k = 0; k < 7; k++) Node.Mesh(g, Sph(.035f, 8, 6), col, Mathf.Cos(k * 2.4f) * .05f * (k % 3), .035f + (k % 2) * .03f, Mathf.Sin(k * 2.4f) * .05f * (k % 3));
                    break;
                default:
                    Node.Mesh(g, Cyl(.01f, .04f, .24f, 10), col, 0, .04f, 0).RotZ(PI / 2.3f);
                    Node.Mesh(g, Cyl(.02f, .02f, .05f, 6), gr, .13f, .09f, 0);
                    break;
            }
            return g;
        }

        public static Transform Dish(GameContent c, int i, Transform parent)
        {
            var rc = c.recipes[i];
            var g = Node.Group(parent, "dish" + i);
            Node.Mesh(g, Cyl(.2f, .13f, .12f, 18), M("#EFE2C9"), 0, .06f, 0);
            Node.Mesh(g, Cyl(.18f, .18f, .02f, 18), M(rc.col), 0, .12f, 0);
            for (int k = 0; k < rc.ing.Length; k++)
                Node.Mesh(g, Sph(.06f, 10, 8), M(c.pantry[rc.ing[k]].color), Mathf.Cos(k * 2.1f) * .08f, .14f, Mathf.Sin(k * 2.1f) * .08f).ScaleY(.7f);
            if (rc.ing.Length == 0) Node.Mesh(g, Sph(.12f, 14, 10), M("#F7F1E3"), 0, .12f, 0).ScaleY(.35f);
            return g;
        }

        public static Transform Snack(GameContent c, int i, Transform parent)
        {
            var sn = c.snacks[i];
            var g = Node.Group(parent, "snack" + i);
            var col = M(sn.color);
            if (sn.shape == "crackers")
            {
                Node.Mesh(g, Cyl(.12f, .12f, .03f, 14), col, 0, .02f, 0);
                Node.Mesh(g, Cyl(.12f, .12f, .03f, 14), col, .03f, .05f, .02f);
            }
            else if (sn.shape == "slices")
            {
                for (int k = 0; k < 3; k++)
                {
                    float x = Mathf.Cos(k * 2.1f) * .06f, z = Mathf.Sin(k * 2.1f) * .06f;
                    Node.Mesh(g, Sph(.08f, 10, 6), col, x, .04f, z).Scale(1, .5f, .45f).RotY(k * 2.1f);
                    Node.Mesh(g, Sph(.05f, 8, 6), M("#F6ECD2"), x, .05f, z + .01f);
                }
            }
            else if (sn.shape == "tart")
            {
                Node.Mesh(g, Cyl(.1f, .08f, .05f, 16), M("#D9A25E"), 0, .025f, 0);
                Node.Mesh(g, Cyl(.085f, .085f, .012f, 16), col, 0, .052f, 0);
            }
            else
            {
                Node.Mesh(g, Sph(.08f, 14, 10), col, 0, .07f, 0).ScaleY(.8f);
                Node.Mesh(g, Cyl(.1f, .1f, .01f, 12), M("#8FAE7E"), 0, .005f, 0);
            }
            return g;
        }

        /// <summary>decorateStove: a rack with every tool you own, a pot at level 2, a hood at level 4.</summary>
        public static void DecorateStove(GameRules rules, Transform stove, ItemParts parts, string styleId)
        {
            if (parts.rack != null) Node.Destroy(parts.rack);
            var c = rules.C;
            var r = Node.Group(stove, "rack", 0, .52f, -.21f);
            var bar = M("#3B2A20");
            Node.Mesh(r, RBox(.56f, .035f, .035f, .012f), bar, 0, .34f, 0);
            foreach (var x in new[] { -.27f, .27f }) Node.Mesh(r, Cyl(.012f, .012f, .34f, 6), bar, x, .17f, 0);
            int k = 0;
            for (int i = 0; i < c.tools.Length; i++)
            {
                if (!rules.Owned(rules.ToolKey(i))) continue;
                var tm = Tool(c, rules.S, i, null, r);
                tm.localScale = Vector3.one * .3f;
                tm.RotX(PI / 2);
                tm.localPosition = new Vector3(-.21f + k * .06f, .24f, .02f);
                k++;
            }
            int lv = rules.KitchenLvl();
            if (lv >= 2) Node.Mesh(r, Cyl(.075f, .065f, .09f, 12), M("#8A5D3B"), .14f, .02f, .26f);
            var st = c.Style(styleId);
            if (lv >= 4) Node.Mesh(r, RBox(.6f, .08f, .3f, .03f), M(st != null ? st.pal[1] : "#C8674E"), 0, .62f, .12f);
            parts.rack = r;
        }
    }
}
