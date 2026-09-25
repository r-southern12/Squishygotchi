using System.Collections.Generic;
using Squishy.Runtime.Rendering;
using UnityEngine;

namespace Squishy.Runtime.Room
{
    /// <summary>
    /// The inside of the bamboo steamer: base ring, paper liner floor with punched holes, and a ring
    /// of slats. Slats on the camera side sink so the room is always visible.
    /// Placeholder geometry built in code; the art pass swaps in modelled pieces.
    /// </summary>
    public sealed class SteamerRoom : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Radius at room width 1x (level 4). Prototype: 2.65, so level 2 (0.77x) is 2.05.")]
        [SerializeField] private float fullWidthRadius = 2.65f;
        [SerializeField] private float wallHeight = 1.35f;
        [Tooltip("Distance between slat centres. Slats are 0.26 wide, so they touch like the prototype's (53 slats at level 2).")]
        [SerializeField] private float slatPitch = 0.243f;
        [SerializeField] private string tableColor = "#A87A4F";

        [Header("Cut-away")]
        [Tooltip("Slats facing the camera more than this (dot product) sink.")]
        [SerializeField] private float cutawayThreshold = 0.42f;
        [SerializeField, Range(0.05f, 1f)] private float loweredHeight = 0.2f;
        [SerializeField] private float cutawaySpeed = 7f;

        [Header("Look")]
        [SerializeField] private Material sceneryMaterial;
        [SerializeField] private string slatColorA = "#D6AE72";
        [SerializeField] private string slatColorB = "#CFA466";
        [SerializeField] private string capColor = "#D6AE72";
        [SerializeField] private string bandColor = "#A97E47";
        [SerializeField] private string linerColor = "#F2E7D2";
        [SerializeField] private string holeColor = "#9E7646";

        private readonly List<Slat> _slats = new List<Slat>();
        private MaterialPalette _palette;
        private Transform _root;

        public float Radius { get; private set; }
        /// <summary>Shared per-colour scenery materials; furniture reuses them.</summary>
        public MaterialPalette Palette { get { return _palette; } }
        /// <summary>Radius the squishy can walk within (inside the liner edge).</summary>
        public float WalkRadius { get { return Radius * 0.86f; } }
        public float FloorY { get; private set; }

        private sealed class Slat
        {
            public Vector3 Direction;
            public Transform Body, Cap, LowBand, HighBand;
            public float Height = 1f;
        }

        public void Build(float relativeWidth)
        {
            if (_root != null) Destroy(_root.gameObject);
            _slats.Clear();
            _palette = new MaterialPalette(sceneryMaterial);
            _root = new GameObject("Steamer").transform;
            _root.SetParent(transform, false);

            Radius = fullWidthRadius * relativeWidth;
            FloorY = 0.19f;

            // The table the steamer stands on.
            Part("Table", MeshKit.RoundedBox(new Vector3(9f, 0.6f, 9f), 0.2f), tableColor, new Vector3(0f, -0.6f, 0f), castShadows: false);
            Part("Base", MeshKit.Disc(Radius + 0.02f, 0.14f), bandColor, Vector3.zero);
            Part("Liner", MeshKit.Disc(Radius * 0.9f, 0.05f), linerColor, new Vector3(0f, 0.14f, 0f));

            // Punched holes in a staggered grid.
            var holeMesh = MeshKit.Disc(0.07f, 0.052f, 10);
            const float step = 0.42f;
            for (float x = -Radius; x <= Radius; x += step)
            for (float z = -Radius; z <= Radius; z += step)
            {
                float zz = z + (Mathf.RoundToInt(x / step) % 2 != 0 ? step * 0.5f : 0f);
                if (new Vector2(x, zz).magnitude < Radius * 0.82f)
                    Part("Hole", holeMesh, holeColor, new Vector3(x, 0.141f, zz), castShadows: false);
            }

            int count = Mathf.Max(12, Mathf.RoundToInt(2f * Mathf.PI * Radius / slatPitch));
            var slatMesh = MeshKit.RoundedBox(new Vector3(0.26f, wallHeight, 0.18f), 0.06f);
            var capMesh = MeshKit.RoundedBox(new Vector3(0.32f, 0.14f, 0.26f), 0.06f);
            var bandMesh = MeshKit.RoundedBox(new Vector3(0.33f, 0.16f, 0.08f), 0.03f);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var rot = Quaternion.LookRotation(dir);
                var s = new Slat { Direction = dir };
                s.Body = Part("Slat", slatMesh, SlatColor(i), dir * Radius, rot);
                s.Cap = Part("Cap", capMesh, capColor, dir * Radius, rot);
                s.LowBand = Part("Band", bandMesh, bandColor, dir * (Radius + 0.12f) + Vector3.up * 0.3f, rot);
                s.HighBand = Part("Band", bandMesh, bandColor, dir * (Radius + 0.12f) + Vector3.up * (wallHeight - 0.38f), rot);
                _slats.Add(s);
                Place(s);
            }
        }

        /// <summary>Sinks slats between the camera and the room centre.</summary>
        public void UpdateCutaway(Vector3 cameraPosition, float deltaTime)
        {
            Vector3 toCamera = cameraPosition - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-4f) return;
            toCamera.Normalize();

            float k = Mathf.Min(1f, deltaTime * cutawaySpeed);
            for (int i = 0; i < _slats.Count; i++)
            {
                var s = _slats[i];
                float target = Vector3.Dot(s.Direction, toCamera) > cutawayThreshold ? loweredHeight : 1f;
                if (Mathf.Abs(s.Height - target) < 0.002f) continue;
                s.Height += (target - s.Height) * k;
                Place(s);
            }
        }

        private void Place(Slat s)
        {
            s.Body.localScale = new Vector3(1f, s.Height, 1f);
            s.Cap.localPosition = s.Direction * Radius + Vector3.up * (wallHeight * s.Height - 0.07f);
            s.LowBand.gameObject.SetActive(wallHeight * s.Height > 0.5f);
            s.HighBand.gameObject.SetActive(s.Height > 0.92f);
        }

        /// <summary>Alternating bamboo tones with a little per-slat lightness variation, as in the prototype.</summary>
        private Color SlatColor(int i)
        {
            Color c = MaterialPalette.Hex(i % 2 == 0 ? slatColorB : slatColorA);
            Color.RGBToHSV(c, out float h, out float s, out float v);
            v = Mathf.Clamp01(v + ((i * 37) % 7 - 3) * 0.012f);
            return Color.HSVToRGB(h, s, v);
        }

        private Transform Part(string name, Mesh mesh, string hex, Vector3 position, Quaternion? rotation = null, bool castShadows = true)
        {
            return Part(name, mesh, MaterialPalette.Hex(hex), position, rotation, castShadows);
        }

        private Transform Part(string name, Mesh mesh, Color color, Vector3 position, Quaternion? rotation = null, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _palette.Get(color);
            r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
