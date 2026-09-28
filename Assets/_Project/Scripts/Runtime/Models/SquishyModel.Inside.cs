using System.Collections.Generic;
using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Squishy.Runtime.Models
{
    /// <summary>
    /// Clear jelly squishies (user request, 28 Sep 2026): the body is see-through (firmer at the rim), and things live
    /// inside it: suspended glitter, snow, glowing motes, boba pearls, koi and goldfish. They drift and settle on their
    /// own, scatter away from a finger press, and slosh when the squishy squashes or bounces.
    /// Everything is in the body's own space, so it squashes and dents along with it.
    /// </summary>
    public sealed partial class SquishyModel
    {
        private sealed class Mote { public Vector3 p, v; public float phase; }
        private sealed class Swimmer { public Transform t, tail; public Vector3 p, v, goal; public float speed, flee, wag; }

        private Transform _inside;
        private readonly List<Mote> _motes = new List<Mote>();
        private readonly List<Swimmer> _fish = new List<Swimmer>();
        private readonly List<Transform> _pearls = new List<Transform>();
        private readonly List<Mote> _pearlMotes = new List<Mote>();
        private Mesh _moteMesh;
        private Vector3[] _moteVerts;
        private string _insideKind;
        private float _prevInsideX;
        private Transform _backing;

        /// <summary>
        /// Catalogue photos only: a soft tinted wall behind the contents (like the scene in a snow globe), so a clear
        /// squishy reads on a card instead of vanishing into the background. In the room it stays truly see-through.
        /// </summary>
        public void ThumbBacking(bool on)
        {
            bool want = on && Fin != null && Fin.clear;
            if (want && _backing == null)
            {
                var m = ThreeMat.Standard(Color.Lerp(ThreeMat.Lin(Fin.color), Color.white, .25f), .6f);
                m.SetFloat("_Cull", (float)CullMode.Front); // the inside of the back wall
                _backing = Node.Mesh(Body, Body.GetComponent<MeshFilter>().sharedMesh, m, 0, 0, 0, shadow: false);
                _backing.localScale = Vector3.one * .985f;
                _backing.gameObject.layer = Body.gameObject.layer;
            }
            if (_backing != null)
            {
                if (want) _backing.GetComponent<Renderer>().sharedMaterial.SetVector("_BaseColor", Color.Lerp(ThreeMat.Lin(Fin.color), Color.white, .25f));
                _backing.gameObject.SetActive(want);
                var bc = Mat.GetVector("_BaseColor");
                bc.w = want ? _base.a * .45f : _base.a; // photos: a clearer front so the contents read at card size
                Mat.SetVector("_BaseColor", bc);
            }
        }

        /// <summary>How far from the middle the inside can go in a direction (a little inside the skin).</summary>
        private static float Room(Vector3 dir, float margin) { return ShapeAt(dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up).magnitude * margin; }

        private static Vector3 RandomInside(System.Random r, float margin)
        {
            var d = new Vector3((float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 1.6f - .7f, (float)r.NextDouble() * 2 - 1);
            if (d.sqrMagnitude < 1e-4f) d = Vector3.up;
            return d.normalized * Room(d, margin) * Mathf.Pow((float)r.NextDouble(), .5f);
        }

        /// <summary>Sets the body see-through (or back to solid) and builds what lives inside.</summary>
        private void SetupInside(FinishData f)
        {
            bool clear = f.clear;
            Mat.SetFloat("_SrcBlend", clear ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            Mat.SetFloat("_DstBlend", clear ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            Mat.SetFloat("_ZWrite", clear ? 0 : 1);
            Mat.SetFloat("_Clear", clear ? 1 : 0);
            Mat.renderQueue = clear ? (int)RenderQueue.Transparent : -1;
            FrontParts(clear);

            string kind = clear ? f.inside : null;
            if (kind == _insideKind && _inside != null && kind != null) { Recolour(f); return; }
            if (_inside != null) { _inside.gameObject.SetActive(false); Object.Destroy(_inside.gameObject); } // hidden now: Destroy waits for the frame end
            _inside = null;
            _motes.Clear();
            _fish.Clear();
            _pearls.Clear();
            _pearlMotes.Clear();
            _insideKind = kind;
            if (string.IsNullOrEmpty(kind)) return;
            _inside = Node.Group(Body, "inside");
            _inside.gameObject.layer = Body.gameObject.layer;
            var rnd = new System.Random(f.name.GetHashCode());
            int n = Mathf.Max(1, f.insideCount);
            if (kind == "glitter" || kind == "snow" || kind == "glow") BuildMotes(f, kind, n, rnd);
            else if (kind == "boba") BuildPearls(f, n, rnd);
            else if (kind == "koi" || kind == "goldfish")
            {
                for (int i = 0; i < n; i++) _fish.Add(BuildFish(f, kind, i, rnd));
                BuildMotes(f, "bubbles", 16, rnd); // a few bubbles drifting up
            }
            Node.SetLayer(_inside, Body.gameObject.layer);
        }

        /// <summary>Eyes, mouth, blush and accessories draw over the clear body (not tinted behind it).</summary>
        private void FrontParts(bool clear)
        {
            var bodyR = Body.GetComponent<Renderer>();
            foreach (var r in Body.GetComponentsInChildren<Renderer>(true))
            {
                if (r == bodyR || (_inside != null && r.transform.IsChildOf(_inside)) || (_backing != null && r.transform == _backing)) continue;
                if (r.sharedMaterial == Mat) continue; // made of the body itself (the cowlick): it keeps the live body colour
                if (clear)
                {
                    if (!_origMats.ContainsKey(r)) { _origMats[r] = r.sharedMaterial; r.sharedMaterial = new Material(r.sharedMaterial); }
                    r.sharedMaterial.renderQueue = 3001;
                }
                else if (_origMats.TryGetValue(r, out var orig)) { r.sharedMaterial = orig; _origMats.Remove(r); }
            }
        }

        private readonly Dictionary<Renderer, Material> _origMats = new Dictionary<Renderer, Material>();

        private void Recolour(FinishData f) { if (_moteMesh != null) _moteMesh.colors = MoteColours(f, _insideKind, _motes.Count); }

        private static Color[] MoteColours(FinishData f, string kind, int n)
        {
            string[] pal = kind == "glow" ? new[] { string.IsNullOrEmpty(f.glow) ? "#FFFFFF" : f.glow }
                : kind == "bubbles" ? new[] { "#FFFFFF", "#E6F7FF" }
                : f.insideColors != null && f.insideColors.Length > 0 ? f.insideColors
                : f.spark != null && f.spark.Length > 0 ? f.spark : new[] { "#FFFFFF" };
            var cols = new Color[n * 4];
            for (int j = 0; j < n; j++)
            {
                var c = ThreeMat.Lin(pal[j % pal.Length]) * (kind == "glow" ? 3f : kind == "snow" ? .9f : 1.2f);
                for (int k = 0; k < 4; k++) cols[j * 4 + k] = c;
            }
            return cols;
        }

        /// <summary>Tiny camera-facing flecks (the sparkle sprites) floating in the jelly.</summary>
        private void BuildMotes(FinishData f, string kind, int n, System.Random rnd)
        {
            _moteVerts = new Vector3[n * 4];
            var corners = new Vector2[n * 4];
            var phase = new Vector2[n * 4];
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                var m = new Mote { p = RandomInside(rnd, .8f), phase = (float)rnd.NextDouble() * 7 };
                _motes.Add(m);
                for (int c = 0; c < 4; c++)
                {
                    _moteVerts[i * 4 + c] = m.p;
                    corners[i * 4 + c] = new Vector2(c == 0 || c == 3 ? -1 : 1, c < 2 ? -1 : 1) * (kind == "snow" ? 1.2f : kind == "glow" ? 1.5f : kind == "bubbles" ? .9f : .75f);
                    phase[i * 4 + c] = new Vector2(m.phase, 0);
                }
                tris[i * 6] = i * 4; tris[i * 6 + 1] = i * 4 + 2; tris[i * 6 + 2] = i * 4 + 1;
                tris[i * 6 + 3] = i * 4; tris[i * 6 + 4] = i * 4 + 3; tris[i * 6 + 5] = i * 4 + 2;
            }
            _moteMesh = new Mesh { name = "inside " + kind, vertices = _moteVerts, uv = corners, uv2 = phase, colors = MoteColours(f, kind, n), triangles = tris };
            _moteMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 3f);
            _moteMesh.MarkDynamic();
            var mat = ThreeMat.Sparkles();
            mat.renderQueue = 2990; // before the clear body, so the jelly tints over them
            var t = Node.Mesh(_inside, _moteMesh, mat, 0, 0, 0, shadow: false);
            t.name = kind;
            if (kind == "bubbles") foreach (var m in _motes) m.v = Vector3.up * .1f;
        }

        private void BuildPearls(FinishData f, int n, System.Random rnd)
        {
            var cols = f.insideColors != null && f.insideColors.Length > 0 ? f.insideColors : new[] { "#2E1B14" };
            for (int i = 0; i < n; i++)
            {
                var m = new Mote { p = RandomInside(rnd, .72f) };
                m.p.y = -.25f - (float)rnd.NextDouble() * .2f; // settled at the bottom
                _pearlMotes.Add(m);
                var mat = ThreeMat.Standard(ThreeMat.Lin(cols[i % cols.Length]), .25f);
                _pearls.Add(Node.Mesh(_inside, ThreeGeo.Sph(.085f, 10, 8), mat, m.p.x, m.p.y, m.p.z, shadow: false));
            }
        }

        /// <summary>A little fish built along +z: body, patches, tail, fins and eyes. Koi are patchy; goldfish frilly.</summary>
        private Swimmer BuildFish(FinishData f, string kind, int i, System.Random rnd)
        {
            var cols = f.insideColors != null && f.insideColors.Length > 0 ? f.insideColors : new[] { "#F28A2E" };
            var fish = Node.Group(_inside, "fish" + i);
            bool koi = kind == "koi";
            string bodyHex = koi ? (i % 3 == 0 ? cols[0] : cols[Mathf.Min(2, cols.Length - 1)]) : cols[0];
            var body = ThreeMat.Standard(ThreeMat.Lin(bodyHex), .35f);
            Node.Mesh(fish, ThreeGeo.Sph(.07f, 12, 10), body, 0, 0, 0, shadow: false).Scale(.75f, .8f, 1.7f);
            if (koi)
                for (int k = 0; k < 3; k++) // patches of orange and black
                {
                    var patch = ThreeMat.Standard(ThreeMat.Lin(cols[1 + (k + i) % (cols.Length - 1)]), .35f);
                    Node.Mesh(fish, ThreeGeo.Sph(.042f, 10, 8), patch, (k - 1) * .018f, .035f, .06f - k * .05f, shadow: false).Scale(1, .5f, 1.3f);
                }
            var tail = Node.Group(fish, "tail", 0, 0, -.11f);
            var fin = ThreeMat.Standard(ThreeMat.Lin(koi ? bodyHex : cols[Mathf.Min(1, cols.Length - 1)]), .4f);
            float tl = koi ? 1 : 1.5f; // goldfish: big flowing tail
            Node.Mesh(tail, ThreeGeo.Sph(.05f, 10, 8), fin, 0, .02f, -.03f * tl, shadow: false).Scale(.18f, .9f * tl, 1.1f * tl);
            Node.Mesh(tail, ThreeGeo.Sph(.05f, 10, 8), fin, 0, -.02f, -.03f * tl, shadow: false).Scale(.18f, .9f * tl, 1.1f * tl);
            Node.Mesh(fish, ThreeGeo.Sph(.04f, 8, 6), fin, 0, .05f, -.01f, shadow: false).Scale(.15f, .8f, 1.4f); // dorsal fin
            var eye = ThreeMat.Basic(ThreeMat.Lin("#1C1418"));
            foreach (float sx in new[] { -1f, 1f }) Node.Mesh(fish, ThreeGeo.Sph(.011f, 6, 5), eye, sx * .042f, .015f, .09f, shadow: false);
            float s = Mathf.Lerp(.9f, 1.15f, (float)rnd.NextDouble());
            fish.localScale = Vector3.one * s * 1.4f;
            var p = RandomInside(rnd, .55f);
            fish.localPosition = p;
            return new Swimmer { t = fish, tail = tail, p = p, v = Random.onUnitSphere * .1f, goal = RandomInside(rnd, .55f), speed = Mathf.Lerp(.22f, .32f, (float)rnd.NextDouble()), wag = (float)rnd.NextDouble() * 6 };
        }

        /// <summary>A finger pressed here (body space): everything nearby scatters, fish dart away.</summary>
        private void PokeInside(Vector3 local, float strength)
        {
            if (_inside == null) return;
            foreach (var m in _motes) Push(m, local, strength, .8f, 1.4f);
            foreach (var m in _pearlMotes) Push(m, local, strength, .7f, 1.1f);
            foreach (var s in _fish)
            {
                var d = s.p - local;
                float dist = d.magnitude;
                if (dist > 1f) continue;
                s.v = (d.sqrMagnitude > 1e-6f ? d / dist : Random.onUnitSphere) * (.9f + .6f * (1 - dist)) * strength;
                s.flee = 1.2f;
                s.goal = s.p + s.v.normalized * .5f;
            }
        }

        private static void Push(Mote m, Vector3 at, float strength, float reach, float power)
        {
            var d = m.p - at;
            float dist = d.magnitude;
            if (dist > reach) return;
            var away = dist > 1e-4f ? d / dist : Random.onUnitSphere;
            var swirl = Vector3.Cross(away, Vector3.up) * .5f;
            m.v += (away + swirl) * power * strength * (1 - dist / reach);
        }

        private void StepInside(float dt, float squash)
        {
            if (_inside == null || dt <= 0) { _prevInsideX = squash; return; }
            // A squash or bounce jolts the contents: they slosh up and out, then settle again.
            float jolt = Mathf.Clamp((squash - _prevInsideX) / dt, -6, 6);
            _prevInsideX = squash;
            bool snow = _insideKind == "snow", glow = _insideKind == "glow";
            string kind = _insideKind;
            for (int i = 0; i < _motes.Count; i++)
            {
                var m = _motes[i];
                bool bubble = kind == "koi" || kind == "goldfish";
                m.v += (m.p.sqrMagnitude > 1e-6f ? m.p.normalized : Vector3.up) * jolt * .05f + Vector3.up * (-jolt * .06f);
                // Glitter and snow sink slowly and swirl; glow motes hover; bubbles rise and start again at the bottom.
                if (bubble) m.v.y += .25f * dt;
                else if (snow) { m.v.y -= .05f * dt; m.v += new Vector3(Mathf.Sin(Time.time * .7f + m.phase), 0, Mathf.Cos(Time.time * .6f + m.phase)) * .03f * dt; }
                else if (glow) m.v += new Vector3(Mathf.Sin(Time.time * .9f + m.phase), Mathf.Cos(Time.time * .8f + m.phase * 1.3f), Mathf.Sin(Time.time * .7f + m.phase * .7f)) * .05f * dt;
                else m.v.y -= .03f * dt;
                m.v *= Mathf.Exp(-(bubble ? .6f : 1.3f) * dt);
                m.p += m.v * dt;
                float lim = Room(m.p, bubble ? .74f : .8f);
                if (m.p.magnitude > lim)
                {
                    if (bubble && m.p.y > 0) { m.p = new Vector3(Random.Range(-.3f, .3f), -.3f, Random.Range(-.3f, .3f)); m.v = Vector3.up * .08f; }
                    else { var n = m.p.normalized; m.p = n * lim; m.v -= n * Vector3.Dot(m.v, n) * 1.5f; }
                }
                for (int c = 0; c < 4; c++) _moteVerts[i * 4 + c] = m.p;
            }
            if (_moteMesh != null && _motes.Count > 0) _moteMesh.vertices = _moteVerts;

            for (int i = 0; i < _pearlMotes.Count; i++)
            {
                var m = _pearlMotes[i];
                m.v += (m.p.sqrMagnitude > 1e-6f ? m.p.normalized : Vector3.up) * jolt * .08f + Vector3.up * (-jolt * .12f);
                m.v.y -= .9f * dt; // pearls sink and pile at the bottom
                m.v *= Mathf.Exp(-2f * dt);
                m.p += m.v * dt;
                float lim = Room(m.p, .72f);
                if (m.p.magnitude > lim) { var n = m.p.normalized; m.p = n * lim; m.v -= n * Vector3.Dot(m.v, n) * 1.3f; m.v *= .8f; }
                _pearls[i].localPosition = m.p;
            }

            foreach (var s in _fish)
            {
                // Wander towards a goal, turn back from the skin, dart when poked, then calm down.
                if ((s.goal - s.p).sqrMagnitude < .02f || Random.value < dt * .15f) s.goal = RandomInside(new System.Random(Random.Range(0, 1 << 30)), .55f);
                var want = (s.goal - s.p).normalized * s.speed;
                s.flee = Mathf.Max(0, s.flee - dt);
                float steer = s.flee > 0 ? .6f : 1.6f;
                s.v += (want - s.v) * Mathf.Min(1, dt * steer);
                s.v += Vector3.up * (-jolt * .05f);
                s.p += s.v * dt;
                float lim = Room(s.p, .6f);
                if (s.p.magnitude > lim) { var n = s.p.normalized; s.p = n * lim; s.v -= n * Vector3.Dot(s.v, n) * 1.2f; s.goal = RandomInside(new System.Random(Random.Range(0, 1 << 30)), .45f); }
                s.t.localPosition = s.p;
                var flat = new Vector3(s.v.x, s.v.y * .4f, s.v.z);
                if (flat.sqrMagnitude > 1e-5f) s.t.localRotation = Quaternion.Slerp(s.t.localRotation, Quaternion.LookRotation(flat, Vector3.up), Mathf.Min(1, dt * 6));
                float spd = s.v.magnitude;
                s.wag += dt * (5 + spd * 30);
                s.tail.localRotation = Quaternion.Euler(0, Mathf.Sin(s.wag) * (18 + spd * 60), 0);
            }
        }
    }
}
