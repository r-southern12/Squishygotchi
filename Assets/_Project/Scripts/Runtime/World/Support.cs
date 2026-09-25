using System.Collections.Generic;
using Squishy.Runtime.Three;
using UnityEngine;
using UnityEngine.Rendering;

namespace Squishy.Runtime.World
{
    /// <summary>Converts between three.js space (the prototype's numbers) and Unity world space: z is mirrored.</summary>
    public static class Space3
    {
        public static Vector3 U(Vector3 p) { return new Vector3(p.x, p.y, -p.z); }
        public static Vector3 U(float x, float y, float z) { return new Vector3(x, y, -z); }

        /// <summary>Ray from a Unity camera, returned in three.js space.</summary>
        public static Ray ThreeRay(Camera cam, Vector2 screenPx)
        {
            var r = cam.ScreenPointToRay(screenPx);
            return new Ray(U(r.origin), U(r.direction));
        }

        /// <summary>Ray against the floor plane y = h (three space).</summary>
        public static bool Floor(Ray r, float h, out Vector3 hit)
        {
            hit = default;
            if (Mathf.Abs(r.direction.y) < 1e-6f) return false;
            float t = (h - r.origin.y) / r.direction.y;
            if (t < 0) return false;
            hit = r.origin + r.direction * t;
            return true;
        }
    }

    /// <summary>Raycaster.intersectObject: exact ray/triangle tests against the meshes under a transform.</summary>
    public static class RayPick
    {
        private static readonly Dictionary<Mesh, (Vector3[] v, int[] t)> Data = new Dictionary<Mesh, (Vector3[], int[])>();

