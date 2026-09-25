using Squishy.Runtime.Rendering;
using UnityEngine;

namespace Squishy.Runtime.Room
{
    /// <summary>How a placeholder piece behaves physically. Real models will carry this on their prefab.</summary>
    public struct PlaceholderShape
    {
        public float radius;
        /// <summary>Height the squishy sits at when using it; 0 means it stands in front instead.</summary>
        public float perchHeight;
        public bool blocks;
        /// <summary>Stand on the room-centre side instead of the item's front (plants, lamps).</summary>
        public bool faceCentre;
        public Renderer highlight;
    }

    /// <summary>
    /// PLACEHOLDER ART. Builds a chunky soft-block stand-in for each item type from the style palette
    /// (0 wood, 1 accent, 2 second accent, 3 dark, 4 light). The art pass replaces this with prefabs on SkinAsset.
    /// </summary>
    public sealed class PlaceholderFurniture
    {
        private readonly MaterialPalette _palette;
        private Transform _root;
        private Color[] _pal;

        public PlaceholderFurniture(MaterialPalette palette)
        {
            _palette = palette;
        }

        public PlaceholderShape Build(string itemTypeId, Transform root, Color[] palette)
        {
            _root = root;
            _pal = palette != null && palette.Length >= 5 ? palette : new[] { Hex("#C79A62"), Hex("#B8332E"), Hex("#6FA58E"), Hex("#3A302A"), Hex("#F2E3C6") };
            var s = new PlaceholderShape { blocks = true };
            Color wood = _pal[0], accent = _pal[1], accent2 = _pal[2], dark = _pal[3], light = _pal[4];

            switch (itemTypeId)
            {
                case "bed":
                    Box(new Vector3(0.9f, 0.18f, 1.2f), 0.06f, wood, 0f, 0f);
                    Box(new Vector3(0.82f, 0.12f, 1.02f), 0.06f, light, 0.18f, 0.06f);
                    Box(new Vector3(0.5f, 0.1f, 0.22f), 0.05f, accent, 0.3f, -0.4f);
                    Box(new Vector3(0.9f, 0.55f, 0.1f), 0.05f, wood, 0f, -0.6f);
                    s.radius = 0.6f; s.perchHeight = 0.3f;
                    break;
                case "stool":
                    for (int i = 0; i < 4; i++) Leg(i, 0.1f, 0.28f, wood);
                    Disc(0.17f, 0.07f, accent, 0.28f);
                    s.radius = 0.2f; s.perchHeight = 0.35f;
                    break;
                case "tea_table":
                    for (int i = 0; i < 4; i++) Leg(i, 0.25f, 0.28f, wood);
                    Box(new Vector3(0.72f, 0.07f, 0.52f), 0.03f, wood, 0.28f, 0f);
                    Disc(0.08f, 0.1f, light, 0.35f);
                    s.radius = 0.42f;
                    break;
                case "stove":
                    Box(new Vector3(0.62f, 0.6f, 0.5f), 0.06f, accent2, 0f, 0f);
                    Box(new Vector3(0.58f, 0.04f, 0.46f), 0.02f, dark, 0.6f, 0f);
                    Disc(0.14f, 0.08f, dark, 0.64f, new Vector3(0.12f, 0f, 0f));
                    Box(new Vector3(0.4f, 0.22f, 0.02f), 0.02f, dark, 0.15f, 0.25f);
                    s.radius = 0.36f;
                    break;
                case "pantry":
                    Box(new Vector3(0.55f, 0.95f, 0.45f), 0.07f, accent, 0f, 0f);
                    Box(new Vector3(0.02f, 0.8f, 0.02f), 0.01f, dark, 0.07f, 0.23f);
                    s.radius = 0.34f;
                    break;
                case "sink":
                    Box(new Vector3(0.5f, 0.55f, 0.42f), 0.06f, light, 0f, 0f);
                    Box(new Vector3(0.36f, 0.04f, 0.28f), 0.02f, accent2, 0.54f, 0.02f);
                    s.radius = 0.3f;
                    break;
                case "bathtub":
                    Box(new Vector3(0.95f, 0.4f, 0.62f), 0.16f, light, 0f, 0f);
                    Box(new Vector3(0.8f, 0.03f, 0.47f), 0.1f, accent2, 0.36f, 0f);
                    s.radius = 0.52f; s.perchHeight = 0.2f;
                    break;
                case "shower":
                    Box(new Vector3(0.62f, 0.08f, 0.62f), 0.04f, light, 0f, 0f);
                    Box(new Vector3(0.62f, 1.2f, 0.06f), 0.03f, accent2, 0f, -0.3f);
                    Box(new Vector3(0.04f, 0.9f, 0.5f), 0.02f, accent, 0.3f, 0f, new Vector3(0.3f, 0f, 0f));
                    s.radius = 0.36f; s.perchHeight = 0.08f;
                    break;
                case "ball":
                    s.highlight = Ball(0.14f, accent);
                    s.radius = 0.14f; s.blocks = false;
                    break;
                case "trampoline":
                    Disc(0.45f, 0.22f, dark, 0f);
                    Disc(0.4f, 0.03f, accent, 0.22f);
                    s.radius = 0.46f; s.perchHeight = 0.26f;
                    break;
                case "beanbag":
                    Blob(0.42f, accent);
                    s.radius = 0.42f; s.perchHeight = 0.28f;
                    break;
                case "floor_cushion":
                    Box(new Vector3(0.5f, 0.12f, 0.5f), 0.06f, accent, 0f, 0f);
                    s.radius = 0.28f; s.perchHeight = 0.12f;
                    break;
                case "lamp":
                    Disc(0.13f, 0.04f, dark, 0f);
                    Box(new Vector3(0.04f, 0.8f, 0.04f), 0.02f, dark, 0.04f, 0f);
                    s.highlight = Disc(0.2f, 0.22f, accent2, 0.78f);
                    s.radius = 0.2f; s.faceCentre = true;
                    break;
                case "plant":
                    Disc(0.15f, 0.22f, accent, 0f);
                    s.highlight = Blob(0.24f, Hex("#6F9A5A"), 0.2f);
                    s.radius = 0.2f; s.faceCentre = true;
                    break;
                case "shelf":
                    Box(new Vector3(0.72f, 0.9f, 0.26f), 0.04f, wood, 0f, 0f);
                    Box(new Vector3(0.64f, 0.03f, 0.2f), 0.01f, light, 0.42f, 0.03f);
                    Box(new Vector3(0.14f, 0.16f, 0.14f), 0.05f, accent, 0.9f, 0f, new Vector3(-0.15f, 0f, 0f));
                    s.radius = 0.38f;
                    break;
                case "wardrobe":
                    Box(new Vector3(0.75f, 1.15f, 0.42f), 0.06f, wood, 0f, 0f);
                    Box(new Vector3(0.02f, 1f, 0.02f), 0.01f, dark, 0.07f, 0.22f);
                    s.radius = 0.42f;
                    break;
                case "rug":
                    Disc(0.8f, 0.012f, accent, 0f);
                    Disc(0.62f, 0.014f, light, 0f);
                    s.radius = 0.8f; s.blocks = false;
                    break;
                case "wall_panel":
                    Box(new Vector3(1f, 1f, 0.1f), 0.04f, wood, 0f, 0f);
                    s.radius = 0.5f;
                    break;
                case "folding_screen":
                    for (int i = -1; i <= 1; i++) Box(new Vector3(0.34f, 0.9f, 0.05f), 0.02f, i == 0 ? light : accent, 0f, 0f, new Vector3(i * 0.33f, 0f, Mathf.Abs(i) * 0.06f));
                    s.radius = 0.5f;
                    break;
                case "tombstone":
                    Box(new Vector3(0.3f, 0.42f, 0.12f), 0.1f, Hex("#A7A29A"), 0f, 0f);
                    s.radius = 0.2f;
                    break;
                default:
                    Box(new Vector3(0.4f, 0.4f, 0.4f), 0.08f, accent, 0f, 0f);
                    s.radius = 0.25f;
                    break;
            }
            return s;
        }

