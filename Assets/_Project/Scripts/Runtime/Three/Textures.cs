using System.Collections.Generic;
using Squishy.Simulation.Game;
using UnityEngine;

namespace Squishy.Runtime.Three
{
    /// <summary>The prototype's canvas-painted textures: style patterns, squishy finish maps, sparkle sprite, light rays, unbox wall tiles.</summary>
    public static class Textures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static float Rnd(float a, float b) { return Random.Range(a, b); }

        /// <summary>patTex(style): 128px tile, repeated 3x3 on the material.</summary>
        public static Texture2D Pattern(StyleData s)
        {
            if (Cache.TryGetValue("pat" + s.id, out var cached)) return cached;
            var g = new Canvas2D(128, 128);
            Color w = Canvas2D.Css(s.pal[0]), a = Canvas2D.Css(s.pal[1]), so = Canvas2D.Css(s.pal[2]), t = Canvas2D.Css(s.pal[3]), l = Canvas2D.Css(s.pal[4]);
            const float PI = Mathf.PI;
            g.FillRect(0, 0, 128, 128, l);
            switch (s.pat)
            {
                case "lattice":
                    g.FillRect(0, 0, 128, 128, so);
                    for (int i = 0; i <= 128; i += 32) { g.Line(i, 0, i, 128, a, 6); g.Line(0, i, 128, i, a, 6); }
                    for (int y = 16; y < 128; y += 32) for (int x = 16; x < 128; x += 32) g.StrokeRect(x - 7, y - 7, 14, 14, t, 3);
                    break;
                case "shoji":
                    g.FillRect(0, 0, 128, 128, Canvas2D.Css("#F4EEDF"));
                    for (int i = 0; i <= 128; i += 32) g.Line(i, 0, i, 128, w, 5);
                    for (float i = 0; i <= 128; i += 42.7f) g.Line(0, i, 128, i, w, 5);
                    break;
                case "diamond":
                    g.FillRect(0, 0, 128, 128, so);
                    for (int i = -128; i <= 256; i += 32) { g.Line(i, 0, i + 128, 128, a, 4); g.Line(i, 128, i + 128, 0, a, 4); }
                    break;
                case "blockfloral":
                    for (int y = 16; y < 128; y += 32)
                    for (int x = 16 + ((y / 32) % 2) * 16; x < 144; x += 32)
                    {
                        for (int k = 0; k < 5; k++) { float an = k / 5f * PI * 2; g.FillCircle(x + Mathf.Cos(an) * 6, y + Mathf.Sin(an) * 6, 5, a); }
                        g.FillCircle(x, y, 4, so); g.FillCircle(x, y, 1.8f, t);
                    }
                    break;
                case "star":
                    g.FillRect(0, 0, 128, 128, l);
                    for (int y = 32; y < 128; y += 64)
                    for (int x = 32; x < 128; x += 64)
                    {
                        g.Save(); g.Translate(x, y);
                        foreach (var r in new[] { 0f, PI / 4 }) { g.Rotate(r); g.FillRect(-18, -18, 36, 36, a); }
                        g.Restore(); g.FillCircle(x, y, 10, so); g.FillCircle(x, y, 4, t);
                    }
                    break;
                case "medallion":
                    g.FillRect(0, 0, 128, 128, a);
                    g.Save(); g.Translate(64, 64); g.Rotate(PI / 4); g.FillRect(-34, -34, 68, 68, t); g.FillRect(-28, -28, 56, 56, so); g.Restore();
                    g.FillCircle(64, 64, 12, a); g.FillCircle(64, 64, 5, t);
                    foreach (var p in new[] { new Vector2(0, 0), new Vector2(128, 0), new Vector2(0, 128), new Vector2(128, 128) }) g.FillCircle(p.x, p.y, 20, so);
                    g.StrokeRect(3, 3, 122, 122, t, 4);
                    break;
                case "talavera":
                    for (int y = 0; y < 128; y += 64)
                    for (int x = 0; x < 128; x += 64)
                    {
                        g.StrokeRect(x + 2, y + 2, 60, 60, a, 3);
                        foreach (var d in new[] { new Vector2(0, -13), new Vector2(13, 0), new Vector2(0, 13), new Vector2(-13, 0) }) g.FillCircle(x + 32 + d.x, y + 32 + d.y, 10, a);
                        g.FillCircle(x + 32, y + 32, 8, so); g.FillCircle(x + 32, y + 32, 3, t);
                        foreach (var d in new[] { new Vector2(6, 6), new Vector2(58, 6), new Vector2(6, 58), new Vector2(58, 58) }) g.FillCircle(x + d.x, y + d.y, 4, so);
                    }
                    break;
                case "mudcloth":
                    g.FillRect(0, 0, 128, 128, so);
                    for (int y = 10; y < 128; y += 32)
                    {
                        for (int x = 6; x < 128; x += 12) g.FillCircle(x, y, 2.4f, l);
                        g.Line(0, y + 11, 128, y + 11, l, 2);
                        for (int x = 10; x < 128; x += 24) { g.Line(x - 4, y + 17, x + 4, y + 25, a, 3); g.Line(x + 4, y + 17, x - 4, y + 25, a, 3); }
                    }
                    break;
                case "waves":
                    for (int y = 12; y < 140; y += 22) for (int x = -16; x < 144; x += 32) g.StrokeArc(x, y, 14, PI, 0, a, 4);
                    break;
                case "lines":
                    for (int y = 0; y < 128; y += 16) g.Line(0, y, 128, y, Canvas2D.Css("rgba(74,74,72,.18)"), 2);
                    g.Line(0, 64, 128, 64, a, 3);
                    break;
                case "stepped":
                {
                    var cols = new[] { a, so, t, l, a, so };
                    for (int i = 0; i < 6; i++)
                    {
                        int y = i * 21; g.FillRect(0, y, 128, 21, cols[i]);
                        for (int x = 0; x < 128; x += 16) { g.FillRect(x, y + 15, 8, 6, cols[(i + 1) % 6]); g.FillRect(x + 4, y + 11, 4, 4, cols[(i + 1) % 6]); }
                    }
                    break;
                }
                case "folkfloral":
                    for (int y = 10; y < 128; y += 42)
                    for (int x = 20 + ((y / 42) % 2) * 32; x < 150; x += 64)
                    {
                        g.Line(x, y + 34, x, y + 14, t, 3); g.FillCircle(x - 9, y + 24, 6, t); g.FillCircle(x + 9, y + 24, 6, t);
                        g.FillPolygon(new[] { new Vector2(x - 10, y + 12), new Vector2(x - 10, y), new Vector2(x - 4, y + 6), new Vector2(x, y - 2), new Vector2(x + 4, y + 6), new Vector2(x + 10, y), new Vector2(x + 10, y + 12) }, a);
                    }
                    break;
                case "gingham":
                {
                    var c = Canvas2D.Css("rgba(227,160,168,.45)");
                    for (int i = 0; i < 128; i += 32) { g.FillRect(i, 0, 16, 128, c); g.FillRect(0, i, 128, 16, c); }
                    break;
                }
                case "atomic":
                {
                    int[] xs = { 24, 90, 56, 110, 20 }, ys = { 24, 40, 80, 104, 100 };
                    for (int k = 0; k < 5; k++)
                    {
                        for (int r = 0; r < 8; r++) { float an = r / 8f * PI * 2; g.Line(xs[k], ys[k], xs[k] + Mathf.Cos(an) * 12, ys[k] + Mathf.Sin(an) * 12, a, 2.5f); }
                        g.FillCircle(xs[k], ys[k], 3, t);
                    }
                    foreach (var p in new[] { new Vector2(60, 20), new Vector2(34, 62), new Vector2(96, 74) }) g.FillCircle(p.x, p.y, 6, so);
                    break;
                }
                case "candy":
                    for (int i = -128; i < 256; i += 32) g.FillPolygon(new[] { new Vector2(i, 0), new Vector2(i + 16, 0), new Vector2(i + 144, 128), new Vector2(i + 128, 128) }, a);
                    for (int k = 0; k < 8; k++) g.FillCircle(Rnd(0, 128), Rnd(0, 128), 4, t);
                    break;
                case "scales":
                    g.FillRect(0, 0, 128, 128, so);
                    for (int y = 0; y < 144; y += 16) for (int x = ((y / 16) % 2) * 16; x < 144; x += 32) g.StrokeArc(x, y, 16, 0, PI, a, 2.5f);
                    break;
                case "stars":
                    g.FillRect(0, 0, 128, 128, so);
                    for (int k = 0; k < 40; k++) g.FillCircle(Rnd(0, 128), Rnd(0, 128), Rnd(.6f, 1.6f), l);
                    for (int k = 0; k < 5; k++) { float x = Rnd(10, 118), y = Rnd(10, 118); g.Line(x - 6, y, x + 6, y, a, 2.5f); g.Line(x, y - 6, x, y + 6, a, 2.5f); }
                    break;
                case "moons":
                    g.FillRect(0, 0, 128, 128, so);
                    for (int y = 20; y < 128; y += 44) for (int x = 20 + ((y / 44) % 2) * 30; x < 140; x += 60) { g.FillCircle(x, y, 11, a); g.FillCircle(x + 5, y - 3, 10, so); }
                    for (int k = 0; k < 14; k++) g.FillCircle(Rnd(0, 128), Rnd(0, 128), 1.2f, l);
                    break;
                case "pixels":
                {
                    var cols = new[] { w, a, so, t };
                    for (int y = 0; y < 128; y += 16) for (int x = 0; x < 128; x += 16) g.FillRect(x, y, 16, 16, cols[((x * 7 + y * 3) / 16) % 4]);
                    break;
                }
                case "leaves":
                    for (int k = 0; k < 18; k++) { g.Save(); g.Translate(Rnd(0, 128), Rnd(0, 128)); g.Rotate(Rnd(0, 6)); g.FillEllipse(0, 0, 10, 4.5f, 0, k % 3 != 0 ? a : t); g.Restore(); }
                    break;
                case "checker":
                    for (int y = 0; y < 128; y += 32) for (int x = 0; x < 128; x += 32) if (((x + y) / 32) % 2 == 1) g.FillRect(x, y, 32, 32, so);
                    break;
                case "dinercheck":
                    for (int y = 0; y < 128; y += 16) for (int x = 0; x < 128; x += 16) if (((x + y) / 16) % 2 == 1) g.FillRect(x, y, 16, 16, t);
                    break;
                case "plaid":
                {
                    g.FillRect(0, 0, 128, 128, so);
                    var c = Canvas2D.Css("rgba(140,47,57,.55)");
                    for (int i = 0; i < 128; i += 32) { g.FillRect(i, 0, 10, 128, c); g.FillRect(0, i, 128, 10, c); g.Line(i + 20, 0, i + 20, 128, t, 1.5f); g.Line(0, i + 20, 128, i + 20, t, 1.5f); }
                    break;
                }
            }
            return Cache["pat" + s.id] = g.ToTexture(true, true, true, "Pattern " + s.id);
        }

