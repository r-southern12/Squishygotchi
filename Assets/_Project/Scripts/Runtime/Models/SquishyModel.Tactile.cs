using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Tactile squishing (user request, 27 Sep 2026, after the real slow-rise dumpling toy): a finger presses a
    /// real dent into the surface where it touches, holding presses deeper, dragging smears it into a trail, and on
    /// release each dent slowly rises back like memory foam (jelly finishes come back quicker). Dents have a fixed
    /// size in the world, so a bigger squishy has more surface to play with and holds more dents at once.
    /// Normals are recalculated and shared across the seam, so dents shade truly with no crease.
    /// </summary>
    public sealed partial class SquishyModel
    {
        private sealed class Dent { public int id; public Vector3 p; public float r, depth, max; public bool held; }

        private readonly List<Dent> _dents = new List<Dent>();
        private int _nextDent = 1;
        private Mesh _tMesh;
        private Vector3[] _tBase, _tNorm, _tWork;
        private int[] _tRep; // the first vertex at the same spot (seam and pole duplicates)
        private static readonly Vector3 Core = new Vector3(0, .15f, 0); // dents push toward here
        private bool _tDirty;
        private float _tSquash; // how much held presses squeeze the whole body

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
            _tRep = new int[_tBase.Length];
            var first = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < _tBase.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(_tBase[i].x * 2000), Mathf.RoundToInt(_tBase[i].y * 2000), Mathf.RoundToInt(_tBase[i].z * 2000));
                if (!first.TryGetValue(k, out int f)) first[k] = f = i;
                _tRep[i] = f;
            }
        }

        private Dent Add(Vector3 local, float r, float depth)
        {
            if (_dents.Count >= MaxDents)
            {
                // The oldest released dent lets go first.
                int i = _dents.FindIndex(x => !x.held);
                _dents.RemoveAt(i >= 0 ? i : 0);
            }
            var d = new Dent { id = _nextDent++, p = local, r = r, depth = depth, max = r * .7f, held = true };
            _dents.Add(d);
            return d;
        }

        /// <summary>Starts a press at a world point on the body. Returns its id.</summary>
        public int PressAt(Vector3 world, float worldRadius)
        {
            EnsureTactileMesh();
            float s = Mathf.Max(.01f, Scale * StageScale);
            return Add(Body.InverseTransformPoint(world), Mathf.Clamp(worldRadius / s, .2f, .55f), 0).id;
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
            // The toy squashes a lot: holding presses sink the body as well as denting it.
            float target = 0;
            foreach (var d in _dents) if (d.held) target += d.depth / Mathf.Max(.01f, d.r);
            _tSquash += (Mathf.Min(.45f, target * .5f) - _tSquash) * Mathf.Min(1, dt * (target > 0 ? 6 : 2.3f / Mathf.Max(.2f, RiseTime)));
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
            // Pushed toward the body's centre (a smooth direction; the pleats' normals swing across each groove and
            // crossed neighbours into creases). Overlapping bowls add up but saturate softly, so there's no seam.
            float limit = 0;
            foreach (var d in _dents) limit = Mathf.Max(limit, d.max);
            limit = Mathf.Max(limit, .01f);
            for (int v = 0; v < _tBase.Length; v++)
            {
                var b = _tBase[v];
                float bowl = 0, bulge = 0;
                foreach (var d in _dents)
                {
                    float rim = d.r * 2.2f;
                    float dx = b.x - d.p.x, dy = b.y - d.p.y, dz = b.z - d.p.z, dd = dx * dx + dy * dy + dz * dz;
                    if (dd >= rim * rim) continue;
                    if (dd < d.r * d.r) { float k = 1 - dd / (d.r * d.r); bowl += d.depth * k * k * k; } // smooth bowl, soft edges
                    else { float w = Mathf.Sin((Mathf.Sqrt(dd) - d.r) / (rim - d.r) * Mathf.PI); bulge = Mathf.Max(bulge, d.depth * .45f * w * w); } // the foam pushed aside swells up round the dent
                }
                if (bowl == 0 && bulge == 0) { _tWork[v] = b; continue; }
                float sink = limit * (1 - Mathf.Exp(-bowl / limit));
                float push = bulge * (1 - sink / limit) - sink;
                _tWork[v] = b + (b - Core).normalized * push;
            }
            _tMesh.vertices = _tWork;
            _tMesh.RecalculateNormals();
            var n = _tMesh.normals;
            for (int v = 0; v < n.Length; v++) if (_tRep[v] != v) n[_tRep[v]] += n[v];
            for (int v = 0; v < n.Length; v++) n[v] = n[_tRep[v]].normalized;
            _tMesh.normals = n;
            _tMesh.RecalculateBounds();
            _tDirty = true;
        }
    }
}
