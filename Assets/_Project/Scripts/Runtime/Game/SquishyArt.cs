using System;
using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Stylised, icon-style portraits of a squishy for notifications and the home-screen widget (user request,
    /// 27 Sep 2026): a glossy, squat, pleated dumpling with bead eyes and blush, painted per pixel from the finish
    /// data (colour, gloss, metal, glow, glitter, galaxy, holographic, patterns), so every squishy type has its own
    /// picture without any art files. Painted on the CPU, so it never depends on a camera render.
    /// </summary>
    public static class SquishyArt
    {
        public enum Mood { Happy, Droopy, Sad }

        private static float Sq(float a) { return a * a; }
        private static float Sstep(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }

        public static byte[] Png(FinishData f, Mood mood, bool baby, int size = 256)
        {
            var t = Paint(f, mood, baby, size);
            var png = t.EncodeToPNG();
            UnityEngine.Object.Destroy(t);
            return png;
        }

        /// <summary>A readable texture, transparent around the squishy.</summary>
        public static Texture2D Paint(FinishData f, Mood mood, bool baby, int size = 256)
        {
            var g = new Canvas2D(size, size);
            float S = size;
            var rng = new System.Random((f.name ?? "").GetHashCode());
            string tier = f.tier ?? "";
            bool galaxy = tier == "Galaxy", holo = tier == "Holographic", glitter = f.spark != null && f.spark.Length > 0;
            float k = baby ? .86f : 1f;
            float cx = .5f, cy = .58f, rx = .41f * k, ryTop = .36f * k, ryBot = .22f * k, gloss = 1 - f.rough;
            Color baseC = Canvas2D.Css(string.IsNullOrEmpty(f.color) ? "#F3A6BD" : f.color);
            if (mood == Mood.Sad) baseC = Color.Lerp(baseC, new Color(.62f, .6f, .58f), .38f);
            Color glowC = string.IsNullOrEmpty(f.glow) ? Color.clear : Canvas2D.Css(f.glow);
            var L = new Vector3(-.45f, -.62f, .64f).normalized;
            var H = (L + Vector3.forward).normalized;
            float topY = cy - ryTop, knotY = topY + .02f * k;

            // Fixed speckles and sparkle positions for glitter, galaxy and patterns (per finish, stable).
            int nSpeck = galaxy ? 260 : glitter ? 420 : f.map == "glitter" ? 700 : 0;
            var speck = new Vector3[Math.Max(nSpeck, 40)];
            for (int i = 0; i < speck.Length; i++) speck[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
            // Specks go into a pixel map first, so shading looks them up instead of testing every speck per pixel.
            int[] dot = null;
            if (nSpeck > 0)
            {
                dot = new int[size * size];
                for (int i = 0; i < dot.Length; i++) dot[i] = -1;
                for (int i = 0; i < nSpeck; i++)
                {
                    var p = speck[i];
                    int px = (int)((cx + (p.x - .5f) * 2 * rx) * S), py = (int)((topY + p.y * (ryTop + ryBot)) * S);
                    if (px >= 0 && py >= 0 && px < size && py < size) dot[py * size + px] = (int)(p.z * 97);
                }
            }

            // Soft shadow on the ground.
            Shade(g, S, .12f, .74f, .88f, .92f, (x, y) =>
            {
                float e = Sq((x - cx) / (rx * 1.05f)) + Sq((y - (cy + ryBot + .02f)) / .05f);
                return new Color(0, 0, 0, Mathf.Clamp01(1 - e) * .18f);
            });
            // Glow finishes: a soft halo round the body.
            if (glowC.a > 0)
                Shade(g, S, cx - rx - .1f, topY - .1f, cx + rx + .1f, cy + ryBot + .1f, (x, y) =>
                {
                    float r = Metric(x, y, cx, cy, rx, ryTop, ryBot);
                    return r <= 1 ? Color.clear : new Color(glowC.r, glowC.g, glowC.b, .5f * Sq(Mathf.Clamp01(1 - (r - 1) / .28f)));
                });

            // The body.
            Shade(g, S, cx - rx, topY, cx + rx, cy + ryBot, (x, y) =>
            {
                float r = Metric(x, y, cx, cy, rx, ryTop, ryBot);
                if (r > 1) return Color.clear;
                float nx = (x - cx) / rx, ny = (y - cy) / (y < cy ? ryTop : ryBot * 1.6f);
                float nz = Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Min(1, nx * nx + ny * ny)));
                // Pleats fanning straight down from the top, with only a slight curl.
                float kx = (x - cx) / rx, ky = (y - knotY) / ryTop, dist = Mathf.Sqrt(kx * kx + ky * ky);
                float phi = Mathf.Atan2(kx, ky * 1.25f) + dist * .2f;
                float lobe = Mathf.Abs(Mathf.Sin(5.2f * phi)), fade = Sq(Mathf.Clamp01(1 - dist / .8f));
                float crease = Mathf.Pow(1 - lobe, 4) * fade;
                float tilt = Mathf.Cos(5.2f * phi) * Mathf.Sign(Mathf.Sin(5.2f * phi)) * .3f * fade;
                var n = new Vector3(nx + tilt * Mathf.Cos(phi), ny, nz).normalized;
                float diff = Mathf.Max(0, Vector3.Dot(n, L));
                float shin = Mathf.Lerp(10, 70, gloss), spec = Mathf.Pow(Mathf.Max(0, Vector3.Dot(n, H)), shin) * Mathf.Lerp(.15f, .6f, gloss);
                var c = Surface(f, baseC, x, y, cx, topY, rx, ryTop, galaxy);
                if (holo)
                {
                    float hue = Mathf.Repeat(nx * .45f + ny * .35f + .5f, 1);
                    c = Color.Lerp(c, Color.HSVToRGB(hue, .35f, 1), .35f);
                }
                c *= .74f + .34f * diff;
                c *= 1 - .3f * crease;
                c *= .9f + .1f * nz;
                if (glowC.a > 0) c = Color.Lerp(c, glowC, .12f + .15f * (1 - nz));
                if (f.metal) { c = Color.Lerp(c * .85f, Color.white, spec * .9f); c += baseC * spec * .4f; }
                else c += Color.white * spec;
                // Glitter: fine bright specks over the surface.
                if (dot != null)
                {
                    int ix = (int)(x * S), iy = (int)(y * S);
                    if (ix >= 0 && iy >= 0 && ix < size && iy < size && dot[iy * size + ix] >= 0) c = Color.Lerp(c, SparkCol(f, dot[iy * size + ix]), .85f);
                }
                c.a = Mathf.Clamp01((1 - r) * rx * S * 2);
                return c;
            });

            // Baby cowlick: a little curl on top.
            if (baby) Curl(g, S, cx + .01f, topY - .01f, .035f, baseC * .85f);

            // A few bigger twinkles for glittery finishes.
            if (glitter)
                for (int i = 0; i < 5; i++)
                {
                    var p = speck[i * 7 % Math.Max(1, speck.Length)];
                    float sx = cx + (p.x - .5f) * 1.5f * rx, sy = topY + .05f + p.y * ryTop;
                    Twinkle(g, S, sx, sy, (.022f + p.z * .018f) * k, SparkCol(f, i));
                }

            // Face.
            float ex = rx * .33f, ey = cy + ryBot * .15f, er = rx * .12f;
            var ink = Canvas2D.Css("#1C1418");
            var blush = Canvas2D.Css("#FF8A9A");
            foreach (float sx in new[] { -1f, 1f })
            {
                float px = cx + sx * ex;
                Soft(g, S, px + sx * rx * .23f, ey + rx * .13f, rx * .13f, rx * .075f, new Color(blush.r, blush.g, blush.b, mood == Mood.Sad ? .35f : .7f));
                if (mood == Mood.Droopy)
                {
                    // Sleepy half-closed eyes.
                    Ellipse(g, S, px, ey + er * .35f, er, er * .55f, ink);
                    Ellipse(g, S, px + er * .3f, ey + er * .2f, er * .22f, er * .18f, Color.white);
                    Arc(g, S, px, ey - er * .1f, er * 1.05f, Mathf.PI * 1.05f, Mathf.PI * 1.95f, ink, er * .22f);
                }
                else
                {
                    Ellipse(g, S, px, ey, er * .88f, er, ink);
                    Ellipse(g, S, px + er * .3f, ey - er * .38f, er * .34f, er * .34f, Color.white);
                    Ellipse(g, S, px - er * .32f, ey + er * .4f, er * .14f, er * .14f, new Color(1, 1, 1, .85f));
                }
            }
            if (mood == Mood.Sad)
            {
                // A tear and a little frown.
                float tx = cx + ex + er * .6f, ty = ey + er * 1.3f;
                Ellipse(g, S, tx, ty, er * .32f, er * .45f, Canvas2D.Css("#8FD3FF"));
                Arc(g, S, cx, ey + rx * .2f, rx * .08f, Mathf.PI * 1.15f, Mathf.PI * 1.85f, ink, er * .2f);
            }
            else if (mood == Mood.Droopy) Line(g, S, cx - rx * .06f, ey + rx * .13f, cx + rx * .06f, ey + rx * .13f, ink, er * .2f);
            else
                foreach (float sx in new[] { -1f, 1f }) Arc(g, S, cx + sx * rx * .045f, ey + rx * .09f, rx * .045f, 0, Mathf.PI, ink, er * .2f);

            return g.ToTexture(true, false, false, "SquishyArt", true);
        }

        /// <summary>The squat, soft-cornered dumpling outline: 1 on the edge.</summary>
        private static float Metric(float x, float y, float cx, float cy, float rx, float ryTop, float ryBot)
        {
            float nx = Mathf.Abs(x - cx) / rx, ny = Mathf.Abs(y - cy) / (y < cy ? ryTop : ryBot);
            float p = y < cy ? 2.1f : 3.2f; // round dome, flatter bottom
            return Mathf.Pow(Mathf.Pow(nx, p) + Mathf.Pow(ny, p), 1 / p);
        }

        /// <summary>The finish's surface colour at a point: plain, or a pattern, or a galaxy.</summary>
        private static Color Surface(FinishData f, Color baseC, float x, float y, float cx, float topY, float rx, float ryTop, bool galaxy)
        {
            float u = (x - cx) / rx, v = (y - topY) / ryTop;
            switch (f.map)
            {
                case "koi":
                    if (Sq((u + .35f) / .3f) + Sq((v - .45f) / .22f) < 1 || Sq((u - .4f) / .25f) + Sq((v - .95f) / .2f) < 1) return Canvas2D.Css("#EE7B3A");
                    if (Sq((u - .1f) / .16f) + Sq((v - .25f) / .12f) < 1) return Canvas2D.Css("#2B2320");
                    return Canvas2D.Css("#FBF6EE");
                case "straw":
                    if (v < .28f + .06f * Mathf.Sin(u * 18)) return Canvas2D.Css("#8CC56A");
                    float gx = Mathf.Repeat(u * 7 + (Mathf.Floor(v * 6) % 2) * .5f, 1), gy = Mathf.Repeat(v * 6, 1);
                    return Sq((gx - .5f) / .12f) + Sq((gy - .5f) / .2f) < 1 ? Canvas2D.Css("#FFE9A6") : Canvas2D.Css("#E8505B");
                case "sesame":
                {
                    // One seed per grid cell, jittered by a hash, so it's cheap per pixel.
                    float gu = u * 8 + 20, gv = v * 6 + 20;
                    int iu = Mathf.FloorToInt(gu), iv = Mathf.FloorToInt(gv);
                    float h1 = Frac(Mathf.Sin(iu * 12.9898f + iv * 78.233f) * 43758.55f), h2 = Frac(h1 * 91.7f), a = h2 * 3;
                    float du = gu - iu - (.25f + .5f * h1), dv = gv - iv - (.25f + .5f * h2);
                    float ru = du * Mathf.Cos(a) + dv * Mathf.Sin(a), rv = -du * Mathf.Sin(a) + dv * Mathf.Cos(a);
                    if (Sq(ru / .16f) + Sq(rv / .07f) < 1) return Canvas2D.Css("#2B2320");
                }
                    return Canvas2D.Css("#FAF3E6");
            }
            if (galaxy)
            {
                // Deep colour with a cloudy swirl of lighter nebula.
                float neb = .5f + .5f * Mathf.Sin(u * 5 + Mathf.Sin(v * 4) * 2) * Mathf.Sin(v * 3 - u * 2);
                return Color.Lerp(baseC, baseC * 1.9f + new Color(.1f, .05f, .15f), neb * .45f);
            }
            return baseC;
        }

        private static float Frac(float v) { return v - Mathf.Floor(v); }

        private static Color SparkCol(FinishData f, int i)
        {
            if (f.spark == null || f.spark.Length == 0) return Color.white;
            return Canvas2D.Css(f.spark[Math.Abs(i) % f.spark.Length]);
        }

        // ---- drawing helpers (unit space, y down, 2x2 supersampled) ----

        private static void Shade(Canvas2D g, float S, float x0, float y0, float x1, float y1, Func<float, float, Color> fn)
        {
            int ax = Mathf.Max(0, (int)(x0 * S) - 1), bx = Mathf.Min((int)S - 1, (int)(x1 * S) + 1);
            int ay = Mathf.Max(0, (int)(y0 * S) - 1), by = Mathf.Min((int)S - 1, (int)(y1 * S) + 1);
            for (int y = ay; y <= by; y++)
            for (int x = ax; x <= bx; x++)
            {
                Color acc = Color.clear;
                float cov = 0;
                for (int s = 0; s < 4; s++)
                {
                    var c = fn((x + .25f + .5f * (s & 1)) / S, (y + .25f + .5f * (s >> 1)) / S);
                    if (c.a <= 0) continue;
                    acc += new Color(c.r, c.g, c.b, 1) * c.a;
                    cov += c.a;
                }
                if (cov > 0) g.Set(x, y, new Color(acc.r / cov, acc.g / cov, acc.b / cov, 1), cov / 4);
            }
        }

        private static void Ellipse(Canvas2D g, float S, float cx, float cy, float rx, float ry, Color col)
        {
            Shade(g, S, cx - rx, cy - ry, cx + rx, cy + ry, (x, y) =>
            {
                float e = Sq((x - cx) / rx) + Sq((y - cy) / ry);
                if (e > 1) return Color.clear;
                var c = col;
                c.a *= Mathf.Clamp01((1 - Mathf.Sqrt(e)) * Mathf.Min(rx, ry) * S * 2);
                return c;
            });
        }

        private static void Soft(Canvas2D g, float S, float cx, float cy, float rx, float ry, Color col)
        {
            Shade(g, S, cx - rx, cy - ry, cx + rx, cy + ry, (x, y) =>
            {
                float e = Sq((x - cx) / rx) + Sq((y - cy) / ry);
                return new Color(col.r, col.g, col.b, col.a * Mathf.Clamp01(1 - e));
            });
        }

        private static void Line(Canvas2D g, float S, float x1, float y1, float x2, float y2, Color col, float w)
        {
            g.Line(x1 * S, y1 * S, x2 * S, y2 * S, col, w * S);
            g.FillCircle(x1 * S, y1 * S, w * S / 2, col);
            g.FillCircle(x2 * S, y2 * S, w * S / 2, col);
        }

        private static void Arc(Canvas2D g, float S, float cx, float cy, float r, float a0, float a1, Color col, float w)
        {
            for (int i = 0; i < 14; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / 14f), t1 = Mathf.Lerp(a0, a1, (i + 1) / 14f);
                Line(g, S, cx + Mathf.Cos(t0) * r, cy + Mathf.Sin(t0) * r, cx + Mathf.Cos(t1) * r, cy + Mathf.Sin(t1) * r, col, w);
            }
        }

        private static void Curl(Canvas2D g, float S, float cx, float cy, float r, Color col)
        {
            col.a = 1;
            Arc(g, S, cx, cy - r, r, Mathf.PI * .5f, Mathf.PI * 2.1f, col, r * .5f);
        }

        private static void Twinkle(Canvas2D g, float S, float x, float y, float r, Color col)
        {
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, d = i % 2 == 0 ? r : r * .28f;
                pts.Add(new Vector2((x + d * Mathf.Cos(a)) * S, (y + d * Mathf.Sin(a)) * S));
            }
            g.FillPolygon(pts, col);
        }
    }
}