        /// <summary>finishMap(kind): koi, straw, sesame squishy skins (256x128).</summary>
        public static Texture2D FinishMap(string kind)
        {
            if (Cache.TryGetValue("fin" + kind, out var cached)) return cached;
            var g = kind == "glitter" ? new Canvas2D(1024, 512) : new Canvas2D(256, 128); // glitter needs fine grain
            if (kind == "koi")
            {
                g.FillRect(0, 0, 256, 128, Canvas2D.Css("#FBF6EE"));
                float[,] spots = { { 40, 40, 34 }, { 150, 70, 40 }, { 210, 30, 22 }, { 95, 95, 18 } };
                string[] cols = { "#EE7B3A", "#EE7B3A", "#2B2320", "#2B2320" };
                for (int i = 0; i < 4; i++) g.FillEllipse(spots[i, 0], spots[i, 1], spots[i, 2], spots[i, 2] * .7f, .4f, Canvas2D.Css(cols[i]));
            }
            else if (kind == "straw")
            {
                g.FillRect(0, 0, 256, 128, Canvas2D.Css("#E8505B"));
                g.FillRect(0, 0, 256, 18, Canvas2D.Css("#8CC56A"));
                var seed = Canvas2D.Css("#FFE9A6");
                for (int y = 28; y < 128; y += 14) for (int x = ((y / 14) % 2) * 10; x < 256; x += 20) g.FillEllipse(x, y, 2, 3.2f, 0, seed);
            }
            else if (kind == "sesame")
            {
                g.FillRect(0, 0, 256, 128, Canvas2D.Css("#FAF3E6"));
                var dark = Canvas2D.Css("#2B2320");
                for (int k = 0; k < 220; k++) { g.Save(); g.Translate(Rnd(0, 256), 20 + Rnd(0, 80)); g.Rotate(Rnd(0, 6)); g.FillEllipse(0, 0, 3, 1.5f, 0, dark); g.Restore(); }
            }
            else if (kind == "glitter")
            {
                // Fine silver glitter (the icon squishy): bright specks over a slightly dimmed base, multiplied by the colour.
                g.FillRect(0, 0, 1024, 512, Canvas2D.Css("#D2CEDB"));
                for (int k = 0; k < 16000; k++) g.FillCircle(Rnd(0, 1024), Rnd(0, 512), Rnd(.6f, 1.4f), Canvas2D.Css(k % 3 == 0 ? "#F2EEFF" : "#FFFFFF"));
                for (int k = 0; k < 4000; k++) g.FillCircle(Rnd(0, 1024), Rnd(0, 512), Rnd(.5f, 1f), Canvas2D.Css("#AFA8C0"));
            }
            return Cache["fin" + kind] = g.ToTexture(true, false, true, "Finish " + kind);
        }

