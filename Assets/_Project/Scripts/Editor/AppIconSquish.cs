using System;
using System.IO;
using Squishy.Runtime.Three;
using UnityEditor;
using UnityEngine;
using static Squishy.EditorTools.AppIconPaint;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Icon concept "squished against the glass": the bun fills the icon, pressed flat on the screen, with a
    /// flattened contact patch, creased pleats bulging round the edges and glass reflections on top.
    /// A joyful squint, B big eyes with hands and breath fog, C tilted cheek-smoosh.
    /// Menu: Squishy > Icon Variants (squish).
    /// </summary>
    public static class AppIconSquish
    {
        private const string Dir = "Assets/_Project/Art/Icon";

        [MenuItem("Squishy/Icon Variants (squish)")]
        public static void Variants()
        {
            Directory.CreateDirectory(Dir);
            for (int v = 0; v < 3; v++) File.WriteAllBytes(Path.Combine(Dir, "icon_squish_" + "ABC"[v] + ".png"), Rounded(Draw(1024, v)));
        }

        /// <summary>Preview as a phone shows it: masked to a rounded square.</summary>
        private static byte[] Rounded(Canvas2D g)
        {
            var t = g.ToTexture(true, false, false, "icon", true);
            int n = t.width;
            var px = t.GetPixels32();
            float rad = n * .22f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Clamp(x + .5f, rad, n - rad), qy = Mathf.Clamp(y + .5f, rad, n - rad);
                float d = Mathf.Sqrt(Sq(x + .5f - qx) + Sq(y + .5f - qy));
                float a = Mathf.Clamp01(rad - d + .5f);
                var p = px[y * n + x];
                p.a = (byte)(p.a * a);
                px[y * n + x] = p;
            }
            t.SetPixels32(px);
            t.Apply();
            var png = t.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(t);
            return png;
        }

        private static float Sstep(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }

        public static Canvas2D Draw(int size, int v)
        {
            var g = new Canvas2D(size, size);
            float S = size;
            string[] grounds = { "#1F8A8A", "#F5B82E", "#6C5CE7" }, glows = { "#7FD6C8", "#FFE9A0", "#B7A8FF" };
            Color gc = Hex(grounds[v]), gl = Hex(glows[v]);
            Shade(g, S, 0, 0, 1, 1, (x, y) =>
            {
                float dx = x - .5f, dy = y - .5f, dist = Mathf.Sqrt(dx * dx + dy * dy);
                float ray = Mathf.Sin(Mathf.Atan2(dy, dx) * 12) > .3f ? .1f : 0;
                return Color.Lerp(gc, gl, Mathf.Clamp01(1 - dist / .7f) * .5f + ray);
            });
            Twinkle(g, S, .12f, .12f, .05f);
            Twinkle(g, S, .9f, .1f, .035f);

            // The bun in its own (possibly tilted) frame.
            float cx = v == 2 ? .56f : .5f, cy = v == 2 ? .64f : .62f, ang = v == 2 ? -.28f : 0, rx = .62f, ry = .58f;
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            Vector2 W(float lx, float ly) => new Vector2(cx + lx * ca - ly * sa, cy + lx * sa + ly * ca);
            Vector2 Loc(float x, float y) { float dx = x - cx, dy = y - cy; return new Vector2(dx * ca + dy * sa, -dx * sa + dy * ca); }
            float pcx = v == 2 ? -.1f : 0, pcy = .03f, prx = .42f, pry = .36f, knotLy = -ry + .05f;
            var bun = Hex("#F7A8C6");
            var L = new Vector3(-.45f, -.6f, .66f).normalized;
            var H = (L + Vector3.forward).normalized;

            Shade(g, S, 0, 0, 1, 1, (x, y) =>
            {
                var l = Loc(x, y);
                float nx = l.x / rx, ny = l.y / ry, rr = nx * nx + ny * ny;
                if (rr > 1) return Color.clear;
                float nz = Mathf.Sqrt(1 - rr);
                // Pleats radiating from the knot at the top.
                float kx = l.x / rx, ky = (l.y - knotLy) / ry, dist = Mathf.Sqrt(kx * kx + ky * ky);
                float phi = Mathf.Atan2(kx, ky * 1.4f) + dist * .5f, s = Mathf.Abs(Mathf.Sin(7 * phi));
                float fade = Mathf.Clamp01(1 - dist / .9f);
                fade *= fade;
                // The flattened patch pressed on the glass (a slightly wobbly ellipse).
                float qx = (l.x - pcx) / prx, qy = (l.y - pcy) / pry;
                float e = Mathf.Sqrt(qx * qx + qy * qy) * (1 + .012f * Mathf.Sin(3 * Mathf.Atan2(qy, qx) + 1));
                float pin = 1 - Sstep(.9f, 1, e);
                float crease = Mathf.Pow(1 - s, 5) * fade * (1 - .8f * pin);
                float tilt = Mathf.Cos(7 * phi) * Mathf.Sign(Mathf.Sin(7 * phi)) * .3f * fade * (1 - pin);
                var dn = new Vector3(nx + tilt * Mathf.Cos(phi), ny, nz).normalized;
                var n = Vector3.Lerp(dn, Vector3.forward, pin).normalized;
                var nw = new Vector3(n.x * ca - n.y * sa, n.x * sa + n.y * ca, n.z);
                float diff = Mathf.Max(0, Vector3.Dot(nw, L));
                float spec = Mathf.Pow(Mathf.Max(0, Vector3.Dot(nw, H)), 70) * .45f * (1 - pin);
                var c = bun * (.74f + .34f * diff);
                c = Color.Lerp(c, Hex("#FFC4D8"), pin * .45f);
                c *= 1 - .38f * crease;
                c *= .9f + .1f * Mathf.Max(nz, pin);
                c *= 1 - .14f * Mathf.Exp(-Sq((e - 1) / .025f));          // where it meets the glass
                c += Color.white * (.2f * Mathf.Exp(-Sq((e - 1.06f) / .035f)) + spec); // glassy rim just outside
                c.a = Mathf.Clamp01((1 - Mathf.Sqrt(rr)) * rx * S * 1.5f);
                return c;
            });
            var kw = W(0, knotLy);
            Knot = 0;
            DrawKnot(g, S, kw.x, kw.y, a => a * 1.4f, bun, L, H);

            // ---- local drawing helpers (in the bun's frame) ----
            void EL(float lx, float ly, float erx, float ery, Func<float, float, Color> col)
            {
                var w = W(lx, ly);
                float m = Mathf.Max(erx, ery);
                Shade(g, S, w.x - m, w.y - m, w.x + m, w.y + m, (x, y) =>
                {
                    var l = Loc(x, y);
                    float e = Sq((l.x - lx) / erx) + Sq((l.y - ly) / ery);
                    if (e > 1) return Color.clear;
                    var c = col(l.x, l.y);
                    c.a *= Mathf.Clamp01((1 - Mathf.Sqrt(e)) * Mathf.Min(erx, ery) * S * 2);
                    return c;
                });
            }
            void Soft(float lx, float ly, float erx, float ery, Color col)
            {
                var w = W(lx, ly);
                float m = Mathf.Max(erx, ery);
                Shade(g, S, w.x - m, w.y - m, w.x + m, w.y + m, (x, y) =>
                {
                    var l = Loc(x, y);
                    float e = Sq((l.x - lx) / erx) + Sq((l.y - ly) / ery);
                    return new Color(col.r, col.g, col.b, col.a * Mathf.Clamp01(1 - e) * Mathf.Clamp01((1 - e) * 3));
                });
            }
            void LL(float x1, float y1, float x2, float y2, Color c, float w)
            {
                var p = W(x1, y1); var q = W(x2, y2);
                g.Line(p.x * S, p.y * S, q.x * S, q.y * S, c, w * S);
                g.FillCircle(p.x * S, p.y * S, w * S / 2, c);
                g.FillCircle(q.x * S, q.y * S, w * S / 2, c);
            }
            void ArcL(float lx, float ly, float r, float a0, float a1, Color c, float w)
            {
                for (int i = 0; i < 16; i++)
                {
                    float t0 = Mathf.Lerp(a0, a1, i / 16f), t1 = Mathf.Lerp(a0, a1, (i + 1) / 16f);
                    LL(lx + r * Mathf.Cos(t0), ly + r * Mathf.Sin(t0), lx + r * Mathf.Cos(t1), ly + r * Mathf.Sin(t1), c, w);
                }
            }
            var ink = Hex("#1A1216");
            void Eye(float lx, float ly, float erx, float ery)
            {
                EL(lx, ly, erx, ery, (x, y) => Color.Lerp(Hex("#111014"), Hex("#3B2A30"), Mathf.Clamp01((y - ly) / ery)));
                EL(lx + erx * .3f, ly - ery * .34f, erx * .36f, erx * .36f, (x, y) => Color.white);
                EL(lx - erx * .32f, ly + ery * .4f, erx * .15f, erx * .15f, (x, y) => Hex("#FFFFFF", .85f));
            }
            void Blush(float lx, float ly, float brx, float bry)
            {
                Soft(lx, ly, brx, bry, Hex("#FF7F8E", .85f));
                for (int i = -1; i <= 1; i++) LL(lx + i * brx * .38f + brx * .1f, ly - bry * .35f, lx + i * brx * .38f - brx * .1f, ly + bry * .35f, Hex("#FFFFFF", .7f), .009f);
            }

            if (v == 0)
            {
                // Joyful squint: > < eyes, big blush, wide little "w".
                Blush(-.29f, .1f, .1f, .055f);
                Blush(.29f, .1f, .1f, .055f);
                LL(-.23f, -.05f, -.13f, 0, ink, .034f); LL(-.13f, 0, -.23f, .05f, ink, .034f);
                LL(.23f, -.05f, .13f, 0, ink, .034f); LL(.13f, 0, .23f, .05f, ink, .034f);
                ArcL(-.03f, .08f, .03f, 0, Mathf.PI, ink, .016f);
                ArcL(.03f, .08f, .03f, 0, Mathf.PI, ink, .016f);
            }
            else if (v == 1)
            {
                // Big eyes and an "o" mouth fogging up the glass.
                Blush(-.3f, .07f, .085f, .045f);
                Blush(.3f, .07f, .085f, .045f);
                Eye(-.17f, -.03f, .085f, .078f);
                Eye(.17f, -.03f, .085f, .078f);
                EL(0, .1f, .05f, .036f, (x, y) => ink);
                EL(0, .118f, .03f, .015f, (x, y) => Hex("#FF6F86"));
                Soft(0, .14f, .2f, .095f, Hex("#FFFFFF", .42f));
                Soft(-.13f, .18f, .09f, .05f, Hex("#FFFFFF", .3f));
                Soft(.12f, .12f, .08f, .045f, Hex("#FFFFFF", .28f));
            }
            else
            {
                // Tilted cheek-smoosh: pressed eye squeezed shut, the other wide open, a sweat drop.
                Blush(-.3f, .09f, .13f, .07f);
                Blush(.29f, .06f, .075f, .04f);
                LL(-.25f, -.05f, -.15f, -.01f, ink, .032f); LL(-.15f, -.01f, -.25f, .03f, ink, .032f);
                Eye(.17f, -.05f, .08f, .085f);
                EL(-.03f, .09f, .04f, .024f, (x, y) => ink);
                EL(-.03f, .101f, .024f, .01f, (x, y) => Hex("#FF6F86"));
                var d = W(.36f, -.22f);
                var drop = new System.Collections.Generic.List<Vector2>();
                for (int i = 0; i <= 24; i++)
                {
                    float t = i / 24f * Mathf.PI * 2, r = .035f;
                    float px = d.x + r * Mathf.Sin(t), py = d.y + r * Mathf.Cos(t);
                    if (t > Mathf.PI * .7f && t < Mathf.PI * 1.3f) { px = d.x + r * Mathf.Sin(t) * .3f; py = d.y - r * 1.9f; }
                    drop.Add(new Vector2(px * S, py * S));
                }
                g.FillPolygon(drop, Hex("#8FD3FF"));
                g.FillCircle((d.x - .01f) * S, (d.y + .008f) * S, .009f * S, Color.white);
            }

            // Glass on top: two diagonal reflections.
            Shade(g, S, 0, 0, .7f, .7f, (x, y) =>
            {
                float u = x + y;
                float a = .3f * Sstep(.3f, .33f, u) * (1 - Sstep(.43f, .46f, u)) + .2f * Sstep(.5f, .515f, u) * (1 - Sstep(.54f, .555f, u));
                return new Color(1, 1, 1, a);
            });
            return g;
        }
    }
}
