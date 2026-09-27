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
            var local = Body.InverseTransformPoint(world);
            PokeInside(local, 1);
            return Add(local, Mathf.Clamp(worldRadius / s, .2f, .55f), 0).id;
        }

        /// <summary>Slides a held press to a new world point. Returns the id to keep using (a smear leaves a trail).</summary>
        public int MovePress(int id, Vector3 world)
        {
            var d = _dents.Find(x => x.id == id);
            if (d == null || !d.held) return id;
            var local = Body.InverseTransformPoint(world);
            if ((local - d.p).sqrMagnitude < d.r * d.r * .09f) return id;
            d.held = false;
            PokeInside(local, .6f);
            return Add(local, d.r, d.depth * .85f).id;
        }

        /// <summary>Lets go of every held press (a touch that ended without a release: pinch, cancel, a new tap).</summary>
        public void ReleaseAll() { foreach (var d in _dents) d.held = false; }

        public bool Holding { get { foreach (var d in _dents) if (d.held) return true; return false; } }

        public void Release(int id)
        {
            var d = _dents.Find(x => x.id == id);
            if (d != null) d.held = false;
        }

        // ---- pinch and squeeze (two fingers, zoomed in) ----
        private bool _pinchOn;
        private Vector3 _pinchA, _pinchB;
        private float _pinch, _pinchT;
        private const float PinchR = .62f, PinchDepth = .58f;

        /// <summary>Two fingers on the squishy: it squeezes between them (see SetPinch) and balloons out elsewhere.</summary>
        public void BeginPinch(Vector3 worldA, Vector3 worldB)
        {
            EnsureTactileMesh();
            _pinchA = Body.InverseTransformPoint(worldA);
            _pinchB = Body.InverseTransformPoint(worldB);
            _pinchOn = true;
            _pinchT = 0;
        }

        /// <summary>0 = fingers where they started, 1 = squeezed as far as it goes.</summary>
        public void SetPinch(float amount) { if (_pinchOn) _pinchT = Mathf.Clamp01(amount); }

        public void EndPinch() { _pinchOn = false; _pinchT = 0; }

        public bool Pinching { get { return _pinchOn; } }

        // ---- draping over what it sits on ----
        private float _supR = 1, _drape, _drapeT, _drapeApplied;

        /// <summary>
        /// Sitting or lying on something with a top of this radius (body units): the underside sags down round its
        /// edges and the sides bulge a little, so a big squishy on a small stool envelops it. 0 lets it rise back.
        /// </summary>
        public void SetSupport(float radius, bool on)
        {
            if (on) { EnsureTactileMesh(); _supR = Mathf.Max(.05f, radius); }
            _drapeT = on ? 1 : 0;
        }

        /// <summary>How far a point sags straight down while draped (the bottom band, outside the seat).</summary>
        private float Sag(Vector3 b, out float side)
        {
            side = 0;
            if (_drape <= 0) return 0;
            float rho = Mathf.Sqrt(b.x * b.x + b.z * b.z), over = Mathf.SmoothStep(0, 1, (rho - _supR) / .35f);
            if (over <= 0) return 0;
            float h = b.y - B;
            side = _drape * .05f * over * Mathf.SmoothStep(0, 1, h / .25f) * (1 - Mathf.SmoothStep(0, 1, (h - .25f) / .5f));
            return _drape * .24f * over * (1 - Mathf.SmoothStep(0, 1, h / .6f));
        }

        /// <summary>Pressing deepens held dents; released ones rise back slowly. Rebuilds the mesh when anything moved.</summary>
        private void StepTactile(float dt)
        {
            // Held presses give the whole body a little squash; the rest of the volume goes into the swell (see Push).
            float target = 0;
            foreach (var d in _dents) if (d.held) target += d.depth / Mathf.Max(.01f, d.r);
            target += _pinch * .6f;
            _tSquash += (Mathf.Min(.22f, target * .25f) - _tSquash) * Mathf.Min(1, dt * (target > 0 ? 6 : 2.3f / Mathf.Max(.2f, RiseTime)));
            // The squeeze follows the fingers quickly, and rises back like memory foam when they let go.
            _pinch += (_pinchT - _pinch) * Mathf.Min(1, dt * (_pinchT > _pinch ? 10 : 2.3f / Mathf.Max(.2f, RiseTime)));
            if (_pinch < .002f && !_pinchOn) _pinch = 0;
            if (_pinch > .02f) { PokeInside(_pinchA, _pinch * dt * 4); PokeInside(_pinchB, _pinch * dt * 4); }
            _drape += (_drapeT - _drape) * Mathf.Min(1, dt * (_drapeT > _drape ? 3 : 1.5f));
            if (_drape < .002f && _drapeT <= 0) _drape = 0;
            bool draping = Mathf.Abs(_drape - _drapeApplied) > .003f;
            if (_dents.Count == 0 && _pinch <= 0 && !draping && (!_tDirty || _drape > 0)) return;
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

        // Per-frame deformation constants (shared by the mesh and the face riders).
        private float _limit = .01f, _swell;

        /// <summary>
        /// How far a point of the surface moves along its outward direction: sunk in under each press and each pinch
        /// point (smooth bowls that saturate softly), a soft lip round each dent, and a gentle swell everywhere else
        /// holding the displaced volume, like fluid in a balloon.
        /// </summary>
        private float Push(Vector3 b)
        {
            float bowl = 0, lip = 0, near = 0;
            foreach (var d in _dents)
            {
                float rim = d.r * 2.2f;
                float dx = b.x - d.p.x, dy = b.y - d.p.y, dz = b.z - d.p.z, dd = dx * dx + dy * dy + dz * dz;
                if (dd >= rim * rim) continue;
                if (dd < d.r * d.r) { float k = 1 - dd / (d.r * d.r); bowl += d.depth * k * k * k; near = 1; }
                else
                {
                    float u = (Mathf.Sqrt(dd) - d.r) / (rim - d.r), w = Mathf.Sin(u * Mathf.PI);
                    lip = Mathf.Max(lip, d.depth * .45f * w * w);
                    near = Mathf.Max(near, 1 - u);
                }
            }
            if (_pinch > 0)
            {
                float r2 = PinchR * PinchR;
                for (int i = 0; i < 2; i++)
                {
                    var c = i == 0 ? _pinchA : _pinchB;
                    float dd = (b - c).sqrMagnitude;
                    if (dd < r2 * 4) near = Mathf.Max(near, 1 - Mathf.Sqrt(dd) / (PinchR * 2));
                    if (dd < r2) { float k = 1 - dd / r2; bowl += _pinch * PinchDepth * k * k; }
                }
            }
            float sink = _limit * (1 - Mathf.Exp(-bowl / _limit));
            return lip * (1 - sink / _limit) - sink + _swell * (1 - near);
        }

        private void ApplyDents()
        {
            if (_tMesh == null) return;
            _drapeApplied = _drape;
            if (_dents.Count == 0 && _pinch <= 0 && _drape <= 0)
            {
                if (_tDirty) { _tMesh.vertices = _tBase; _tMesh.normals = _tNorm; _tMesh.RecalculateBounds(); _tDirty = false; RideSurface(true); }
                return;
            }
            // The volume pressed in (roughly depth x area of each bowl) comes back out as a swell over the rest.
            _limit = .01f;
            float volume = 0;
            foreach (var d in _dents) { _limit = Mathf.Max(_limit, d.max); volume += d.depth * d.r * d.r; }
            if (_pinch > 0) { _limit = Mathf.Max(_limit, PinchDepth); volume += 2 * _pinch * PinchDepth * PinchR * PinchR * .6f; }
            _swell = Mathf.Min(.16f, volume * .55f);
            for (int v = 0; v < _tBase.Length; v++)
            {
                var b = _tBase[v];
                float push = Push(b), sag = Sag(b, out float side);
                _tWork[v] = push == 0 && sag == 0 && side == 0 ? b : b + (b - Core).normalized * (push + side) + Vector3.down * sag;
            }
            _tMesh.vertices = _tWork;
            _tMesh.RecalculateNormals();
            var n = _tMesh.normals;
            for (int v = 0; v < n.Length; v++) if (_tRep[v] != v) n[_tRep[v]] += n[v];
            for (int v = 0; v < n.Length; v++) n[v] = n[_tRep[v]].normalized;
            _tMesh.normals = n;
            _tMesh.RecalculateBounds();
            _tDirty = true;
            RideSurface(false);
        }

        /// <summary>Where a resting surface point is now (dents, pinch, swell and drape).</summary>
        private Vector3 Displaced(Vector3 a)
        {
            float sag = Sag(a, out float side);
            return a + (a - Core).normalized * (Push(a) + side) + Vector3.down * sag;
        }

        // ---- the face rides the surface ----
        private sealed class Rider { public Transform t; public Vector3 basePos, anchor; public Quaternion baseRot; }
        private readonly List<Rider> _riders = new List<Rider>();

        /// <summary>
        /// Eyes, mouth, blush, brows, cowlick and accessories move with the skin under them: pressed in with a dent,
        /// carried out by the swell, tilted with the slope, and back to rest when it rises.
        /// </summary>
        private void RideSurface(bool rest)
        {
            CollectRiders();
            foreach (var r in _riders)
            {
                if (r.t == null) continue;
                if (rest) { r.t.localPosition = r.basePos; r.t.localRotation = r.baseRot; continue; }
                var a = r.anchor;
                var dir = (a - Core).normalized;
                var t1 = Vector3.Cross(dir, Vector3.up);
                if (t1.sqrMagnitude < 1e-4f) t1 = Vector3.right;
                t1.Normalize();
                var t2 = Vector3.Cross(dir, t1);
                const float e = .05f;
                Vector3 a1 = a + t1 * e, a2 = a + t2 * e;
                var p0 = Displaced(a);
                var p1 = Displaced(a1);
                var p2 = Displaced(a2);
                // Tilt with the surface: the displaced patch round the anchor against the resting one.
                var nNew = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                var nOld = Vector3.Cross(a1 - a, a2 - a).normalized;
                if (Vector3.Dot(nNew, dir) < 0) nNew = -nNew;
                if (Vector3.Dot(nOld, dir) < 0) nOld = -nOld;
                r.t.localPosition = r.basePos + (p0 - a);
                r.t.localRotation = Quaternion.FromToRotation(nOld, nNew) * r.baseRot;
            }
        }

        /// <summary>Everything sitting on the skin: direct children of the body, or children of groups at its centre.</summary>
        private void CollectRiders()
        {
            _riders.RemoveAll(r => r.t == null);
            foreach (Transform c in Body)
            {
                if (c == _inside || c == _backing) continue;
                if (c.localPosition.sqrMagnitude > .04f) AddRider(c, c.localPosition);
                else if (c.GetComponent<MeshFilter>() == null) foreach (Transform g in c) if (g.localPosition.sqrMagnitude > .04f) AddRider(g, g.localPosition); // groups at the centre (brows)
            }
        }

        private void AddRider(Transform t, Vector3 anchor)
        {
            foreach (var r in _riders) if (r.t == t) return;
            _riders.Add(new Rider { t = t, basePos = t.localPosition, anchor = anchor, baseRot = t.localRotation });
        }
    }
}
