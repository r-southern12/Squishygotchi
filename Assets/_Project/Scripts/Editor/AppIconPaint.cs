using System;
using System.IO;
using Squishy.Runtime.Three;
using UnityEditor;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>
    /// The app icon, painted per pixel like a soft 3D render (user reference photos, 26 Sep 2026): a glossy
    /// pink bun whose pleats fan out from a swirl knot, bead eyes with highlights, a tiny "w" mouth and
    /// peach blush, sitting in a solid bamboo steamer on a bold sunburst tile.
    /// Menu: Squishy > Icon Variants (painted). Variant 0 teal, 1 sunny with the lid floating, 2 close-up on mint.
    /// </summary>
    public static class AppIconPaint
    {
        private const string Dir = "Assets/_Project/Art/Icon";

        [MenuItem("Squishy/Icon Variants (painted)")]
        public static void Variants()
        {
            Directory.CreateDirectory(Dir);
            for (int v = 0; v < 3; v++) File.WriteAllBytes(Path.Combine(Dir, "icon_paint_" + v + ".png"), Png(Draw(1024, v, true, true, 1)));
        }

        public static byte[] Png(Canvas2D g)
        {
            var t = g.ToTexture(true, false, false, "icon", true);
            var png = t.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(t);
            return png;
        }

        private static Color Hex(string h, float a = 1) { var c = Canvas2D.Css(h); c.a = a; return c; }

        /// <summary>Per-pixel painter over a unit-space box: fn(u, v) returns colour and coverage (2x2 supersampled).</summary>
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
                if (cov <= 0) continue;
                g.Set(x, y, new Color(acc.r / cov, acc.g / cov, acc.b / cov, 1), cov / 4);
            }
        }

        /// <summary>Paints one variant. k scales the figure about the centre (for the adaptive-icon safe zone).</summary>
        public static Canvas2D Draw(int size, int v, bool ground, bool figure, float k)
        {
            var g = new Canvas2D(size, size);
            float S = size;
            string[] grounds = { "#1F8A8A", "#F5B82E", "#7ED3B2" };
            string[] glows = { "#7FD6C8", "#FFE9A0", "#D8FFF0" };
            if (ground)
            {
                Color gc = Hex(grounds[v]), gl = Hex(glows[v]);
                Shade(g, S, 0, 0, 1, 1, (x, y) =>
                {
                    float dx = x - .5f, dy = y - .52f, dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ray = Mathf.Sin(Mathf.Atan2(dy, dx) * 12) > .3f ? .1f : 0;
                    return Color.Lerp(gc, gl, Mathf.Clamp01(1 - dist / .6f) * .6f + ray * Mathf.Clamp01(1 - dist));
                });
            }
            if (!figure) return g;

            // Layout in unit space (y down), scaled about the centre.
            float zoom = v == 2 ? 1.28f : 1f, lift = v == 1 ? .07f : v == 2 ? .1f : 0;
            Func<float, float> U = a => .5f + (a - .5f) * k * zoom;
            float Ux(float a) => U(a);
            float Uy(float a) => U(a + lift);
            float Ur(float a) => a * k * zoom;

            float cx = Ux(.5f), bunY = Uy(.52f), rx = Ur(.34f), ry = Ur(.29f);
            float rimY = Uy(.64f), rimRx = Ur(.43f), rimRy = Ur(.1f), bandBot = Uy(.88f);
            var bun = Hex(v == 2 ? "#FFB8CF" : "#F7A8C6");
            var bamboo = Hex("#E7B77E");
            var L = new Vector3(-.45f, -.6f, .66f).normalized;
            var H = (L + Vector3.forward).normalized;

            // Soft shadow on the tile under the steamer.
            Shade(g, S, cx - rimRx * 1.1f, bandBot - rimRy, cx + rimRx * 1.1f, bandBot + rimRy * 1.6f, (x, y) =>
            {
                float e = Sq((x - cx) / (rimRx * 1.05f)) + Sq((y - bandBot - rimRy * .5f) / (rimRy * 1.1f));
                return new Color(0, 0, 0, Mathf.Clamp01(1 - e) * .22f);
            });

            // Back of the steamer: rim and the dark inside.
            Ellipse(g, S, cx, rimY, rimRx, rimRy, (x, y) => Hex("#C89556"));
            Ellipse(g, S, cx, rimY + Ur(.005f), rimRx * .92f, rimRy * .8f, (x, y) => Color.Lerp(Hex("#6B4527"), Hex("#8E6038"), (y - rimY) / rimRy * .5f + .5f));

            // The bun: shaded dome with creases fanning from the knot.
            float knotX = cx, knotY = bunY - ry + Ur(.045f);
            Shade(g, S, cx - rx, bunY - ry, cx + rx, bunY + ry, (x, y) =>
            {
                float nx = (x - cx) / rx, ny = (y - bunY) / ry, rr = nx * nx + ny * ny;
                if (rr > 1) return Color.clear;
                float nz = Mathf.Sqrt(1 - rr);
                // Pleats: creases radiate from the knot, twisting a little, fading down the body.
                float kx = (x - knotX) / rx, ky = (y - knotY) / ry, dist = Mathf.Sqrt(kx * kx + ky * ky);
                float phi = Mathf.Atan2(kx, ky * 1.4f) + dist * .5f;
                float s = Mathf.Abs(Mathf.Sin(5.5f * phi));
                float fade = Mathf.Clamp01(1 - dist / .95f);
                fade *= fade;
                float crease = Mathf.Pow(1 - s, 5) * fade;
                // Tilt the normal across each lobe so they read as puffy.
                float lobeTilt = Mathf.Cos(5.5f * phi) * Mathf.Sign(Mathf.Sin(5.5f * phi)) * .35f * fade;
                var n = new Vector3(nx + lobeTilt * Mathf.Cos(phi), ny - lobeTilt * Mathf.Sin(phi) * .3f, nz).normalized;
                float diff = Mathf.Max(0, Vector3.Dot(n, L));
                float spec = Mathf.Pow(Mathf.Max(0, Vector3.Dot(n, H)), 70) * .45f;
                var c = bun * (.74f + .34f * diff);
                c = Color.Lerp(c, Hex("#FFD6C4"), Mathf.Clamp01(ny) * .18f); // warm bounce low down
                c *= 1 - .38f * crease;
                c *= .93f + .07f * nz;
                c += Color.white * spec;
                c.a = Mathf.Clamp01((1 - Mathf.Sqrt(rr)) * rx * S * 1.5f);
                return c;
            });
            // Swirl knot on top.
            Ellipse(g, S, knotX, knotY - Ur(.012f), Ur(.055f), Ur(.036f), (x, y) =>
            {
                float nx = (x - knotX) / Ur(.055f), ny = (y - knotY + Ur(.012f)) / Ur(.036f);
                float d = Mathf.Max(0, Vector3.Dot(new Vector3(nx, ny, Mathf.Sqrt(Mathf.Max(0, 1 - nx * nx - ny * ny))).normalized, L));
                return bun * (.62f + .5f * d);
            });
            g.StrokeArc(knotX * S + Ur(.004f) * S, (knotY - Ur(.016f)) * S, Ur(.022f) * S, Mathf.PI * .9f, Mathf.PI * 2.4f, bun * .72f, Ur(.009f) * S);

            // Face: bead eyes, "w" mouth, soft blush.
            float eyeY = bunY + Ur(.06f), eyeDx = Ur(.125f), er = Ur(.047f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float ex = cx + sx * eyeDx;
                Ellipse(g, S, ex, eyeY, er, er * 1.06f, (x, y) => Color.Lerp(Hex("#111014"), Hex("#3B2A30"), Mathf.Clamp01((y - eyeY) / er)));
                Ellipse(g, S, ex + er * .32f, eyeY - er * .36f, er * .34f, er * .34f, (x, y) => Color.white);
                Ellipse(g, S, ex - er * .34f, eyeY + er * .4f, er * .14f, er * .14f, (x, y) => Hex("#FFFFFF", .85f));
                float bx = cx + sx * Ur(.215f), by = eyeY + Ur(.06f);
                Shade(g, S, bx - Ur(.06f), by - Ur(.04f), bx + Ur(.06f), by + Ur(.04f), (x, y) =>
                {
                    float e = Sq((x - bx) / Ur(.052f)) + Sq((y - by) / Ur(.03f));
                    return Hex("#FF8A7A", Mathf.Clamp01(1 - e) * .75f);
                });
            }
            float mr = Ur(.02f), my = eyeY + Ur(.028f);
            foreach (float sx in new[] { -1f, 1f })
                g.StrokeArc((cx + sx * mr) * S, my * S, mr * S, 0, Mathf.PI, Hex("#1A1216"), Ur(.009f) * S);

            // Front of the steamer: a solid bamboo band with a groove and a lit lip.
            Shade(g, S, cx - rimRx, rimY - rimRy, cx + rimRx, bandBot + rimRy, (x, y) =>
            {
                float nx = (x - cx) / rimRx;
                if (Mathf.Abs(nx) > 1) return Color.clear;
                float arc = Mathf.Sqrt(1 - nx * nx);
                float top = rimY + rimRy * arc, bottom = bandBot + rimRy * arc;
                if (y < top || y > bottom) return Color.clear;
                float t = (y - top) / (bottom - top);
                float lum = .72f + .32f * arc - .12f * nx;
                var c = bamboo * lum;
                if (t < .12f) c = Color.Lerp(Hex("#F6D29E") * (.85f + .2f * arc), c, t / .12f); // lip
                float groove = Mathf.Abs(t - .42f);
                if (groove < .025f) c *= .8f;
                float edge = Mathf.Min(Mathf.Min(1, (x - (cx - rimRx)) * S), Mathf.Min((cx + rimRx - x) * S, (bottom - y) * S));
                c.a = Mathf.Clamp01(edge);
                return c;
            });

            if (v == 1)
            {
                // The lid floating above at a jaunty tilt, with its woven top.
                g.Save();
                g.Translate(Ux(.58f) * S, Uy(.12f) * S);
                g.Rotate(.2f);
                float lrx = Ur(.36f) * S, lry = Ur(.085f) * S, lh = Ur(.06f) * S;
                g.FillRect(-lrx, 0, lrx * 2, lh, Hex("#D9A568"));
                g.FillEllipse(0, lh, lrx, lry, 0, Hex("#C8955A"));
                g.FillEllipse(0, 0, lrx, lry, 0, Hex("#F1C98F"));
                for (int i = -4; i <= 4; i++)
                {
                    g.Line(i * lrx * .2f - lrx * .12f, -lry * .7f, i * lrx * .2f + lrx * .12f, lry * .7f, Hex("#D9A568", .8f), Ur(.006f) * S);
                    g.Line(i * lrx * .2f + lrx * .12f, -lry * .7f, i * lrx * .2f - lrx * .12f, lry * .7f, Hex("#D9A568", .5f), Ur(.006f) * S);
                }
                g.FillEllipse(0, -lry * .1f, lrx * .16f, lry * .38f, 0, Hex("#C8955A"));
                g.Restore();
            }
            // Twinkles.
            Twinkle(g, S, Ux(.16f), Uy(.2f - lift), Ur(.045f));
            Twinkle(g, S, Ux(.86f), Uy(.36f - lift), Ur(.03f));
            return g;
        }

        private static float Sq(float a) { return a * a; }

        private static void Ellipse(Canvas2D g, float S, float cx, float cy, float rx, float ry, Func<float, float, Color> col)
        {
            Shade(g, S, cx - rx, cy - ry, cx + rx, cy + ry, (x, y) =>
            {
                float e = Sq((x - cx) / rx) + Sq((y - cy) / ry);
                if (e > 1) return Color.clear;
                var c = col(x, y);
                c.a *= Mathf.Clamp01((1 - Mathf.Sqrt(e)) * Mathf.Min(rx, ry) * S * 2);
                return c;
            });
        }

        private static void Twinkle(Canvas2D g, float S, float x, float y, float r)
        {
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, d = i % 2 == 0 ? r : r * .26f;
                pts.Add(new Vector2((x + d * Mathf.Cos(a)) * S, (y + d * Mathf.Sin(a)) * S));
            }
            g.FillPolygon(pts, Color.white);
        }
    }
}