        /// <summary>Distance along a Unity-world ray to the nearest hit under root, or -1.</summary>
        public static float Hit(Ray worldRay, Transform root, bool recursive = true)
        {
            float best = -1;
            var filters = recursive ? root.GetComponentsInChildren<MeshFilter>() : root.GetComponents<MeshFilter>();
            foreach (var mf in filters)
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mf.gameObject.activeInHierarchy) continue;
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled) continue;
                var toLocal = mf.transform.worldToLocalMatrix;
                var o = toLocal.MultiplyPoint3x4(worldRay.origin);
                var d = toLocal.MultiplyVector(worldRay.direction);
                if (!mesh.bounds.IntersectRay(new Ray(o, d))) continue;
                if (!Data.TryGetValue(mesh, out var md)) { md = (mesh.vertices, mesh.triangles); Data[mesh] = md; }
                var v = md.v;
                var t = md.t;
                for (int i = 0; i < t.Length; i += 3)
                {
                    if (!Tri(o, d, v[t[i]], v[t[i + 1]], v[t[i + 2]], out float s)) continue;
                    var hitW = mf.transform.localToWorldMatrix.MultiplyPoint3x4(o + d * s);
                    float dist = Vector3.Dot(hitW - worldRay.origin, worldRay.direction.normalized);
                    if (dist > 0 && (best < 0 || dist < best)) best = dist;
                }
            }
            return best;
        }

        /// <summary>Forgets cached vertices of a mesh that changes (the curtain).</summary>
        public static void Forget(Mesh m) { Data.Remove(m); }

        private static bool Tri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0;
            Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            Vector3 s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0 || u > 1) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float w = Vector3.Dot(d, q) * inv;
            if (w < 0 || u + w > 1) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t > 0;
        }
    }

    /// <summary>pool(): instanced, recycled particles (steam puffs, water drops, confetti).</summary>
    public sealed class ParticlePool
    {
        private sealed class P
        {
            public bool on;
            public Vector3 p, v, r, w;
            public float t, life, s = 1, drag = 1, g, floor;
            public Color col = Color.white;
        }

        private readonly P[] _ps;
        private readonly Mesh _mesh;
        private readonly Material _mat;
        private readonly bool _colored;
        private readonly int _layer;
        private readonly Matrix4x4[] _m;
        private readonly Vector4[] _c;
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private int _next, _live;

        public ParticlePool(int count, Mesh mesh, Material mat, bool colored, int layer)
        {
            _ps = new P[count];
            for (int i = 0; i < count; i++) _ps[i] = new P();
            _mesh = mesh;
            _mat = mat;
            _mat.enableInstancing = true;
            _colored = colored;
            _layer = layer;
            _m = new Matrix4x4[count];
            _c = new Vector4[count];
        }

        public void Spawn(Vector3 p, Vector3 v, float s, float life, float drag, float g, float floor = 0, Vector3? r = null, Vector3? w = null, Color? col = null)
        {
            var q = _ps[_next];
            _next = (_next + 1) % _ps.Length;
            if (!q.on) _live++;
            q.on = true; q.t = 0; q.p = p; q.v = v; q.s = s; q.life = life; q.drag = drag; q.g = g; q.floor = floor;
            q.r = r ?? Vector3.zero; q.w = w ?? Vector3.zero;
            if (col.HasValue) q.col = col.Value;
        }

        public void Clear() { foreach (var q in _ps) q.on = false; _live = 0; }

        /// <summary>Steps and draws. Steam shrinks in and out; confetti tumbles, lands and fades.</summary>
        public void Update(float dt, bool steam, Camera cam)
        {
            if (_live == 0) return;
            int n = 0;
            foreach (var q in _ps)
            {
                if (!q.on) continue;
                q.t += dt;
                if (q.t >= q.life) { q.on = false; _live--; continue; }
                float e = Mathf.Exp(-q.drag * dt);
                q.v *= e;
                q.v.y += q.g * dt;
                q.p += q.v * dt;
                float u = q.t / q.life, sc;
                if (steam) sc = Mathf.Max(.001f, q.s * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.1f + .05f)), .6f));
                else
                {
                    if (q.p.y < q.floor) { q.p.y = q.floor; q.v = Vector3.zero; q.w *= .85f; }
                    q.r += q.w * dt;
                    sc = u > .8f ? Mathf.Max(.001f, (1 - u) / .2f) : 1;
                }
                // three.js XYZ Euler, mirrored into Unity: x and y angles flip sign.
                var rot = steam ? Quaternion.identity
                    : Quaternion.AngleAxis(-q.r.x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(-q.r.y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(q.r.z * Mathf.Rad2Deg, Vector3.forward);
                _m[n] = Matrix4x4.TRS(Space3.U(q.p), rot, Vector3.one * sc);
                _c[n] = q.col;
                n++;
            }
            if (n == 0) return;
            var rp = new RenderParams(_mat) { layer = _layer, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, camera = cam, worldBounds = new Bounds(Vector3.zero, Vector3.one * 100) };
            if (_colored)
            {
                _mpb.SetVectorArray("_InstColor", _c);
                rp.matProps = _mpb;
            }
            Graphics.RenderMeshInstanced(rp, _mesh, 0, _m, n);
        }
    }

    /// <summary>lights(): hemisphere, shadowed key, cool fill and up to two lamp point lights, as shader globals.</summary>
    public static class SceneLighting
    {
        private static readonly int HemiSky = Shader.PropertyToID("_HemiSky"), HemiGround = Shader.PropertyToID("_HemiGround");
        private static readonly int FillDir = Shader.PropertyToID("_FillDir"), FillColor = Shader.PropertyToID("_FillColor");
        private static readonly int LampPos = Shader.PropertyToID("_LampPos"), LampColor = Shader.PropertyToID("_LampColor");
        private static readonly Vector4[] Pos = new Vector4[2], Col = new Vector4[2];

        public static void Globals()
        {
            Shader.SetGlobalVector(HemiSky, ThreeMat.Lin("#FFF1DC") * .62f);
            Shader.SetGlobalVector(HemiGround, ThreeMat.Lin("#8A6445") * .62f);
            // Fill light at (6, 5, -3) aimed at the origin.
            Shader.SetGlobalVector(FillDir, Space3.U(new Vector3(6, 5, -3).normalized));
            Shader.SetGlobalVector(FillColor, ThreeMat.Lin("#D8E4FF") * .3f);
            ClearLamps();
        }

        /// <summary>The key light shines from (-5, 11, 6) relative to its target.</summary>
        public static void Key(Light key)
        {
            key.transform.rotation = Quaternion.LookRotation(Space3.U(-new Vector3(-5, 11, 6)), Vector3.up);
        }

        public static void Lamp(int i, Vector3 threePos, float intensity)
        {
            Pos[i] = Space3.U(threePos);
            Pos[i].w = 2.6f;
            Col[i] = ThreeMat.Lin("#FFB65C") * intensity;
            Shader.SetGlobalVectorArray(LampPos, Pos);
            Shader.SetGlobalVectorArray(LampColor, Col);
        }

        public static void ClearLamps()
        {
            for (int i = 0; i < 2; i++) { Pos[i] = new Vector4(0, -100, 0, 2.6f); Col[i] = Vector4.zero; }
            Shader.SetGlobalVectorArray(LampPos, Pos);
            Shader.SetGlobalVectorArray(LampColor, Col);
        }
    }

    /// <summary>Uniforms of the tilt-shift + grade pass and the sparkle size.</summary>
    public static class Post
    {
        private static readonly int Focus = Shader.PropertyToID("_PostFocus"), Band = Shader.PropertyToID("_PostBand"),
            Warm = Shader.PropertyToID("_PostWarm"), Flash = Shader.PropertyToID("_PostFlash"), Mode = Shader.PropertyToID("_PostMode"),
            SparkTime = Shader.PropertyToID("_SparkTime"), SparkPx = Shader.PropertyToID("_SparkPx");

        public static void Set(float focus, float band, float warm, float flash)
        {
            Shader.SetGlobalFloat(Focus, focus);
            Shader.SetGlobalFloat(Band, band);
            Shader.SetGlobalFloat(Warm, warm);
            Shader.SetGlobalFloat(Flash, flash);
        }

        public static void ThumbMode(bool on) { Shader.SetGlobalFloat(Mode, on ? 1f : 0f); }
        public static void Sparkles(float time, float px) { Shader.SetGlobalFloat(SparkTime, time); Shader.SetGlobalFloat(SparkPx, px); }
    }
}