        private Renderer Box(Vector3 size, float round, Color color, float y, float z, Vector3 offset = default(Vector3))
        {
            return Part(MeshKit.RoundedBox(size, round), color, new Vector3(offset.x, y + offset.y, z + offset.z), Vector3.one);
        }

        private Renderer Disc(float radius, float height, Color color, float y, Vector3 offset = default(Vector3))
        {
            return Part(MeshKit.Disc(radius, height, 24), color, new Vector3(offset.x, y, offset.z), Vector3.one);
        }

        private Renderer Ball(float radius, Color color)
        {
            return Part(MeshKit.RoundedBox(Vector3.one * radius * 2f, radius, 4), color, Vector3.zero, Vector3.one);
        }

        private Renderer Blob(float radius, Color color, float y = 0f)
        {
            return Part(MeshKit.Dumpling(24, 14), color, new Vector3(0f, y, 0f), Vector3.one * radius);
        }

        private void Leg(int corner, float spread, float height, Color color)
        {
            float x = (corner % 2 == 0 ? -1 : 1) * spread, z = (corner < 2 ? -1 : 1) * spread * 0.8f;
            Part(MeshKit.RoundedBox(new Vector3(0.05f, height, 0.05f), 0.02f), color, new Vector3(x, 0f, z), Vector3.one);
        }

        private Renderer Part(Mesh mesh, Color color, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject("Placeholder_Part");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _palette.Get(color);
            return r;
        }

        private static Color Hex(string hex)
        {
            return MaterialPalette.Hex(hex);
        }
    }
}
