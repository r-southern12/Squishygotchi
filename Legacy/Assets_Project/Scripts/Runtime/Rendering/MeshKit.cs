using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Runtime.Rendering
{
    /// <summary>
    /// Procedural placeholder meshes for the soft-block look: rounded boxes, discs and the dumpling body.
    /// Meshes are cached by shape, so every slat of the same size shares one mesh.
    /// </summary>
    public static class MeshKit
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>A box with rounded edges. Pivot at the centre of the bottom face.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 3)
        {
            string key = "box" + size + radius + segments;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            Vector3 half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            Vector3 inner = half - Vector3.one * radius;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            // (normal, u, v) with u x v = normal, so quads (a, b, c, d) wind front-facing in Unity.
            Vector3[,] faces =
            {
                { Vector3.right, Vector3.up, Vector3.forward }, { Vector3.left, Vector3.forward, Vector3.up },
                { Vector3.up, Vector3.forward, Vector3.right }, { Vector3.down, Vector3.right, Vector3.forward },
                { Vector3.forward, Vector3.right, Vector3.up }, { Vector3.back, Vector3.up, Vector3.right },
            };

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faces[f, 0], u = faces[f, 1], v = faces[f, 2];
                float[] cu = EdgeWeighted(Vector3.Scale(Abs(u), half).magnitude, radius, segments);
                float[] cv = EdgeWeighted(Vector3.Scale(Abs(v), half).magnitude, radius, segments);
                float hn = Vector3.Scale(Abs(n), half).magnitude;
                int start = verts.Count;
                for (int j = 0; j < cv.Length; j++)
                for (int i = 0; i < cu.Length; i++)
                {
                    Vector3 p = n * hn + u * cu[i] + v * cv[j];
                    Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 dir = p - c;
                    dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : n;
                    verts.Add(c + dir * radius + Vector3.up * half.y);
                    normals.Add(dir);
                }
                int w = cu.Length;
                for (int j = 0; j < cv.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    int a = start + j * w + i, b = a + 1, c = a + w + 1, d = a + w;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                }
            }
            return Store(key, verts, normals, tris);
        }

        /// <summary>A flat cylinder (disc) with a top cap and sides. Pivot at the bottom centre.</summary>
        public static Mesh Disc(float radius, float height, int sides = 48)
        {
            string key = "disc" + radius + height + sides;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            int centre = verts.Count;
            verts.Add(new Vector3(0f, height, 0f));
            normals.Add(Vector3.up);
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius));
                normals.Add(Vector3.up);
            }
            for (int i = 0; i < sides; i++) { tris.Add(centre); tris.Add(centre + i + 2); tris.Add(centre + i + 1); }

            int side = verts.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir * radius); normals.Add(dir);
                verts.Add(dir * radius + Vector3.up * height); normals.Add(dir);
            }
            for (int i = 0; i < sides; i++)
            {
                int b0 = side + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                tris.Add(b0); tris.Add(t0); tris.Add(t1);
                tris.Add(b0); tris.Add(t1); tris.Add(b1);
            }
            return Store(key, verts, normals, tris);
        }

        /// <summary>Round bun with a flattened base, radius 1 around its middle, sitting on y = 0.</summary>
        public static Mesh Dumpling(int lon = 40, int lat = 24)
        {
            string key = "dumpling" + lon + lat;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            const float bottom = -0.46f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int j = 0; j <= lat; j++)
            {
                float theta = j * Mathf.PI / lat;
                for (int i = 0; i <= lon; i++)
                {
                    float phi = i * Mathf.PI * 2f / lon;
                    float y = Mathf.Cos(theta), r = Mathf.Sin(theta);
                    y = y > 0f ? y * 0.8f : Mathf.Max(y * 0.72f, bottom);
                    r *= 1f + 0.08f * Mathf.Clamp01(-y * 2f); // a little belly near the base
                    verts.Add(new Vector3(Mathf.Cos(phi) * r, y - bottom, Mathf.Sin(phi) * r));
                }
            }
            int w = lon + 1;
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w + 1, d = a + w;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }

            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // Weld normals across the UV seam and at the poles so the glossy body has no visible crease.
            var n = mesh.normals;
            for (int j = 0; j <= lat; j++)
            {
                Vector3 sum = n[j * w] + n[j * w + lon];
                if (j == 0 || j == lat) { sum = Vector3.zero; for (int i = 0; i <= lon; i++) sum += n[j * w + i]; }
                sum.Normalize();
                if (j == 0 || j == lat) for (int i = 0; i <= lon; i++) n[j * w + i] = sum;
                else { n[j * w] = sum; n[j * w + lon] = sum; }
            }
            mesh.normals = n;
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        private static float[] EdgeWeighted(float half, float radius, int segments)
        {
            // Points bunched into the rounded band at each end, one flat span in between.
            var list = new List<float>();
            for (int k = 0; k <= segments; k++) list.Add(-half + radius * (1f - Mathf.Cos(k * Mathf.PI * 0.5f / segments)));
            for (int k = segments; k >= 0; k--) list.Add(half - radius * (1f - Mathf.Cos(k * Mathf.PI * 0.5f / segments)));
            return list.ToArray();
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static Mesh Store(string key, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }
    }

    /// <summary>One material per colour, cloned from a base. Keeps the SRP batcher happy (no property blocks).</summary>
    public sealed class MaterialPalette
    {
        private readonly Material _base;
        private readonly Dictionary<Color, Material> _byColor = new Dictionary<Color, Material>();

        public MaterialPalette(Material baseMaterial)
        {
            _base = baseMaterial;
        }

        public Material Get(Color color)
        {
            if (_byColor.TryGetValue(color, out var m)) return m;
            m = new Material(_base) { name = _base.name + " " + ColorUtility.ToHtmlStringRGB(color) };
            m.SetColor("_BaseColor", color);
            // Matte (Lambert-like) like the prototype's scenery: no specular highlights.
            m.SetFloat("_SpecularHighlights", 0f);
            m.DisableKeyword("_SPECULAR_COLOR");
            m.SetColor("_SpecColor", Color.black);
            _byColor[color] = m;
            return m;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
