using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>Life stages (baby cowlick, elder eyebrows, tint, size, bounce) and prestige cosmetics (hats, glasses).</summary>
    public sealed partial class SquishyModel
    {
        public float StageScale = 1, StageBounce = 1;
        private float _eyeW = 1;
        private GameRules.Life _stage = GameRules.Life.Young;
        private Transform _cowlick, _brows, _hat, _face;
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
                var top = ShapeAt(Vector3.up);
                _cowlick.localPosition = top + new Vector3(0, .02f, 0);
                Node.Mesh(_cowlick, Torus(.07f, .022f, 6, 16, Mathf.PI * 1.5f), Mat, 0, .06f, 0, shadow: false, receive: true).RotY(Mathf.PI / 2);
                // Soft eyebrows for elders.
                _brows = Node.Group(Body, "brows");
                var browMat = M("#EDE6DA");
                for (int k = 0; k < 2; k++)
                {
                    float sx = k == 0 ? -1 : 1;
                    var d = new Vector3(sx * .3f, .42f, .86f).normalized;
                    var p = ShapeAt(d);
                    var b = Node.Mesh(_brows, RBox(.16f, .035f, .05f, .015f), browMat, 0, 0, 0, shadow: false);
                    b.localPosition = p + (p - new Vector3(0, -.1f, 0)).normalized * .015f;
                    b.localRotation = Quaternion.FromToRotation(Vector3.forward, (p - new Vector3(0, -.1f, 0)).normalized) * Quaternion.AngleAxis(sx * -12, Vector3.forward);
                }
            }
            _cowlick.gameObject.SetActive(stage == GameRules.Life.Baby);
            _brows.gameObject.SetActive(stage == GameRules.Life.Elder);
            _eyeW = stage == GameRules.Life.Baby ? 1.2f : 1f; // bigger eyes for babies
            ThreeMat.SetOpacity(_blush, stage == GameRules.Life.Elder ? .7f : Fin != null && Fin.tier == "Galaxy" ? .35f : .5f);
            _g = -1;
        }

        /// <summary>Wears the equipped prestige hat and face accessory (or none).</summary>
        public void SetCosmetics(CosmeticData hat, CosmeticData face)
        {
            if (_hat != null) Node.Destroy(_hat);
            if (_face != null) Node.Destroy(_face);
            _hat = hat != null ? Hat(hat) : null;
            _face = face != null ? Glasses(face) : null;
            int layer = Pivot.gameObject.layer;
            if (_hat != null) Node.SetLayer(_hat, layer);
            if (_face != null) Node.SetLayer(_face, layer);
        }

        private Transform Hat(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            var top = ShapeAt(Vector3.up);
            g.localPosition = top + new Vector3(0, -.04f, 0);
            Material col = M(c.color), accent = M("#FFF7EC");
            switch (c.id)
            {
                case "party_hat":
                    Node.Mesh(g, Cyl(.01f, .22f, .42f, 16), col, 0, .2f, 0, shadow: false);
                    Node.Mesh(g, Sph(.06f, 10, 8), accent, 0, .43f, 0, shadow: false);
                    g.RotZ(-.18f);
                    break;
                case "flower_crown":
                    Node.Mesh(g, Torus(.3f, .035f, 6, 24), M("#8FAE7E"), 0, .02f, 0, shadow: false).RotX(Mathf.PI / 2);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2;
                        Node.Mesh(g, Sph(.06f, 8, 6), M(i % 2 == 0 ? c.color : "#FFF1A8"), Mathf.Cos(a) * .3f, .05f, Mathf.Sin(a) * .3f, shadow: false);
                    }
                    break;
                case "beret":
                    Node.Mesh(g, Sph(.34f, 16, 8), col, .05f, .02f, 0, shadow: false).Scale(1, .28f, 1);
                    Node.Mesh(g, Cyl(.02f, .02f, .06f, 6), col, .05f, .1f, 0, shadow: false);
                    g.RotZ(-.2f);
                    break;
                case "chef_hat":
                    Node.Mesh(g, Cyl(.2f, .2f, .16f, 16), col, 0, .06f, 0, shadow: false);
                    Node.Mesh(g, Sph(.26f, 14, 10), col, 0, .26f, 0, shadow: false).Scale(1, .75f, 1);
                    break;
                default: // bow
                    Node.Mesh(g, Sph(.1f, 10, 8), col, -.1f, .05f, .12f, shadow: false).Scale(1.2f, .8f, .6f);
                    Node.Mesh(g, Sph(.1f, 10, 8), col, .1f, .05f, .12f, shadow: false).Scale(1.2f, .8f, .6f);
                    Node.Mesh(g, Sph(.045f, 8, 6), accent, 0, .05f, .14f, shadow: false);
                    break;
            }
            return g;
        }

        private Transform Glasses(CosmeticData c)
        {
            var g = Node.Group(Body, c.id);
            var mat = M(c.color);
            bool star = c.id == "star_glasses";
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1 : 1;
                var d = new Vector3(sx * .3f, .2f, .93f).normalized;
                var p = ShapeAt(d);
                var n = (p - new Vector3(0, -.1f, 0)).normalized;
                var rim = Node.Mesh(g, star ? Torus(.13f, .022f, 4, 5) : Torus(.12f, .016f, 6, 20), mat, 0, 0, 0, shadow: false);
                rim.localPosition = p + n * .05f;
                rim.localRotation = Quaternion.FromToRotation(Vector3.forward, n) * Quaternion.AngleAxis(star ? 18 : 0, Vector3.forward);
            }
            var bridgeP = ShapeAt(new Vector3(0, .22f, 1).normalized);
            Node.Mesh(g, Cyl(.012f, .012f, .16f, 5), mat, bridgeP.x, bridgeP.y + .02f, bridgeP.z + .05f, shadow: false).RotZ(Mathf.PI / 2);
            return g;
        }
    }
}