        /// <summary>spTex: soft round sparkle with a cross (linear, only alpha is used).</summary>
        public static Texture2D Sparkle()
        {
            if (Cache.TryGetValue("spark", out var cached)) return cached;
            var g = new Canvas2D(32, 32);
            g.FillShader((x, y) =>
            {
                float d = Mathf.Sqrt((x - 16) * (x - 16) + (y - 16) * (y - 16)) / 16f;
                float al = d < .35f ? Mathf.Lerp(1f, .55f, d / .35f) : d < 1f ? Mathf.Lerp(.55f, 0f, (d - .35f) / .65f) : 0f;
                return new Color(1, 1, 1, al);
            });
            g.FillRect(15, 3, 2, 26, Color.white);
            g.FillRect(3, 15, 26, 2, Color.white);
            return Cache["spark"] = g.ToTexture(false, false, true, "Sparkle");
        }

        /// <summary>rayTex: 14 wedges of a radial warm gradient (linear).</summary>
        public static Texture2D Rays()
        {
            if (Cache.TryGetValue("rays", out var cached)) return cached;
            var g = new Canvas2D(128, 128);
            float wedge = Mathf.PI * 2f / 14f;
            g.FillShader((x, y) =>
            {
                float dx = x - 64, dy = y - 64, d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Repeat(Mathf.Atan2(dy, dx), Mathf.PI * 2f);
                if (d > 64f || Mathf.Repeat(ang, wedge) > Mathf.PI / 14f) return new Color(0, 0, 0, 0);
                float t = Mathf.Clamp01((d - 5f) / 59f);
                return new Color(1f, 240f / 255f, 205f / 255f, 1f - t);
            });
            return Cache["rays"] = g.ToTexture(false, false, true, "Rays");
        }

