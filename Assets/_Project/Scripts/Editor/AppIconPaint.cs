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

        /// <summary>Knot style: 0 twisted pinch, 1 flush spiral nub, 2 soft dimple where the pleats meet.</summary>
        public static int Knot;

        [MenuItem("Squishy/Icon Variants (painted)")]
        public static void Variants()
        {
            Directory.CreateDirectory(Dir);
            for (Knot = 0; Knot < 3; Knot++) File.WriteAllBytes(Path.Combine(Dir, "icon_knot_" + "ABC"[Knot] + ".png"), Png(Draw(1024, 0, true, true, 1)));
            Knot = 0;
        }

        public static byte[] Png(Canvas2D g)
        {
            var t = g.ToTexture(true, false, false, "icon", true);
            var png = t.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(t);
            return png;
        }

        internal static Color Hex(string h, float a = 1) { var c = Canvas2D.Css(h); c.a = a; return c; }

        /// <summary>Per-pixel painter over a unit-space box: fn(u, v) returns colour and coverage (2x2 supersampled).</summary>
        internal static void Shade(Canvas2D g, float S, float x0, float y0, float x1, float y1, Func<float, float, Color> fn)
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

            float cx = Ux(.5f), bunY = Uy(.475f), rx = Ur(.325f), ry = Ur(.305f);
            float rimY = Uy(.6f), rimRx = Ur(.44f), rimRy = Ur(.125f), bandBot = Uy(.86f);
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
            Ellipse(g, S, cx, rimY + Ur(.006f), rimRx * .9f, rimRy * .78f, (x, y) => Color.Lerp(Hex("#B98249"), Hex("#4E301A"), Mathf.Clamp01((y - (rimY - rimRy * .78f)) / (rimRy * 1.2f))));

            Cloud(g, S, Ux(.15f), Uy(.6f), Ur(.075f));
            Cloud(g, S, Ux(.12f), Uy(.45f), Ur(.05f));
            Cloud(g, S, Ux(.17f), Uy(.35f), Ur(.03f));
            Cloud(g, S, Ux(.85f), Uy(.58f), Ur(.07f));
            Cloud(g, S, Ux(.89f), Uy(.44f), Ur(.045f));
            Cloud(g, S, Ux(.84f), Uy(.35f), Ur(.028f));

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
                // Shadow where the bun sinks into the steamer.
                float fx = (x - cx) / rimRx, front = rimY + rimRy * Mathf.Sqrt(Mathf.Max(0, 1 - fx * fx));
                c *= 1 - .4f * Mathf.SmoothStep(0, 1, (y - (front - Ur(.07f))) / Ur(.07f));
                c *= .93f + .07f * nz;
                c += Color.white * spec;
                c.a = Mathf.Clamp01((1 - Mathf.Sqrt(rr)) * rx * S * 1.5f);
                return c;
            });
            DrawKnot(g, S, knotX, knotY, Ur, bun, L, H);

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

            Cloud(g, S, Ux(.1f), Uy(.7f), Ur(.055f));
            Cloud(g, S, Ux(.9f), Uy(.7f), Ur(.05f));
            foreach (var a in new[] { -2.5f, -2.1f, -1.05f, -.65f })
            {
                float r0 = Ur(.39f), r1 = Ur(.45f), py = bunY - Ur(.02f);
                g.Line((cx + r0 * Mathf.Cos(a)) * S, (py + r0 * Mathf.Sin(a)) * S, (cx + r1 * Mathf.Cos(a)) * S, (py + r1 * Mathf.Sin(a)) * S, Hex("#FFFFFF", .9f), Ur(.012f) * S);
            }

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

        /// <summary>Soft steam puffs: white discs with a feathered edge.</summary>
        private static void Steam(Canvas2D g, float S, Vector3[] puffs, float alpha)
        {
            foreach (var p in puffs)
            {
                float px = p.x, py = p.y, pr = p.z;
                Shade(g, S, px - pr, py - pr, px + pr, py + pr, (x, y) =>
                {
                    float d = Mathf.Sqrt(Sq(x - px) + Sq(y - py)) / pr;
                    if (d > 1) return Color.clear;
                    return new Color(1, 1, 1, alpha * Mathf.SmoothStep(0, 1, (1 - d) * 2.2f) * (.85f + .15f * (1 - d)));
                });
            }
        }

        /// <summary>A cartoon steam puff: a cluster of round lobes, white on top with a cool shade underneath.</summary>
        private static void Cloud(Canvas2D g, float S, float x, float y, float r)
        {
            var lobes = new[] { new Vector3(0, 0, 1), new Vector3(-.85f, .25f, .7f), new Vector3(.85f, .25f, .72f), new Vector3(-.35f, -.55f, .72f), new Vector3(.4f, -.5f, .62f) };
            foreach (var l in lobes) Ellipse(g, S, x + l.x * r, y + l.y * r + r * .12f, l.z * r, l.z * r, (a, b) => Hex("#BFE3E0", .95f));
            foreach (var l in lobes) Ellipse(g, S, x + l.x * r, y + l.y * r, l.z * r * .97f, l.z * r * .97f, (a, b) => Hex("#FFFFFF", .97f));
        }

        internal static void DrawKnot(Canvas2D g, float S, float kx, float ky, Func<float, float> Ur, Color bun, Vector3 L, Vector3 H)
        {
            Color Dome(float x, float y, float cx, float cy, float rx, float ry, float shade)
            {
                float nx = (x - cx) / rx, ny = (y - cy) / ry;
                var nn = new Vector3(nx, ny, Mathf.Sqrt(Mathf.Max(0, 1 - nx * nx - ny * ny))).normalized;
                return bun * (shade + .36f * Mathf.Max(0, Vector3.Dot(nn, L))) + Color.white * (Mathf.Pow(Mathf.Max(0, Vector3.Dot(nn, H)), 50) * .35f);
            }
            if (Knot == 0)
            {
                // Dumpling crown: the pleats gather into a small puckered mound, folds twisting into a pinched centre.
                float cx = kx, cy = ky - Ur(.006f), rx = Ur(.064f), ry = Ur(.04f);
                Ellipse(g, S, cx, cy, rx, ry, (x, y) =>
                {
                    float nx = (x - cx) / rx, ny = (y - cy) / ry;
                    float a = Mathf.Atan2(ny, nx), d = Mathf.Sqrt(nx * nx + ny * ny);
                    // Puffy lobes between the folds, twisting towards the centre.
                    float lobe = Mathf.Abs(Mathf.Sin(5 * (a + (1 - d) * 1.3f)));
                    var c = Dome(x, y, cx, cy, rx, ry, .7f);
                    return c * (.78f + .26f * Mathf.Sqrt(lobe));
                });
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * Mathf.PI / 4;
                    Vector2 prev = new Vector2(cx, cy - ry * .15f);
                    for (int s = 1; s <= 6; s++)
                    {
                        float t = s / 6f, a = a0 + (1 - t) * .9f;
                        var p = new Vector2(cx + Mathf.Cos(a) * rx * t * .98f, cy - ry * .15f + Mathf.Sin(a) * ry * t * .98f);
                        g.Line(prev.x * S, prev.y * S, p.x * S, p.y * S, bun * .66f, Ur(.0075f) * (1.2f - t * .5f) * S);
                        prev = p;
                    }
                }
                float px0 = cx, py0 = cy - ry * .2f, pr = Ur(.017f);
                Ellipse(g, S, px0, py0, pr, pr * .85f, (x, y) => Dome(x, y, px0, py0, pr, pr * .85f, .8f));
            }
            else if (Knot == 1)
            {
                // Flush spiral nub: the pleats wind into a snail-shell swirl.
                float cx = kx, cy = ky - Ur(.008f), rx = Ur(.058f), ry = Ur(.038f);
                Ellipse(g, S, cx, cy, rx, ry, (x, y) => Dome(x, y, cx, cy, rx, ry, .72f));
                Vector2 prev = new Vector2(cx, cy - ry * .15f);
                for (int i = 1; i <= 60; i++)
                {
                    float t = i / 60f, a = t * Mathf.PI * 4.2f, rr = t * .92f;
                    var p = new Vector2(cx + Mathf.Cos(a) * rx * rr, cy - ry * .15f + Mathf.Sin(a) * ry * rr);
                    g.Line(prev.x * S, prev.y * S, p.x * S, p.y * S, bun * .66f, Ur(.0075f) * S);
                    prev = p;
                }
            }
            else
            {
                // Soft dimple: the pleats meet in a small pinched hollow with a lit lip.
                float cx = kx, cy = ky, rx = Ur(.04f), ry = Ur(.022f);
                Ellipse(g, S, cx, cy - Ur(.004f), rx * 1.2f, ry * 1.25f, (x, y) => Dome(x, y, cx, cy - Ur(.004f), rx * 1.2f, ry * 1.25f, .82f));
                Ellipse(g, S, cx, cy, rx * .55f, ry * .5f, (x, y) => bun * .6f);
            }
        }

        internal static float Sq(float a) { return a * a; }

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

        internal static void Twinkle(Canvas2D g, float S, float x, float y, float r)
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
