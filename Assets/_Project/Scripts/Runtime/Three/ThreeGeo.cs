using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Runtime.Three
{
    /// <summary>
    /// Exact ports of the prototype's three.js geometry (rbox, cyl, sph, torus, circle, plane, icosahedron).
    /// Vertices stay in three.js space (right-handed, CCW front faces): the game world sits under a root
    /// scaled (1, 1, -1), which mirrors it into Unity and flips culling to match. Meshes drawn outside that
    /// root (instanced particles) are built with <c>mirror: true</c>. All meshes are cached by their parameters.
    /// </summary>
    public static class ThreeGeo
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        // ---- rbox: rounded-rect footprint extruded with a 2-segment bevel (flat shaded, like the prototype) ----

        public static Mesh RBox(float w, float h, float d, float r = 0.05f)
        {
            r = Mathf.Min(r, w / 2f - 0.002f, Mathf.Min(h / 2f - 0.002f, d / 2f - 0.002f));
            string key = "rbox" + w.ToString("F3") + "," + h.ToString("F3") + "," + d.ToString("F3") + "," + r.ToString("F3");
            if (Cache.TryGetValue(key, out var cached)) return cached;

            float a = w / 2f - r, b = d / 2f - r, c = Mathf.Min(a, b) * 0.6f;
            // Shape outline exactly as the prototype's path, curveSegments = 2.
            var shape = new List<Vector2>
            {
                new Vector2(-a + c, -b), new Vector2(a - c, -b), Quad(new Vector2(a - c, -b), new Vector2(a, -b), new Vector2(a, -b + c)),
                new Vector2(a, -b + c), new Vector2(a, b - c), Quad(new Vector2(a, b - c), new Vector2(a, b), new Vector2(a - c, b)),
                new Vector2(a - c, b), new Vector2(-a + c, b), Quad(new Vector2(-a + c, b), new Vector2(-a, b), new Vector2(-a, b - c)),
                new Vector2(-a, b - c), new Vector2(-a, -b + c), Quad(new Vector2(-a, -b + c), new Vector2(-a, -b), new Vector2(-a + c, -b)),
            };
            RemoveDuplicates(shape);
            int n = shape.Count;
            var bevel = new Vector2[n];
            for (int i = 0; i < n; i++) bevel[i] = BevelVec(shape[i], shape[(i + n - 1) % n], shape[(i + 1) % n]);

            float depth = Mathf.Max(0.002f, h - 2f * r);
            // Layers bottom to top: (offset, z) in extrude space; z centred afterwards.
            var layers = new List<Vector2>();
            for (int s = 0; s <= 2; s++) { float t = s / 2f; layers.Add(new Vector2(r * Mathf.Sin(t * Mathf.PI / 2f), -r * Mathf.Cos(t * Mathf.PI / 2f))); }
            for (int s = 2; s >= 0; s--) { float t = s / 2f; layers.Add(new Vector2(r * Mathf.Sin(t * Mathf.PI / 2f), depth + r * Mathf.Cos(t * Mathf.PI / 2f))); }

            var b0 = new MeshBuilder();
            // Contour at each layer, in three.js space after rotateX(-90deg) and centring: (x, z - depth/2, -y).
            System.Func<int, int, Vector3> P = (layer, i) =>
            {
                Vector2 pt = shape[i] + bevel[i] * layers[layer].x;
                return new Vector3(pt.x, layers[layer].y - depth / 2f, -pt.y);
            };
            System.Func<int, int, Vector2> UvSide = (layer, i) => Vector2.zero;
            // Caps.
            for (int i = 1; i < n - 1; i++)
            {
                b0.TriOutward(P(0, 0), P(0, i), P(0, i + 1), Uv(shape[0]), Uv(shape[i]), Uv(shape[i + 1]));
                int top = layers.Count - 1;
                b0.TriOutward(P(top, 0), P(top, i), P(top, i + 1), Uv(shape[0]), Uv(shape[i]), Uv(shape[i + 1]));
            }
            // Sides (bevel rings and walls).
            for (int l = 0; l < layers.Count - 1; l++)
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 p0 = P(l, i), p1 = P(l, j), p2 = P(l + 1, j), p3 = P(l + 1, i);
                bool alongX = Mathf.Abs(shape[i].y - shape[j].y) < Mathf.Abs(shape[i].x - shape[j].x);
                Vector2 u0 = SideUv(p0, alongX, depth), u1 = SideUv(p1, alongX, depth), u2 = SideUv(p2, alongX, depth), u3 = SideUv(p3, alongX, depth);
                b0.TriOutward(p0, p1, p2, u0, u1, u2);
                b0.TriOutward(p0, p2, p3, u0, u2, u3);
            }
            return Cache[key] = b0.Build(key, flat: true);
        }

        private static Vector2 Quad(Vector2 p0, Vector2 ctrl, Vector2 p1)
        {
            // Midpoint (t = 0.5) of the quadratic curve; the end point is listed separately.
            return 0.25f * p0 + 0.5f * ctrl + 0.25f * p1;
        }

        private static void RemoveDuplicates(List<Vector2> pts)
        {
            for (int i = pts.Count - 1; i >= 0; i--)
                if ((pts[i] - pts[(i + 1) % pts.Count]).sqrMagnitude < 1e-10f) pts.RemoveAt(i);
        }

        private static Vector2 BevelVec(Vector2 pt, Vector2 prev, Vector2 next)
        {
            // Miter vector so each edge moves out by exactly 1 unit (three.js getBevelVec).
            Vector2 e1 = (pt - prev).normalized, e2 = (next - pt).normalized;
            Vector2 n1 = new Vector2(e1.y, -e1.x), n2 = new Vector2(e2.y, -e2.x);
            float k = 1f + Vector2.Dot(n1, n2);
            return k < 1e-4f ? n1 : (n1 + n2) / k;
        }

        private static Vector2 Uv(Vector2 shapePt)
        {
            return new Vector2(shapePt.x, shapePt.y);
        }

        private static Vector2 SideUv(Vector3 p, bool alongX, float depth)
        {
            float z = p.y + depth / 2f;
            return alongX ? new Vector2(p.x, 1f - z) : new Vector2(-p.z, 1f - z);
        }

        // ---- cylinder (three.js CylinderGeometry, heightSegments 1) ----

        public static Mesh Cyl(float rt, float rb, float h, int seg = 16)
        {
            string key = "cyl" + rt + "," + rb + "," + h + "," + seg;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = new MeshBuilder();
            float slope = (rb - rt) / h;
            var top = new List<int>();
            var bot = new List<int>();
            for (int x = 0; x <= seg; x++)
            {
                float u = (float)x / seg, th = u * Mathf.PI * 2f;
                float s = Mathf.Sin(th), c = Mathf.Cos(th);
                Vector3 nrm = new Vector3(s, slope, c).normalized;
                top.Add(mb.Vert(new Vector3(rt * s, h / 2f, rt * c), nrm, new Vector2(u, 1f)));
                bot.Add(mb.Vert(new Vector3(rb * s, -h / 2f, rb * c), nrm, new Vector2(u, 0f)));
            }
            for (int x = 0; x < seg; x++)
            {
                int a = top[x], b = bot[x], c2 = bot[x + 1], d = top[x + 1];
                mb.Tri(a, b, d);
                mb.Tri(b, c2, d);
            }
            Cap(mb, rt, h / 2f, seg, true);
            Cap(mb, rb, -h / 2f, seg, false);
            return Cache[key] = mb.Build(key, flat: false);
        }

        private static void Cap(MeshBuilder mb, float radius, float y, int seg, bool top)
        {
            if (radius <= 0f) return;
            float sign = top ? 1f : -1f;
            Vector3 nrm = new Vector3(0f, sign, 0f);
            int centreStart = mb.Count;
            for (int x = 1; x <= seg; x++) mb.Vert(new Vector3(0f, y, 0f), nrm, new Vector2(0.5f, 0.5f));
            int ringStart = mb.Count;
            for (int x = 0; x <= seg; x++)
            {
                float th = (float)x / seg * Mathf.PI * 2f;
                float s = Mathf.Sin(th), c = Mathf.Cos(th);
                mb.Vert(new Vector3(radius * s, y, radius * c), nrm, new Vector2(c * 0.5f + 0.5f, s * 0.5f * sign + 0.5f));
            }
            for (int x = 0; x < seg; x++)
            {
                int ctr = centreStart + x, i0 = ringStart + x, i1 = ringStart + x + 1;
                if (top) mb.Tri(i0, i1, ctr); else mb.Tri(i1, i0, ctr);
            }
        }

        // ---- sphere (three.js SphereGeometry) ----

        public static Mesh Sph(float r, int w = 14, int hSeg = 10)
        {
            string key = "sph" + r + "," + w + "," + hSeg;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            return Cache[key] = SphereBuilder(w, hSeg, v => v * r).Build(key, flat: false);
        }

        /// <summary>A unit sphere with each vertex remapped (the squishy's bao shape), normals recomputed smooth.</summary>
        public static Mesh Deformed(string key, int w, int hSeg, System.Func<Vector3, Vector3> shape)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = SphereBuilder(w, hSeg, shape);
            return Cache[key] = mb.Build(key, flat: false, recalcNormals: true);
        }

        private static MeshBuilder SphereBuilder(int w, int hSeg, System.Func<Vector3, Vector3> map)
        {
            var mb = new MeshBuilder();
            var grid = new List<int[]>();
            for (int iy = 0; iy <= hSeg; iy++)
            {
                var row = new int[w + 1];
                float v = (float)iy / hSeg;
                for (int ix = 0; ix <= w; ix++)
                {
                    float u = (float)ix / w;
                    var unit = new Vector3(-Mathf.Cos(u * Mathf.PI * 2f) * Mathf.Sin(v * Mathf.PI), Mathf.Cos(v * Mathf.PI), Mathf.Sin(u * Mathf.PI * 2f) * Mathf.Sin(v * Mathf.PI));
                    row[ix] = mb.Vert(map(unit), unit, new Vector2(u, 1f - v));
                }
                grid.Add(row);
            }
            for (int iy = 0; iy < hSeg; iy++)
            for (int ix = 0; ix < w; ix++)
            {
                int a = grid[iy][ix + 1], b = grid[iy][ix], c = grid[iy + 1][ix], d = grid[iy + 1][ix + 1];
                if (iy != 0) mb.Tri(a, b, d);
                if (iy != hSeg - 1) mb.Tri(b, c, d);
            }
            return mb;
        }

        // ---- torus (three.js TorusGeometry, in the XY plane) ----

        public static Mesh Torus(float radius, float tube, int radialSeg, int tubularSeg, float arc = Mathf.PI * 2f)
        {
            string key = "torus" + radius + "," + tube + "," + radialSeg + "," + tubularSeg + "," + arc;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = new MeshBuilder();
            var grid = new int[radialSeg + 1, tubularSeg + 1];
            for (int j = 0; j <= radialSeg; j++)
            for (int i = 0; i <= tubularSeg; i++)
            {
                float u = (float)i / tubularSeg * arc, v = (float)j / radialSeg * Mathf.PI * 2f;
                var p = new Vector3((radius + tube * Mathf.Cos(v)) * Mathf.Cos(u), (radius + tube * Mathf.Cos(v)) * Mathf.Sin(u), tube * Mathf.Sin(v));
                var centre = new Vector3(radius * Mathf.Cos(u), radius * Mathf.Sin(u), 0f);
                grid[j, i] = mb.Vert(p, (p - centre).normalized, new Vector2((float)i / tubularSeg, (float)j / radialSeg));
            }
            for (int j = 1; j <= radialSeg; j++)
            for (int i = 1; i <= tubularSeg; i++)
            {
                int a = grid[j, i - 1], b = grid[j - 1, i - 1], c = grid[j - 1, i], d = grid[j, i];
                mb.Tri(a, b, d);
                mb.Tri(b, c, d);
            }
            return Cache[key] = mb.Build(key, flat: false);
        }

        // ---- circle and plane (XY plane, facing +z in three.js) ----

        public static Mesh Circle(float radius, int seg)
        {
            string key = "circle" + radius + "," + seg;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = new MeshBuilder();
            int ctr = mb.Vert(Vector3.zero, Vector3.forward, new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= seg; s++)
            {
                float th = (float)s / seg * Mathf.PI * 2f;
                mb.Vert(new Vector3(radius * Mathf.Cos(th), radius * Mathf.Sin(th), 0f), Vector3.forward, new Vector2(Mathf.Cos(th) * 0.5f + 0.5f, Mathf.Sin(th) * 0.5f + 0.5f));
            }
            for (int s = 1; s <= seg; s++) mb.Tri(s, s + 1, ctr);
            return Cache[key] = mb.Build(key, flat: false);
        }

        public static Mesh Plane(float w, float h, int ws = 1, int hs = 1, float offsetY = 0f)
        {
            string key = "plane" + w + "," + h + "," + ws + "," + hs + "," + offsetY;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = BuildPlane(w, h, ws, hs, offsetY);
            return Cache[key] = mb.Build(key, flat: false, dynamic: false);
        }

        /// <summary>An unshared, writable plane (for the shower curtain that ripples open and shut).</summary>
        public static Mesh PlaneDynamic(float w, float h, int ws, int hs, float offsetY)
        {
            return BuildPlane(w, h, ws, hs, offsetY).Build("plane_dyn", flat: false, dynamic: true);
        }

        private static MeshBuilder BuildPlane(float w, float h, int ws, int hs, float offsetY)
        {
            var mb = new MeshBuilder();
            for (int iy = 0; iy <= hs; iy++)
            for (int ix = 0; ix <= ws; ix++)
            {
                float x = (float)ix / ws * w - w / 2f, y = h / 2f - (float)iy / hs * h + offsetY;
                mb.Vert(new Vector3(x, y, 0f), Vector3.forward, new Vector2((float)ix / ws, 1f - (float)iy / hs));
            }
            for (int iy = 0; iy < hs; iy++)
            for (int ix = 0; ix < ws; ix++)
            {
                int a = ix + (ws + 1) * iy, b = ix + (ws + 1) * (iy + 1), c = ix + 1 + (ws + 1) * (iy + 1), d = ix + 1 + (ws + 1) * iy;
                mb.Tri(a, b, d);
                mb.Tri(b, c, d);
            }
            return mb;
        }

        /// <summary>Icosahedron, detail 1 (the steam and splash puffs), flat shaded. Mirrored for instanced drawing.</summary>
        public static Mesh Ico1(bool mirror = true)
        {
            string key = "ico1" + mirror;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            var mb = new MeshBuilder();
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]].normalized, b = v[f[i + 1]].normalized, c = v[f[i + 2]].normalized;
                Vector3 ab = ((a + b) / 2f).normalized, bc = ((b + c) / 2f).normalized, ca = ((c + a) / 2f).normalized;
                mb.TriFlat(a, ab, ca); mb.TriFlat(ab, b, bc); mb.TriFlat(ca, bc, c); mb.TriFlat(ab, bc, ca);
            }
            return Cache[key] = mb.Build(key, flat: true, mirror: mirror);
        }

        /// <summary>RingGeometry(inner, outer, segments) rotated flat (rotateX(-90deg)), for the selection ring.</summary>
        public static Mesh FlatRing(float inner, float outer, int seg)
        {
            string key = "ring" + inner + "," + outer + "," + seg;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mb = new MeshBuilder();
            for (int j = 0; j <= 1; j++)
            for (int i = 0; i <= seg; i++)
            {
                float r = j == 0 ? inner : outer, th = (float)i / seg * Mathf.PI * 2f;
                float x = r * Mathf.Cos(th), y = r * Mathf.Sin(th);
                mb.Vert(new Vector3(x, 0f, -y), Vector3.up, new Vector2((x / outer + 1f) / 2f, (y / outer + 1f) / 2f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i, b = i + seg + 1, c = i + seg + 2, d = i + 1;
                mb.Tri(a, b, d);
                mb.Tri(b, c, d);
            }
            return Cache[key] = mb.Build(key, flat: false);
        }

        /// <summary>A plane for instanced confetti (double-sided, so mirroring only matters for placement).</summary>
        public static Mesh PlaneMirrored(float w, float h)
        {
            string key = "planeM" + w + "," + h;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            return Cache[key] = BuildPlane(w, h, 1, 1, 0f).Build(key, flat: false, mirror: true);
        }
    }

    /// <summary>Collects three.js-space triangles and emits a Unity mesh (optionally mirrored).</summary>
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<int> _t = new List<int>();

        public int Count { get { return _v.Count; } }

        public int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(p); _n.Add(n); _uv.Add(uv);
            return _v.Count - 1;
        }

        public void Tri(int a, int b, int c)
        {
            _t.Add(a); _t.Add(b); _t.Add(c);
        }

        /// <summary>Adds a flat triangle wound so its normal points away from the origin (convex shapes only).</summary>
        public void TriOutward(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a);
            if (nrm.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(nrm, (a + b + c) / 3f) < 0f) { var tmp = b; b = c; c = tmp; var tu = ub; ub = uc; uc = tu; nrm = -nrm; }
            nrm.Normalize();
            Tri(Vert(a, nrm, ua), Vert(b, nrm, ub), Vert(c, nrm, uc));
        }

        public void TriFlat(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
            Tri(Vert(a, nrm, Vector2.zero), Vert(b, nrm, Vector2.zero), Vert(c, nrm, Vector2.zero));
        }

        public Mesh Build(string name, bool flat, bool recalcNormals = false, bool dynamic = false, bool mirror = false)
        {
            // Mirrored meshes negate z (turning three.js CCW fronts into Unity CW fronts) for use outside the world root.
            float zs = mirror ? -1f : 1f;
            var verts = new Vector3[_v.Count];
            var norms = new Vector3[_n.Count];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = new Vector3(_v[i].x, _v[i].y, zs * _v[i].z);
                norms[i] = new Vector3(_n[i].x, _n[i].y, zs * _n[i].z);
            }
            var mesh = new Mesh { name = name };
            if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = _uv.ToArray();
            mesh.triangles = _t.ToArray();
            if (recalcNormals) mesh.RecalculateNormals();
            else mesh.normals = norms;
            if (recalcNormals) WeldSeamNormals(mesh);
            mesh.RecalculateBounds();
            if (dynamic) mesh.MarkDynamic();
            return mesh;
        }

        /// <summary>Averages normals of vertices sharing a position (sphere seams and poles), like three's indexed sphere.</summary>
        private static void WeldSeamNormals(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var groups = new Dictionary<Vector3Int, Vector3>();
            System.Func<Vector3, Vector3Int> Key = p => new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));
            for (int i = 0; i < v.Length; i++)
            {
                var k = Key(v[i]);
                groups[k] = (groups.TryGetValue(k, out var s) ? s : Vector3.zero) + n[i];
            }
            for (int i = 0; i < v.Length; i++) n[i] = groups[Key(v[i])].normalized;
            mesh.normals = n;
        }
    }
}
