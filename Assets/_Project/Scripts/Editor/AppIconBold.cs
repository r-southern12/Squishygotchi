using System.Collections.Generic;
using System.IO;
using Squishy.Runtime.Three;
using UnityEditor;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Bold, eye-catching icon candidates: saturated grounds, a big close-cropped bun with a thick ink
    /// outline and an expressive face. Menu: Squishy > Icon Variants (writes a side-by-side sheet).
    /// </summary>
    public static class AppIconBold
    {
        private const string Dir = "Assets/_Project/Art/Icon";
        private static readonly Color Ink = Canvas2D.Css("#3A2418");

        [MenuItem("Squishy/Icon Variants")]
        public static void Variants()
        {
            Directory.CreateDirectory(Dir);
            const int S = 512, Gap = 24;
            var sheet = new Canvas2D(S * 3 + Gap * 4, S + Gap * 2);
            sheet.FillRect(0, 0, sheet.Width, sheet.Height, Canvas2D.Css("#FFFFFF"));
            for (int v = 0; v < 3; v++) Draw(sheet, Gap + v * (S + Gap), Gap, S, v);
            var t = sheet.ToTexture(true, false, false, "sheet", true);
            File.WriteAllBytes(Path.Combine(Dir, "icon_variants.png"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        private static Color Hex(string h, float a = 1) { var c = Canvas2D.Css(h); c.a = a; return c; }

        /// <summary>Variant 0 "Pop" (teal), 1 "Wink" (tomato, steamer-lid hat), 2 "Peek" (sunny, over the rim).</summary>
        public static void Draw(Canvas2D g, float ox, float oy, float S, int v)
        {
            System.Func<float, float> X = u => ox + u * S, Y = u => oy + u * S, R = u => u * S;
            float ow = .03f; // outline width
            string[] grounds = { "#1F8A8A", "#E2553A", "#F5B82E" };
            string[] glows = { "#5CC3B8", "#FF8A5C", "#FFE07A" };
            Color ground = Hex(grounds[v]), glow = Hex(glows[v]);
            // Rounded-square tile with a radial glow behind the bun.
            float rad = .22f;
            foreach (var c in new[] { new Vector2(rad, rad), new Vector2(1 - rad, rad), new Vector2(rad, 1 - rad), new Vector2(1 - rad, 1 - rad) })
                g.FillCircle(X(c.x), Y(c.y), R(rad), ground);
            g.FillRect(X(rad), Y(0), R(1 - 2 * rad), R(1), ground);
            g.FillRect(X(0), Y(rad), R(1), R(1 - 2 * rad), ground);
            for (int i = 10; i >= 1; i--) g.FillCircle(X(.5f), Y(.5f), R(.05f * i), Hex(glows[v], .07f));
            // Sunburst rays for extra pop.
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * Mathf.PI / 6, a1 = a0 + Mathf.PI / 18;
                var ray = new List<Vector2> { new Vector2(X(.5f), Y(.55f)), new Vector2(X(.5f + .7f * Mathf.Cos(a0)), Y(.55f + .7f * Mathf.Sin(a0))), new Vector2(X(.5f + .7f * Mathf.Cos(a1)), Y(.55f + .7f * Mathf.Sin(a1))) };
                ClipRay(ray, X(0), Y(0), X(1), Y(1));
                g.FillPolygon(ray, new Color(glow.r, glow.g, glow.b, .16f));
            }

            float cx = .5f, cy = v == 2 ? .62f : .58f, rx = .38f, ry = .32f;
            if (v == 2)
            {
                // Steamer rim the bun peeks over (drawn behind), then the bun, then the rim front.
                Blob(g, X(.5f), Y(.8f), R(.47f), R(.1f), Hex("#8A5A32"), R(ow));
            }
            // Bun body with a thick outline, soft underside shade and a big gloss.
            Blob(g, X(cx), Y(cy), R(rx), R(ry), Hex("#FFF6E8"), R(ow));
            g.FillEllipse(X(cx), Y(cy + ry * .55f), R(rx * .8f), R(ry * .32f), 0, Hex("#F1DDC0"));
            g.FillEllipse(X(cx - .17f), Y(cy - .16f), R(.075f), R(.04f), -.6f, Hex("#FFFFFF"));
            // Pleated swirl knot on top, outlined so it reads at tiny sizes.
            float ty = cy - ry + .01f;
            Blob(g, X(cx), Y(ty - .02f), R(.075f), R(.055f), Hex("#FFF6E8"), R(ow * .8f));
            g.StrokeArc(X(cx + .005f), Y(ty - .03f), R(.03f), Mathf.PI * .8f, Mathf.PI * 2.5f, Ink, R(.016f));
            for (int i = -2; i <= 2; i++) if (i != 0)
                g.Line(X(cx + i * .03f), Y(ty + .03f), X(cx + i * .055f), Y(ty + .075f), Hex("#D9BF98"), R(.014f));

            if (v == 1)
            {
                // Mini bamboo steamer lid worn as a jaunty hat.
                g.Save();
                g.Translate(X(cx + .17f), Y(cy - ry - .02f));
                g.Rotate(.35f);
                Blob(g, 0, 0, R(.17f), R(.06f), Hex("#D9A15E"), R(ow * .8f));
                g.FillEllipse(0, -R(.012f), R(.12f), R(.035f), 0, Hex("#E9BD7F"));
                g.Line(-R(.1f), 0, R(.1f), 0, Hex("#A8743F"), R(.01f));
                g.FillCircle(0, -R(.045f), R(.02f), Hex("#A8743F"));
                g.Restore();
            }

            // Face.
            float ey = cy + .02f, ex = .12f, er = .06f;
            if (v == 1)
            {
                Eye(g, X(cx - ex), Y(ey), R(er));
                g.StrokeArc(X(cx + ex), Y(ey + .02f), R(.045f), Mathf.PI * 1.15f, Mathf.PI * 1.85f, Ink, R(.024f)); // wink
            }
            else
            {
                Eye(g, X(cx - ex), Y(ey), R(er));
                Eye(g, X(cx + ex), Y(ey), R(er));
            }
            g.FillEllipse(X(cx - .21f), Y(ey + .075f), R(.055f), R(.032f), 0, Hex("#FF8FA3", .85f));
            g.FillEllipse(X(cx + .21f), Y(ey + .075f), R(.055f), R(.032f), 0, Hex("#FF8FA3", .85f));
            // Open happy mouth with a little tongue.
            var mouth = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float a = Mathf.PI * i / 16; mouth.Add(new Vector2(X(cx - .05f * Mathf.Cos(a)), Y(ey + .07f + .055f * Mathf.Sin(a)))); }
            g.FillPolygon(mouth, Ink);
            g.FillEllipse(X(cx), Y(ey + .105f), R(.028f), R(.016f), 0, Hex("#FF6F86"));

            if (v == 2)
            {
                // Front of the rim and two little nub hands gripping it.
                var front = new List<Vector2>();
                for (int i = 0; i <= 24; i++) { float a = Mathf.PI * i / 24; front.Add(new Vector2(X(.5f - .47f * Mathf.Cos(a)), Y(.8f + .1f * Mathf.Sin(a)))); }
                for (int i = 24; i >= 0; i--) { float a = Mathf.PI * i / 24; front.Add(new Vector2(X(.5f - .47f * Mathf.Cos(a)), Y(1.02f + .1f * Mathf.Sin(a)))); }
                g.FillPolygon(Grow(front, R(ow)), Ink);
                g.FillPolygon(front, Hex("#D9A15E"));
                for (int s = 1; s <= 2; s++)
                    for (int i = 1; i <= 24; i++)
                    {
                        float a0 = Mathf.PI * (i - 1) / 24, a1 = Mathf.PI * i / 24, y0 = .8f + s * .07f;
                        g.Line(X(.5f - .47f * Mathf.Cos(a0)), Y(y0 + .1f * Mathf.Sin(a0)), X(.5f - .47f * Mathf.Cos(a1)), Y(y0 + .1f * Mathf.Sin(a1)), Hex("#A8743F"), R(.012f));
                    }
                Blob(g, X(cx - .2f), Y(.86f), R(.055f), R(.04f), Hex("#FFF6E8"), R(ow * .8f));
                Blob(g, X(cx + .2f), Y(.86f), R(.055f), R(.04f), Hex("#FFF6E8"), R(ow * .8f));
            }
            // Sparkles.
            Sparkle(g, X(.18f), Y(.2f), R(.05f));
            Sparkle(g, X(.83f), Y(.27f), R(.035f));
        }

        private static void Blob(Canvas2D g, float x, float y, float rx, float ry, Color fill, float outline)
        {
            g.FillEllipse(x, y, rx + outline, ry + outline, 0, Ink);
            g.FillEllipse(x, y, rx, ry, 0, fill);
        }

        private static void Eye(Canvas2D g, float x, float y, float r)
        {
            g.FillEllipse(x, y, r * .82f, r, 0, Ink);
            g.FillCircle(x + r * .25f, y - r * .35f, r * .36f, Color.white);
            g.FillCircle(x - r * .3f, y + r * .38f, r * .16f, Color.white);
        }

        private static void Sparkle(Canvas2D g, float x, float y, float r)
        {
            var star = new List<Vector2>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, d = i % 2 == 0 ? r : r * .28f;
                star.Add(new Vector2(x + d * Mathf.Cos(a), y + d * Mathf.Sin(a)));
            }
            g.FillPolygon(star, Color.white);
        }

        private static List<Vector2> Grow(List<Vector2> pts, float d)
        {
            var c = Vector2.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;
            var o = new List<Vector2>();
            foreach (var p in pts) { var n = (p - c); float l = n.magnitude; o.Add(l > 0 ? p + n / l * d : p); }
            return o;
        }

        private static void ClipRay(List<Vector2> ray, float x0, float y0, float x1, float y1)
        {
            for (int i = 0; i < ray.Count; i++) ray[i] = new Vector2(Mathf.Clamp(ray[i].x, x0, x1), Mathf.Clamp(ray[i].y, y0, y1));
        }
    }
}
