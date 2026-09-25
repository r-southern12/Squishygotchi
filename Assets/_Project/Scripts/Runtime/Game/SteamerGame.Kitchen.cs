using System.Collections.Generic;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.World;
using UnityEngine;
using static Squishy.Runtime.Game.Ease;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// A professional kitchen behind the reveal counter, softly out of focus and gently looping:
    /// steel range with flickering burners, steaming stock pots, extractor hood, swaying utensil rail,
    /// shelves and warm pendant lights. Also the fresh-steamer swap after each reveal.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private readonly List<Transform> flames = new List<Transform>(), swingers = new List<Transform>();
        private readonly List<Vector3> potTops = new List<Vector3>();
        private ParticlePool bgSteam;
        private float bgSteamT, boxSlide;

        private void BuildKitchen()
        {
            var k = Node.Group(unbox, "Kitchen");
            Material steel = M("#C3C9CD"), steelDark = M("#8E979D"), top = M("#DCE0E2"), black = M("#34383B"), wood = M("#A87A4F");
            const float Z = -3.3f;
            // Back range and counter, steel, running the width of the view.
            Node.Mesh(k, RBox(16, 1.1f, 1.2f, .06f), steelDark, 0, .55f, Z, shadow: false);
            Node.Mesh(k, RBox(16, .08f, 1.3f, .03f), top, 0, 1.12f, Z, shadow: false);
            for (int i = -3; i <= 3; i++) Node.Mesh(k, RBox(.06f, .9f, .02f, .01f), steel, i * 2.2f, .5f, Z + .61f, shadow: false);
            for (int i = -3; i <= 3; i++) Node.Mesh(k, RBox(.5f, .04f, .03f, .01f), black, i * 2.2f + 1.1f, .85f, Z + .62f, shadow: false);
            // Burners, flames and stock pots.
            var flameMat = Basic(Lin("#FFB14D"), .85f, Blend.Additive, null, false, false);
            float[] bx = { -3.4f, -1.8f, 1.8f, 3.4f };
            for (int i = 0; i < bx.Length; i++)
            {
                Node.Mesh(k, Cyl(.3f, .3f, .04f, 16), black, bx[i], 1.18f, Z, shadow: false);
                var fl = Node.Mesh(k, Cyl(.02f, .16f, .16f, 12), flameMat, bx[i], 1.26f, Z, shadow: false);
                flames.Add(fl);
                bool big = i % 2 == 0;
                float h = big ? .7f : .45f, r = big ? .42f : .34f;
                Node.Mesh(k, Cyl(r, r * .95f, h, 20), steel, bx[i], 1.2f + h / 2, Z, shadow: false);
                Node.Mesh(k, Cyl(r * 1.02f, r * 1.02f, .05f, 20), top, bx[i], 1.2f + h + .02f, Z, shadow: false);
                Node.Mesh(k, Cyl(.05f, .05f, .08f, 8), black, bx[i], 1.2f + h + .08f, Z, shadow: false);
                potTops.Add(new Vector3(bx[i], 1.25f + h, Z));
            }
            // Extractor hood.
            Node.Mesh(k, RBox(9, .9f, 1.5f, .08f), steel, 0, 3.3f, Z + .1f, shadow: false);
            Node.Mesh(k, RBox(9.2f, .12f, 1.6f, .04f), steelDark, 0, 2.82f, Z + .1f, shadow: false);
            Node.Mesh(k, RBox(2, 2, 1, .06f), steel, 0, 4.6f, Z - .1f, shadow: false);
            // Utensil rail with hanging tools that sway.
            Node.Mesh(k, Cyl(.03f, .03f, 7, 8), steelDark, 0, 2.55f, Z + .75f, shadow: false).RotZ(Mathf.PI / 2);
            for (int i = 0; i < 8; i++)
            {
                var hang = Node.Group(k, "hang", -3.2f + i * .9f, 2.55f, Z + .75f);
                Node.Mesh(hang, Cyl(.012f, .012f, .25f, 5), steelDark, 0, -.13f, 0, shadow: false);
                var tool = KitchenModels.Tool(C, S, i % C.tools.Length, null, hang);
                tool.localScale = Vector3.one * 1.3f;
                tool.RotX(Mathf.PI / 2);
                tool.localPosition = new Vector3(0, -.45f, 0);
                swingers.Add(hang);
            }
            // Shelves with bowls, jars and a tower of bamboo steamers.
            foreach (var sx in new[] { -6.2f, 6.2f })
            {
                for (int s = 0; s < 2; s++)
                {
                    float y = 2 + s * .9f;
                    Node.Mesh(k, RBox(2.6f, .08f, .6f, .03f), wood, sx, y, Z - .2f, shadow: false);
                    for (int j = 0; j < 4; j++)
                    {
                        string col = new[] { "#EFE2C9", "#6E9C9A", "#C8674E", "#D9A64A" }[(j + s) % 4];
                        if ((j + s) % 2 == 0) Node.Mesh(k, Cyl(.2f, .14f, .16f, 14), M(col), sx - .9f + j * .6f, y + .12f, Z - .2f, shadow: false);
                        else Node.Mesh(k, Cyl(.14f, .14f, .38f, 14), M(col), sx - .9f + j * .6f, y + .23f, Z - .2f, shadow: false);
                    }
                }
            }
            var tower = Node.Group(k, "tower", 5.4f, 1.16f, Z + .1f);
            for (int s = 0; s < 3; s++) new SteamerModel(Node.Group(tower, "tier", 0, s * SteamerModel.H * .3f, 0), 24).Group.localScale = Vector3.one * .3f;
            SteamerModel.Lid(tower).localPosition = new Vector3(0, 3 * SteamerModel.H * .3f, 0);
            tower.GetChild(tower.childCount - 1).localScale = Vector3.one * .3f;
            // Warm pendant lights.
            var glow = M("#F6D9A0", "#FFD58A");
            foreach (var lx in new[] { -4.5f, 4.5f })
            {
                Node.Mesh(k, Cyl(.01f, .01f, 1.5f, 4), black, lx, 4.3f, Z + 1.2f, shadow: false);
                Node.Mesh(k, Cyl(.18f, .45f, .4f, 16), M("#3F4447"), lx, 3.4f, Z + 1.2f, shadow: false);
                Node.Mesh(k, Sph(.16f, 12, 8), glow, lx, 3.25f, Z + 1.2f, shadow: false);
            }
            bgSteam = new ParticlePool(60, Ico1(), SteamMat(), false, UnboxLayer);
        }

        /// <summary>The kitchen's gentle loop: flickering flames, rising steam, swaying utensils.</summary>
        private void StepKitchen(float dt)
        {
            for (int i = 0; i < flames.Count; i++)
            {
                float f = 1 + .18f * Mathf.Sin(time * 11 + i * 1.7f) + .1f * Mathf.Sin(time * 23 + i);
                flames[i].localScale = new Vector3(1, f, 1);
            }
            for (int i = 0; i < swingers.Count; i++) swingers[i].RotZ(.06f * Mathf.Sin(time * .9f + i * .8f));
            bgSteamT -= dt;
            if (bgSteamT <= 0)
            {
                bgSteamT = .12f;
                var p = potTops[Random.Range(0, potTops.Count)];
                bgSteam.Spawn(p + new Vector3(Rnd(-.2f, .2f), 0, Rnd(-.2f, .2f)), new Vector3(Rnd(-.1f, .1f), Rnd(.5f, .9f), 0), Rnd(.18f, .3f), Rnd(1.8f, 2.6f), .6f, .15f);
            }
            bgSteam.Update(dt, true, cam);
        }

        /// <summary>After a reveal: the opened steamer slides away and a fresh lidded one slides in.</summary>
        private void StartSwap()
        {
            ustate = "swap";
            ust = 0;
            ui.ShowHud(true);
            ui.SetHint("");
            ui.SetRing(0);
            lidOn = false;
            lid.gameObject.SetActive(false);
            sfx.Lift();
        }

        private void StepSwap()
        {
            const float Out = .55f, In = .75f, D = 11f;
            if (ust < Out) { boxSlide = D * InOutCubic(ust / Out); return; }
            if (!lid.gameObject.activeSelf) { lid.gameObject.SetActive(true); lidR = Vector3.zero; }
            float k = Mathf.Min(1, (ust - Out) / In);
            boxSlide = -D * (1 - InOutCubic(k));
            lidP = LidRest + new Vector3(boxSlide, 0, 0);
            if (k >= 1) { boxSlide = 0; lidP = LidRest; LidLanded(); }
        }
    }
}
