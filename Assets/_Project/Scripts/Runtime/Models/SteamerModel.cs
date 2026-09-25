using System.Collections.Generic;
using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using static Squishy.Runtime.Three.ThreeGeo;
using static Squishy.Runtime.Three.ThreeMat;

namespace Squishy.Runtime.Models
{
    /// <summary>buildSteamer / makeLid: the bamboo steamer room with slats that sink on the camera side, skinnable.</summary>
    public sealed class SteamerModel
    {
        public const float H = 1.35f, DefaultR = 2.3f;

        public readonly Transform Group;
        public readonly Material Liner, Holes;
        public float? Grow; // expansion animation progress, null when idle

        private readonly int _n;
        private readonly float _r;
        private readonly float[] _ang, _h;
        private readonly Transform[] _slat, _cap, _low, _high;
        private readonly Material[] _slatMats;
        private readonly Material _capMat, _bandMat;

        public SteamerModel(Transform parent, int n, float rad = DefaultR)
        {
            _n = n;
            _r = rad;
            float R = rad;
            Group = Node.Group(parent, "Steamer");
            Node.Mesh(Group, Cyl(R + .02f, R + .02f, .14f, 48), M("#A97E47"), 0, .07f, 0);
            Liner = Lambert(Lin("#F2E7D2"));
            Node.Mesh(Group, Cyl(R * .9f, R * .9f, .05f, 48), Liner, 0, .165f, 0);

            // Steam holes in the liner: one combined mesh (they never move).
            var pts = new List<Vector2>();
            for (double x = -R; x <= R; x += .42)
            for (double z = -R; z <= R; z += .42)
            {
                long k = (long)System.Math.Floor(x / .42 + .5);
                double zz = z + (k % 2 != 0 ? .21 : 0);
                if (System.Math.Sqrt(x * x + zz * zz) < R * .82) pts.Add(new Vector2((float)x, (float)zz));
            }
            Holes = Lambert(Lin("#9E7646"));
            var hole = Cyl(.07f, .07f, .052f, 8);
            var combine = new CombineInstance[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                combine[i] = new CombineInstance { mesh = hole, transform = Matrix4x4.Translate(new Vector3(pts[i].x, .167f, pts[i].y)) };
            var holes = new Mesh { name = "holes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            holes.CombineMeshes(combine, true, true);
            Node.Mesh(Group, holes, Holes, 0, 0, 0, shadow: false, receive: true);

            _capMat = Lambert(Lin("#D6AE72"));
            _bandMat = Lambert(Lin("#A97E47"));
            var slatG = RBox(.26f, H, .18f, .06f);
            var capG = RBox(.32f, .14f, .26f, .06f);
            var bandG = RBox(.33f, .16f, .08f, .03f);
            _ang = new float[n];
            _h = new float[n];
            _slat = new Transform[n];
            _cap = new Transform[n];
            _low = new Transform[n];
            _high = new Transform[n];
            _slatMats = new Material[n];
            for (int i = 0; i < n; i++)
            {
                _ang[i] = (float)i / n * Mathf.PI * 2f;
                _h[i] = 1f;
                _slatMats[i] = Lambert(Color.white);
                _slat[i] = Node.Group(Group, "slat");
                Node.Mesh(_slat[i], slatG, _slatMats[i], 0, H / 2f, 0);
                _cap[i] = Node.Mesh(Group, capG, _capMat);
                _low[i] = Node.Mesh(Group, bandG, _bandMat);
                _high[i] = Node.Mesh(Group, bandG, _bandMat);
                Place(i);
            }
        }

        public float Radius { get { return _r; } }

        private void Place(int i)
        {
            float a = _ang[i], c = Mathf.Cos(a), s = Mathf.Sin(a), hh = _h[i], R = _r;
            foreach (var t in new[] { _slat[i], _cap[i], _low[i], _high[i] }) t.RotY(-a + Mathf.PI / 2f);
            _slat[i].localPosition = new Vector3(c * R, 0, s * R);
            _slat[i].localScale = new Vector3(1, hh, 1);
            _cap[i].localPosition = new Vector3(c * R, H * hh, s * R);
            _low[i].localPosition = new Vector3(c * (R + .12f), .38f, s * (R + .12f));
            _low[i].gameObject.SetActive(H * hh > .5f);
            _high[i].localPosition = new Vector3(c * (R + .12f), H - .3f, s * (R + .12f));
            _high[i].gameObject.SetActive(hh > .92f);
        }

        public void Skin(SteamerSkinData sk)
        {
            for (int i = 0; i < _n; i++)
                _slatMats[i].SetColor("_BaseColor", OffsetHsl(i % 2 != 0 ? sk.a : sk.b, 0, 0, ((i * 37) % 7 - 3) * .008f));
            _capMat.SetColor("_BaseColor", Lin(sk.a));
            _bandMat.SetColor("_BaseColor", Lin(sk.t));
        }

        /// <summary>Slats facing the camera (direction cx, cz) sink so the room stays visible.</summary>
        public void Cutaway(float cx, float cz, float dt, float limit)
        {
            for (int i = 0; i < _n; i++)
            {
                float dd = Mathf.Cos(_ang[i]) * cx + Mathf.Sin(_ang[i]) * cz;
                float t = dd > limit ? .2f : 1f;
                if (Mathf.Abs(_h[i] - t) > .002f)
                {
                    _h[i] += (t - _h[i]) * Mathf.Min(1f, dt * 7f);
                    Place(i);
                }
            }
        }

        /// <summary>makeLid (radius 2.3).</summary>
        public static Transform Lid(Transform parent)
        {
            const float R = DefaultR;
            var g = Node.Group(parent, "Lid");
            Node.Mesh(g, Cyl(R * .72f, R + .14f, .5f, 48), M("#D6AE72"), 0, .25f, 0);
            Node.Mesh(g, Torus(R + .12f, .13f, 8, 56), M("#A97E47"), 0, 0, 0, shadow: true, receive: false).RotX(Mathf.PI / 2f);
            for (int i = 1; i < 4; i++) Node.Mesh(g, Torus(R + .1f - i * .17f, .05f, 6, 56), M("#A97E47"), 0, i * .13f, 0, shadow: false).RotX(Mathf.PI / 2f);
            Node.Mesh(g, Cyl(R * .72f, R * .72f, .04f, 40), M("#C79C60"), 0, .51f, 0);
            Node.Mesh(g, RBox(.7f, .18f, .24f, .08f), M("#A97E47"), 0, .62f, 0);
            return g;
        }
    }
}