        /// <summary>
        /// The woven bamboo base of the steamer (replaces the prototype's dotted paper liner, 25 Sep 2026):
        /// a plain over-under weave of 32px strips in two tones, with dark gaps and soft shading where strips dip under.
        /// </summary>
        public static Texture2D Weave(string a, string b, string gap)
        {
            // A real steamer base (25 Sep 2026, from the user's photos): parallel flat bamboo slats with rows of
            // long rounded slots between them, staggered row to row, a highlight on each slat edge and faint grain.
            string key = "slats" + a + b + gap;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            const int N = 512, P = 64, SlatW = 46, SlotLen = 92, Bridge = 36, Period = SlotLen + Bridge;
            var g = new Canvas2D(N, N);
            Color ca = Canvas2D.Css(a), cb = Canvas2D.Css(b), cg = Canvas2D.Css(gap);
            Color hole = Color.Lerp(cg, Color.black, .45f), shade = new Color(cg.r * .7f, cg.g * .7f, cg.b * .7f, .35f), grain = new Color(cg.r, cg.g, cg.b, .12f), hi = new Color(1, 1, 1, .28f);
            for (int j = 0; j < N / P; j++)
            {
                float y = j * P;
                var slat = Color.Lerp(ca, cb, ((j * 37) % 5) / 5f);
                g.FillRect(0, y, N, P, slat);                      // slat plus the bridges between slots
                g.FillRect(0, y + 2, N, 3, hi);                    // rounded top edge catching the light
                g.FillRect(0, y + SlatW - 4, N, 3, shade);          // lower edge in shade
                for (int k = 0; k < 4; k++) g.FillRect(0, y + 9 + k * 9 + (j % 3), N, 1, grain);
                // The slot row under this slat, staggered by half a period on alternate rows.
                float sy = y + SlatW, sh = P - SlatW, r = sh / 2f, off = (j % 2) * Period / 2f;
                for (float x = -Period + off; x < N + Period; x += Period)
                {
                    g.FillRect(x + r, sy, SlotLen - 2 * r, sh, hole);
                    g.FillCircle(x + r, sy + r, r, hole);
                    g.FillCircle(x + SlotLen - r, sy + r, r, hole);
                }
            }
            return Cache[key] = g.ToTexture(true, true, true, "Slats");
        }

