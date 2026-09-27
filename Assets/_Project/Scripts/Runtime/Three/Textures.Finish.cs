using Squishy.Simulation.Game;
using UnityEngine;

namespace Squishy.Runtime.Three
{
    /// <summary>
    /// Finish textures painted from each squishy's own colours (user review, 28 Sep 2026: glitter skins looked like
    /// flat opaque colour). Glitter: dense multicoloured flecks through the surface. Galaxy: soft nebula clouds and
    /// stars. Holographic: pastel rainbow sheen bands. The base colour is painted in, so the material stays white.
    /// </summary>
    public static partial class Textures
    {
        /// <summary>A painted map for this finish's tier, or null when the tier has none (plain, UV, patterns use their own).</summary>
        public static Texture2D TierMap(FinishData f)
        {
            if (!string.IsNullOrEmpty(f.map) || f.spark == null || f.spark.Length == 0) return null;
            string kind = f.tier == "Glitter" ? "fleck" : f.tier == "Galaxy" || f.name == "Cosmic Pearl" ? "nebula" : f.tier == "Holographic" || f.name == "Candy Floss" ? "holo" : null;
            if (kind == null) return null;
            string key = "tier" + kind + f.name;
            if (Cache.TryGetValue(key, out var hit)) return hit;
            var baseC = Canvas2D.Css(f.color);
            var sp = new Color[f.spark.Length];
            for (int i = 0; i < sp.Length; i++) sp[i] = Canvas2D.Css(f.spark[i]);
            // Pale flecks vanish on a pale squishy: push their colour so they always read (white stays white).
            for (int i = 0; i < sp.Length; i++) { Color.RGBToHSV(sp[i], out var h, out var s, out var v); if (s > .04f) sp[i] = Color.HSVToRGB(h, Mathf.Max(s, .55f), Mathf.Max(v, .9f)); }
            var rnd = new System.Random(f.name.GetHashCode());
            float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
            Canvas2D g;
            if (kind == "fleck")
            {
                // Glitter suspended in the squish: fine flecks everywhere, some chunkier ones, a few darker for depth.
                g = new Canvas2D(1024, 512);
                g.FillRect(0, 0, 1024, 512, Color.Lerp(baseC, Color.white, .08f));
                for (int k = 0; k < 2500; k++) g.FillCircle(R(0, 1024), R(0, 512), R(.8f, 1.8f), Color.Lerp(baseC, Color.black, .12f));
                for (int k = 0; k < 9000; k++) g.FillCircle(R(0, 1024), R(0, 512), R(.8f, 1.6f), sp[k % sp.Length]);
                for (int k = 0; k < 2600; k++) g.FillEllipse(R(0, 1024), R(0, 512), R(2.2f, 3.6f), R(1.4f, 2.4f), R(0, 3.14f), sp[k % sp.Length]); // chunky glitter reads even small
                for (int k = 0; k < 1200; k++) g.FillCircle(R(0, 1024), R(0, 512), R(.7f, 1.2f), Color.white);
            }
            else if (kind == "nebula")
            {
                // Deep space: soft coloured clouds, then stars of two sizes.
                const int W = 512, H = 256;
                g = new Canvas2D(W, H);
                var blobs = new Vector4[10];
                for (int i = 0; i < blobs.Length; i++) blobs[i] = new Vector4(R(0, W), R(0, H), R(40, 110), i % sp.Length);
                g.FillShader((x, y) =>
                {
                    var c = baseC;
                    foreach (var b in blobs)
                    {
                        float dx = Mathf.Min(Mathf.Abs(x - b.x), W - Mathf.Abs(x - b.x)), dy = y - b.y;
                        float w = Mathf.Exp(-(dx * dx + dy * dy) / (b.z * b.z)) * .45f;
                        c = Color.Lerp(c, sp[(int)b.w], w);
                    }
                    return c;
                });
                for (int k = 0; k < 900; k++) g.FillCircle(R(0, W), R(0, H), R(.4f, .9f), Color.Lerp(Color.white, sp[k % sp.Length], .3f));
                for (int k = 0; k < 60; k++) g.FillCircle(R(0, W), R(0, H), R(1.2f, 2f), Color.white);
            }
            else
            {
                // Holographic: soft pastel rainbow bands sweeping diagonally over a pearly base.
                const int W = 512, H = 256;
                g = new Canvas2D(W, H);
                g.FillShader((x, y) =>
                {
                    float u = x / (float)W, v = y / (float)H;
                    float t = u * 2 + v * .8f + .12f * Mathf.Sin(u * 12.566f + v * 6f);
                    t -= Mathf.Floor(t);
                    float fi = t * sp.Length;
                    int a = (int)fi % sp.Length, b = (a + 1) % sp.Length;
                    var band = Color.Lerp(sp[a], sp[b], fi - Mathf.Floor(fi));
                    return Color.Lerp(baseC, band, .65f);
                });
            }
            return Cache[key] = g.ToTexture(true, true, true, "Finish " + f.name);
        }

        /// <summary>A four-point star with a soft glow: the sparkle sprite for squish and bounce effects (linear, alpha only).</summary>
        public static Texture2D Glint()
        {
            if (Cache.TryGetValue("glint", out var hit)) return hit;
            const int S = 64;
            var g = new Canvas2D(S, S);
            g.FillShader((x, y) =>
            {
                float dx = (x + .5f - S / 2f) / (S / 2f), dy = (y + .5f - S / 2f) / (S / 2f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Exp(-r * r * 60);
                float glow = Mathf.Exp(-r * r * 7) * .35f;
                float rays = Mathf.Exp(-Mathf.Abs(dx) * 38) * Mathf.Max(0, 1 - Mathf.Abs(dy)) + Mathf.Exp(-Mathf.Abs(dy) * 38) * Mathf.Max(0, 1 - Mathf.Abs(dx));
                float a = Mathf.Clamp01(core + glow + rays * .9f);
                return new Color(a, a, a, a);
            });
            return Cache["glint"] = g.ToTexture(false, false, true, "Glint");
        }
    }
}
