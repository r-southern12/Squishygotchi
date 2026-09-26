using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;

namespace Squishy.Runtime.Models
{
    /// <summary>makeSquishy: the bao-shaped pet with eyes, blush, glitter and spring squash.</summary>
    public sealed partial class SquishyModel
    {
        private const float B = -.6f;
        private const int SPK = 48;

        public readonly Transform Pivot, Yaw, Body;
        public float Scale, X, V, Blink = 3, EyeOpen = 1, K = 12, C = 4.5f, Grey;
        public bool Held;
        public FinishData Fin;
        public readonly Material Mat;

        private float _g = -1;
        private Color _base = Color.white;
        private readonly Transform[] _eyes = new Transform[2];
        private readonly Material _blush;
        private readonly MeshRenderer _sparkles;
        private readonly Mesh _sparkMesh;
        private static readonly Color GreyC = ThreeMat.Lin("#9C968F");

        private static float Sstep(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }
        private static float Smax(float a, float b, float k) { float h = Mathf.Max(k - Mathf.Abs(a - b), 0) / k; return Mathf.Max(a, b) + h * h * k * .25f; }

        /// <summary>
        /// shapeAt: maps a unit direction onto the bao surface (flat bottom). The top swirl is stronger than the
        /// prototype's (user request, 25 Sep 2026): deeper pleats that twist into a raised, pinched knot.
        /// </summary>
        public static Vector3 ShapeAt(Vector3 d)
        {
            // Dumpling-toy look (user references, 26 Sep 2026): a wide dome whose top half is gathered into 12 puffy
            // lobes split by sharp creases, twisting into a smooth, slightly flattened crown (no knot or bump: user, 26 Sep 2026).
            float th = Mathf.Atan2(d.z, d.x), t = Sstep(.34f, .9f, d.y), top = Sstep(.86f, 1, d.y);
            // Creases stay on the crown (clear of the eyes and the rim) and fade just before the centre, so the
            // mesh never pinches; a rounded crease profile keeps the shading smooth.
            float c = Mathf.Cos(6 * (th + Twist(d.y))), groove = c * c;
            groove = groove * groove * groove * t * (1 - top); // cos^6: a smooth rounded groove, no V-edge to alias
            float r = 1.16f * (1 - .3f * groove) * (1 - .06f * Sstep(.6f, 1, d.y)) * (1 + .05f * Sstep(-.1f, -.7f, d.y));
            return new Vector3(d.x * r, Smax(d.y * .86f - .03f * top - .1f * groove, B, .14f), d.z * r);
        }

        private static float Twist(float y) { return Sstep(.34f, .9f, y) * 1.4f; }

        /// <summary>
        /// The bao mesh. Its grid is twisted along the swirl so every groove runs with the grid columns instead of
        /// cutting across the rows (which left saw-tooth steps in the grooves); the surface is still ShapeAt.
        /// </summary>
        public static Mesh BaoMesh()
        {
            return ThreeGeo.Deformed("bao", 216, 84, v =>
            {
                var d = v.normalized;
                float a = -Twist(d.y), cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                return ShapeAt(new Vector3(d.x * cs - d.z * sn, d.y, d.x * sn + d.z * cs));
            });
        }

        public SquishyModel(Transform parent, float scale)
        {
            Scale = scale;
            Pivot = Node.Group(parent, "Squishy");
            Yaw = Node.Group(Pivot, "yaw");
            Mat = ThreeMat.Standard(Color.white, .4f);
            Mat.SetFloat("_ReceiveShadows", 0f);
            Body = Node.Mesh(Yaw, BaoMesh(), Mat, 0, -B, 0, shadow: true, receive: false); // no self-shadow: the pleat grooves would show shadow acne
            Pivot.localScale = Vector3.one * scale;

            // Solid ink beads (a glossy dark material reflected the sky and washed the face out); the white shines do the gloss.
            var eyeMat = ThreeMat.Basic(ThreeMat.Lin("#140E12"));
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1 : 1;
                var d = new Vector3(sx * .36f, .1f, .93f).normalized;
                var p = ShapeAt(d);
                var n = p - new Vector3(0, -.1f, 0);
                n.y *= 1.3f;
                n.Normalize();
                var e = Node.Mesh(Body, ThreeGeo.Sph(.1f, 14, 12), eyeMat, 0, 0, 0, shadow: false, receive: true);
                e.localPosition = p + n * -.018f;
                e.localRotation = Quaternion.FromToRotation(Vector3.forward, n);
                e.localScale = new Vector3(1, 1.08f, .45f);
                _eyes[k] = e;
                // A shine in each eye (blinks with it).
                var shine = Node.Mesh(e, ThreeGeo.Sph(.034f, 10, 8), ThreeMat.Basic(Color.white), -.03f, .036f, .085f, shadow: false);
                shine.name = "shine";
                Node.Mesh(e, ThreeGeo.Sph(.014f, 6, 5), ThreeMat.Basic(Color.white), .034f, -.03f, .085f, shadow: false).name = "shine2";
            }

            // A tiny "w" mouth between the eyes: two little smile arcs side by side.
            for (int k = 0; k < 2; k++)
            {
                var d = new Vector3(k == 0 ? -.035f : .035f, .03f, 1).normalized;
                var p = ShapeAt(d);
                var n = p - new Vector3(0, -.1f, 0);
                n.y *= 1.3f;
                n.Normalize();
                var mouth = Node.Mesh(Body, ThreeGeo.Torus(.034f, .011f, 6, 12, Mathf.PI), eyeMat, 0, 0, 0, shadow: false, receive: true);
                mouth.localPosition = p + n * -.004f;
                mouth.localRotation = Quaternion.FromToRotation(Vector3.forward, n) * Quaternion.AngleAxis(180, Vector3.forward);
                mouth.name = "mouth";
            }