        private static void Strip(Canvas2D g, float x, float y, float w, float h, bool across, Color c, Color grain, Color hi)
        {
            g.FillRect(x, y, w, h, c);
            if (across)
            {
                g.FillRect(x, y + h * .3f, w, h * .18f, hi);
                for (int k = 1; k < 4; k++) g.FillRect(x, y + h * k / 4f, w, 1, grain);
            }
            else
            {
                g.FillRect(x + w * .3f, y, w * .18f, h, hi);
                for (int k = 1; k < 4; k++) g.FillRect(x + w * k / 4f, y, 1, h, grain);
            }
        }

        /// <summary>tileTex: the unbox counter's back wall tiles (repeat 8x3).</summary>
        public static Texture2D Tiles()
        {
            if (Cache.TryGetValue("tiles", out var cached)) return cached;
            var g = new Canvas2D(128, 128);
            g.FillRect(0, 0, 128, 128, Canvas2D.Css("#CDBFA6"));
            string[] cols = { "#E6DAC3", "#E0D2B8", "#EADFCA" };
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) g.FillRect(x * 32 + 2, y * 32 + 2, 28, 28, Canvas2D.Css(cols[(x * 3 + y) % 3]));
            return Cache["tiles"] = g.ToTexture(true, true, true, "Tiles");
        }
    }
}
