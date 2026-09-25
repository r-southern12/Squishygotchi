using System.Collections.Generic;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Squishy.Runtime.Three
{
    /// <summary>
    /// The prototype's materials: m(hex) Lambert (optional emissive at .55), pattern Lambert (map repeated 3x3,
    /// optionally double-sided), MeshStandard (squishy) and MeshBasic (blush, glow, rays, confetti).
    /// Colours are given as sRGB hex and converted to linear, like three.js lin().
    /// </summary>
    public static class ThreeMat
    {
        private static Material _lambert, _standard, _basic, _sparkles;
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        private static Material Base(ref Material m, string name)
        {
            if (m == null) m = Resources.Load<Material>("Materials/" + name);
            return m;
        }

        public static Color Lin(string hex) { return Hex(hex).linear; }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        /// <summary>m(hex, emissive): shared matte material.</summary>
        public static Material M(string hex, string emissive = null, bool receiveShadows = true)
        {
            string key = "m" + hex + "|" + emissive + (receiveShadows ? "" : "|nr");
            if (Cache.TryGetValue(key, out var mat)) return mat;
            mat = Lambert(Lin(hex), emissive != null ? Lin(emissive) * .55f : (Color?)null);
            mat.name = "m " + hex;
            if (!receiveShadows) mat.SetFloat("_ReceiveShadows", 0f);
            return Cache[key] = mat;
        }

        /// <summary>A fresh Lambert (for colours that change: slats, liner, lamp shade).</summary>
        public static Material Lambert(Color linear, Color? emissive = null, Texture map = null, bool doubleSided = false)
        {
            var mat = new Material(Base(ref _lambert, "Lambert"));
            mat.SetVector("_BaseColor", linear);
            mat.SetVector("_EmissionColor", emissive ?? Color.black);
            if (map != null) mat.SetTexture("_BaseMap", map);
            if (doubleSided) mat.SetFloat("_Cull", (float)CullMode.Off);
            return mat;
        }

        /// <summary>pat(style) / patDS(style).</summary>
        public static Material Pattern(StyleData s, bool doubleSided = false)
        {
            string key = "pat" + s.id + (doubleSided ? "ds" : "");
            if (Cache.TryGetValue(key, out var mat)) return mat;
            mat = Lambert(Color.white, null, Textures.Pattern(s), doubleSided);
            mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            mat.name = key;
            return Cache[key] = mat;
        }

        /// <summary>MeshStandardMaterial (roughness, metalness, emissive, optional map).</summary>
        public static Material Standard(Color linear, float roughness, float metalness = 0f)
        {
            var mat = new Material(Base(ref _standard, "Standard"));
            mat.EnableKeyword("_STANDARD");
            mat.SetVector("_BaseColor", linear);
            mat.SetFloat("_Roughness", roughness);
            mat.SetFloat("_Metalness", metalness);
            mat.SetVector("_EmissionColor", Color.black);
            return mat;
        }

        public enum Blend { Opaque, Alpha, Additive }

        /// <summary>MeshBasicMaterial.</summary>
        public static Material Basic(Color linear, float opacity = 1f, Blend blend = Blend.Opaque, Texture map = null, bool doubleSided = false, bool depthWrite = true)
        {
            var mat = new Material(Base(ref _basic, "Basic"));
            linear.a = opacity;
            mat.SetVector("_BaseColor", linear);
            if (map != null) mat.SetTexture("_BaseMap", map);
            mat.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            SetBlend(mat, blend, depthWrite);
            return mat;
        }

        public static void SetBlend(Material mat, Blend blend, bool depthWrite)
        {
            mat.SetFloat("_SrcBlend", blend == Blend.Opaque ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", blend == Blend.Opaque ? (float)BlendMode.Zero : blend == Blend.Additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", depthWrite ? 1f : 0f);
            mat.renderQueue = blend == Blend.Opaque ? (int)RenderQueue.Geometry : (int)RenderQueue.Transparent;
        }

        public static void SetOpacity(Material mat, float opacity)
        {
            var c = mat.GetVector("_BaseColor");
            c.w = opacity;
            mat.SetVector("_BaseColor", c);
        }

        public static Material Sparkles()
        {
            var mat = new Material(Base(ref _sparkles, "Sparkles"));
            mat.SetTexture("_BaseMap", Textures.Sparkle());
            return mat;
        }

        /// <summary>THREE.Color(hex).offsetHSL(h, s, l), returned linear.</summary>
        public static Color OffsetHsl(string hex, float dh, float ds, float dl)
        {
            Color c = Hex(hex);
            RgbToHsl(c, out float h, out float s, out float l);
            h = Mathf.Repeat(h + dh, 1f);
            s = Mathf.Clamp01(s + ds);
            l = Mathf.Clamp01(l + dl);
            return HslToRgb(h, s, l).linear;
        }

        private static void RgbToHsl(Color c, out float h, out float s, out float l)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            l = (min + max) / 2f;
            if (Mathf.Approximately(min, max)) { h = 0; s = 0; return; }
            float d = max - min;
            s = l <= .5f ? d / (max + min) : d / (2f - max - min);
            if (max == c.r) h = (c.g - c.b) / d + (c.g < c.b ? 6f : 0f);
            else if (max == c.g) h = (c.b - c.r) / d + 2f;
            else h = (c.r - c.g) / d + 4f;
            h /= 6f;
        }

        private static Color HslToRgb(float h, float s, float l)
        {
            if (s == 0) return new Color(l, l, l);
            float p = l <= .5f ? l * (1f + s) : l + s - l * s, q = 2f * l - p;
            return new Color(Hue(q, p, h + 1f / 3f), Hue(q, p, h), Hue(q, p, h - 1f / 3f));
        }

        private static float Hue(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6f) return p + (q - p) * 6 * t;
            if (t < .5f) return q;
            if (t < 2f / 3f) return p + (q - p) * 6 * (2f / 3f - t);
            return p;
        }
    }
}