            _blush = ThreeMat.Basic(ThreeMat.Lin("#FF9C8F"), .45f, ThreeMat.Blend.Alpha, depthWrite: false);
            for (int k = 0; k < 2; k++)
            {
                float sx = k == 0 ? -1 : 1;
                var d = new Vector3(sx * .6f, -.02f, .8f).normalized;
                var p = ShapeAt(d);
                var n = (p - new Vector3(0, -.1f, 0)).normalized;
                var b = Node.Mesh(Body, ThreeGeo.Circle(.11f, 14), _blush, 0, 0, 0, shadow: false);
                b.localPosition = p + n * .006f;
                b.localRotation = Quaternion.FromToRotation(Vector3.forward, n);
                b.localScale = new Vector3(1, .62f, 1);
            }

            // Glitter: 48 camera-facing sprites spread over (and just inside) the surface.
            var verts = new Vector3[SPK * 4];
            var corners = new Vector2[SPK * 4];
            var phase = new Vector2[SPK * 4];
            var cols = new Color[SPK * 4];
            var tris = new int[SPK * 6];
            for (int i = 0; i < SPK; i++)
            {
                var d = new Vector3(Random.Range(-1f, 1f), Random.Range(-.3f, 1f), Random.Range(-1f, 1f)).normalized;
                var p = ShapeAt(d) * Random.Range(.7f, 1.01f);
                float ph = Random.Range(0f, 7f);
                for (int c = 0; c < 4; c++)
                {
                    verts[i * 4 + c] = p;
                    corners[i * 4 + c] = new Vector2(c == 0 || c == 3 ? -1 : 1, c < 2 ? -1 : 1);
                    phase[i * 4 + c] = new Vector2(ph, 0);
                    cols[i * 4 + c] = Color.white;
                }
                tris[i * 6] = i * 4; tris[i * 6 + 1] = i * 4 + 2; tris[i * 6 + 2] = i * 4 + 1;
                tris[i * 6 + 3] = i * 4; tris[i * 6 + 4] = i * 4 + 3; tris[i * 6 + 5] = i * 4 + 2;
            }
            _sparkMesh = new Mesh { name = "sparkles", vertices = verts, uv = corners, uv2 = phase, colors = cols, triangles = tris };
            _sparkMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 3f);
            _sparkMesh.MarkDynamic();
            var sp = Node.Mesh(Body, _sparkMesh, ThreeMat.Sparkles(), 0, 0, 0, shadow: false);
            _sparkles = sp.GetComponent<MeshRenderer>();
        }

        public void SetFinish(FinishData f)
        {
            Fin = f;
            _base = ThreeMat.Lin(f.color);
            Mat.SetVector("_BaseColor", _base);
            Mat.SetFloat("_Roughness", f.rough);
            Mat.SetFloat("_Metalness", f.metal ? .6f : 0f);
            Mat.SetTexture("_BaseMap", string.IsNullOrEmpty(f.map) ? Texture2D.whiteTexture : Textures.FinishMap(f.map));
            Mat.SetVector("_EmissionColor", string.IsNullOrEmpty(f.glow) ? Color.black : ThreeMat.Lin(f.glow) * .25f);
            K = f.tier == "UV" ? 40 : f.tier == "Common" ? 5 : 12;
            C = f.tier == "UV" ? 3 : f.tier == "Common" ? 5 : 4.5f;
            bool spark = f.spark != null && f.spark.Length > 0;
            _sparkles.enabled = spark;
            if (spark)
            {
                var cols = new Color[SPK * 4];
                for (int j = 0; j < SPK; j++) for (int c = 0; c < 4; c++) cols[j * 4 + c] = ThreeMat.Lin(f.spark[j % f.spark.Length]);
                _sparkMesh.colors = cols;
            }
            ThreeMat.SetOpacity(_blush, f.tier == "Galaxy" ? .35f : .5f);
            _g = -1;
        }

        /// <summary>Spring squash, blinking, squint and greying, once per frame.</summary>
        public void Update(float dt, float extraSquash, bool closed, float droop = 0)
        {
            float k = Held ? 120 : K * StageBounce, c = Held ? 2 * Mathf.Sqrt(120) * .9f : C;
            V += (k * ((Held ? 1 : 0) - X) - c * V) * dt;
            X += V * dt;
            float x = Mathf.Clamp(X + extraSquash + droop * .3f, -.45f, 1);
            float sc = Scale * StageScale;
            Pivot.localScale = new Vector3(sc * (1 + .3f * x), sc * (1 - .42f * x), sc * (1 + .3f * x));
            Blink -= dt;
            if (Blink < 0) Blink = Random.Range(2.5f, 5f);
            float bl = Blink < .12f ? .1f : 1, squint = 1 - .8f * Mathf.Clamp01(x * 1.6f);
            float open = closed ? .08f : Mathf.Min(bl, squint) * (1 - droop * .45f);
            EyeOpen += (open - EyeOpen) * Mathf.Min(1, dt * 14);
            foreach (var e in _eyes) e.localScale = new Vector3(_eyeW, 1.08f * EyeOpen * _eyeW + .02f, .45f);
            if (Grey != _g)
            {
                _g = Grey;
                Mat.SetVector("_BaseColor", Color.Lerp(StageTint(_base), GreyC, Grey));
                _sparkles.enabled = Fin != null && Fin.spark != null && Fin.spark.Length > 0 && Grey < .5f;
            }
        }
    }
}
