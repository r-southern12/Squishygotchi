using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Tactile squishing (user request, 27 Sep 2026, after the real slow-rise dumpling toy): a finger presses a
    /// real dent into the surface where it touches, holding presses deeper, dragging smears it into a trail, and on
    /// release each dent slowly rises back like memory foam (jelly finishes come back quicker). Dents have a fixed
    /// size in the world, so a bigger squishy has more surface to play with and holds more dents at once.
    /// Normals are bent analytically (no mesh-wide recalculation, so no seams).
    /// </summary>
    public sealed partial class SquishyModel
    {
        private sealed class Dent { public int id; public Vector3 p; public float r, depth, max; public bool held; }

        private readonly List<Dent> _dents = new List<Dent>();
        private int _nextDent = 1;
        private Mesh _tMesh;
        private Vector3[] _tBase, _tNorm, _tWork, _tWorkN;
        private bool _tDirty;

        /// <summary>Seconds for a released dent to mostly rise back (from the finish: foam slow, jelly quick).</summary>
        public float RiseTime = 2.4f;

        /// <summary>How many dents fit at once: more on a bigger squishy.</summary>
        public int MaxDents { get { return Mathf.Clamp(Mathf.RoundToInt(3 + Scale * StageScale * 30), 4, 16); } }

        public bool Dented { get { return _dents.Count > 0; } }

        private void EnsureTactileMesh()
        {
            if (_tMesh != null) return;
            var mf = Body.GetComponent<MeshFilter>();
            _tMesh = Object.Instantiate(mf.sharedMesh);
            _tMesh.name = "bao (tactile)";
            _tMesh.MarkDynamic();
            mf.sharedMesh = _tMesh;
            _tBase = _tMesh.vertices;
            _tNorm = _tMesh.normals;
            _tWork = new Vector3[_tBase.Length];
            _tWorkN = new Vector3[_tBase.Length];
        }

        private Dent Add(Vector3 local, float r, float depth)
        {
            if (_dents.Count >= MaxDents)
            {
                // The oldest released dent lets go first.
                int i = _dents.FindIndex(x => !x.held);
                _dents.RemoveAt(i >= 0 ? i : 0);
            }
            var d = new Dent { id = _nextDent++, p = local, r = r, depth = depth, max = r * .36f, held = true };
            _dents.Add(d);
            return d;
        }

        /// <summary>Starts a press at a world point on the body. Returns its id.</summary>
        public int PressAt(Vector3 world, float worldRadius)
        {
            EnsureTactileMesh();
            float s = Mathf.Max(.01f, Scale * StageScale);
            return Add(Body.InverseTransformPoint(world), Mathf.Clamp(worldRadius / s, .12f, .38f), 0).id;
        }

        /// <summary>Slides a held press to a new world point. Returns the id to keep using (a smear leaves a trail).</summary>
        public int MovePress(int id, Vector3 world)
        {
            var d = _dents.Find(x => x.id == id);
            if (d == null || !d.held) return id;
            var local = Body.InverseTransformPoint(world);
            if ((local - d.p).sqrMagnitude < d.r * d.r * .09f) return id;
            d.held = false;
            return Add(local, d.r, d.depth * .85f).id;
        }

        public void Release(int id)
        {
            var d = _dents.Find(x => x.id == id);
            if (d != null) d.held = false;
        }

        /// <summary>Pressing deepens held dents; released ones rise back slowly. Rebuilds the mesh when anything moved.</summary>
        private void StepTactile(float dt)
        {
            if (_dents.Count == 0 && !_tDirty) return;
            for (int i = _dents.Count - 1; i >= 0; i--)
            {
                var d = _dents[i];
                if (d.held) d.depth += (d.max - d.depth) * Mathf.Min(1, dt * 6);
                else
                {
                    d.depth *= Mathf.Exp(-dt * 2.3f / Mathf.Max(.2f, RiseTime));
                    if (d.depth < .002f) _dents.RemoveAt(i);
                }
            }
            ApplyDents();
        }

        private void ApplyDents()
        {
            if (_tMesh == null) return;
            if (_dents.Count == 0)
            {
                if (_tDirty) { _tMesh.vertices = _tBase; _tMesh.normals = _tNorm; _tMesh.RecalculateBounds(); _tDirty = false; }
                return;
            }
            System.Array.Copy(_tBase, _tWork, _tBase.Length);
            System.Array.Copy(_tNorm, _tWorkN, _tNorm.Length);
            foreach (var d in _dents)
            {
                float r2 = d.r * d.r, rim = d.r * 1.7f, rim2 = rim * rim;
                for (int v = 0; v < _tBase.Length; v++)
                {
                    var b = _tBase[v];
                    float dx = b.x - d.p.x, dy = b.y - d.p.y, dz = b.z - d.p.z, dd = dx * dx + dy * dy + dz * dz;
                    if (dd >= rim2 || dd < 1e-8f) continue;
                    float dist = Mathf.Sqrt(dd), push, slope;
                    if (dd < r2)
                    {
                        // A smooth bowl: (1 - s^2)^2, steepest part of its wall facing the dent's centre.
                        float s = dist / d.r, k = 1 - s * s;
                        push = -d.depth * k * k;
                        slope = d.depth * 4 * s * k / d.r;
                    }
                    else
                    {
                        // A soft bulge round the rim, as the squish pushes the foam aside.
                        float k = (dist - d.r) / (rim - d.r);
                        push = d.depth * .12f * Mathf.Sin(k * Mathf.PI);
                        slope = -d.depth * .12f * Mathf.PI * Mathf.Cos(k * Mathf.PI) / (rim - d.r);
                    }
                    var n = _tNorm[v];
                    _tWork[v] += n * push;
                    // Bend the normal: tilt it towards the dent's centre along the surface by the wall's slope.
                    var away = new Vector3(dx, dy, dz) / dist;
                    var tangent = away - n * Vector3.Dot(away, n);
                    _tWorkN[v] -= tangent * (slope * .6f); // gentler than the true slope: soft, not streaky
                }
            }
            for (int v = 0; v < _tWorkN.Length; v++) _tWorkN[v].Normalize();
            _tMesh.vertices = _tWork;
            _tMesh.normals = _tWorkN;
            _tMesh.RecalculateBounds();
            _tDirty = true;
        }
    }
}
