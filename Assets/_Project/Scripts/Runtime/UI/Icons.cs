using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// The prototype's inline SVG icons, drawn from their original path data with Painter2D.
    /// Supports M L H V C S Q A Z (absolute and relative), fills and round-capped strokes.
    /// </summary>
    public static class Icons
    {
        private sealed class Layer
        {
            public string d;
            public string fill, stroke;
            public float width;
        }

        private static readonly Dictionary<string, Layer[]> Defs = new Dictionary<string, Layer[]>
        {
            { "hunger", new[] { F("M3 11h18a9 9 0 0 1-18 0z", "#C8674E"), S("M8 8c0-2 2-2 2-4M13 8c0-2 2-2 2-4", "#A67C52", 1.8f) } },
            { "hunger_bubble", new[] { F("M3 11h18a9 9 0 0 1-18 0z", "#C8674E") } },
            { "play", new[] { F(Circle(12, 12, 8.5f), "#6E9C9A"), S("M4 12h16M12 3.5c3 3 3 14 0 17", "#F7F0E4", 1.8f) } },
            { "play_bubble", new[] { F(Circle(12, 12, 8.5f), "#6E9C9A") } },
            { "rest", new[] { F("M15 3a8.5 8.5 0 1 0 6 13.5A7 7 0 0 1 15 3z", "#8C7BB0") } },
            { "clean", new[] { F("M12 3s6 7 6 11a6 6 0 0 1-12 0c0-4 6-11 6-11z", "#7FB0C9") } },
            { "idle", new[] { F("M12 3.5l2.4 5 5.4.6-4 3.7 1.1 5.4L12 15.5l-4.9 2.7 1.1-5.4-4-3.7 5.4-.6z", "#D9A64A") } },
            { "coin", new[] { F(Circle(12, 12, 10), "#D9A64A"), F(Circle(12, 12, 7), "#E9BE66"), S("M10 6.5v11M14 6.5v11M10 10h4M10 14h4", "#9A6B22", 1.8f) } },
            { "sound_off", new[] { FS("M4 9v6h4l5 4V5L8 9z", "currentColor", 2), S("M16 9l5 6M21 9l-5 6", "currentColor", 2) } },
            { "sound_on", new[] { FS("M4 9v6h4l5 4V5L8 9z", "currentColor", 2), S("M16 8.5a5 5 0 0 1 0 7M18.5 6a8.5 8.5 0 0 1 0 12", "currentColor", 2) } },
            { "back", new[] { S("M14 6l-6 6 6 6", "currentColor", 2.4f) } },
            { "friends", new[] { F(Circle(9, 8.5f, 3.4f), "#33261D"), F(Circle(16.5f, 9.5f, 2.8f), "#6E9C9A"), S("M3.2 19.5c.9-3.6 3.1-5.2 5.8-5.2s4.9 1.6 5.8 5.2", "#33261D", 2.1f), S("M14.6 14.6c2.9-.5 5.4 1 6.2 4.4", "#6E9C9A", 2.1f) } },
            { "view_room", new[] { S("M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5", "#33261D", 2.2f) } },
            { "view_follow", new[] { S("M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5", "#33261D", 2.2f) } },
            { "tasks", new[] { F(RectPath(4, 3, 16, 18, 3), "#6F9A74"), S("M8 9l2 2 4-4M8 16h8", "#F7F0E4", 2) } },
            { "shop", new[] { F("M5 8h14l-1.2 11.5a2 2 0 0 1-2 1.5H8.2a2 2 0 0 1-2-1.5z", "#D9A64A"), S("M9 8V6.5a3 3 0 0 1 6 0V8", "#8C5A2A", 2) } },
            { "undo", new[] { S("M9 14L4 9l5-5", "#33261D", 2.2f), S("M4 9h10a6 6 0 0 1 0 12h-3", "#33261D", 2.2f) } },
            { "catalogue", new[] { F(RectPath(4, 3, 15, 18, 3), "#C8674E"), F(RectPath(7, 6, 9, 5, 1.5f), "#F7F0E4"), S("M7 14h9M7 17h6", "#F7F0E4", 1.8f) } },
            { "edit", new[] { FS("M4 20h4L19 9l-4-4L4 16z", "#E9BE66", 2.2f, "#33261D"), S("M13.5 6.5l4 4", "#33261D", 2.2f) } },
            { "rotate", new[] { S("M20 12a8 8 0 1 1-2.3-5.7", "#33261D", 2.2f), S("M20 4v5h-5", "#33261D", 2.2f) } },
            { "tilt", new[] { FS("M3 16l9 4 9-4-9-4z", "#E9BE66", 2.2f, "#33261D"), S("M12 3v6M9 6l3-3 3 3", "#33261D", 2.2f) } },
            { "close", new[] { S("M6 6l12 12M18 6L6 18", "currentColor", 2.4f) } },
            { "lock", new[] { F(RectPath(2, 5, 8, 6, 1.5f), "#6F5F52"), S("M4 5V4a2 2 0 0 1 4 0v1", "#6F5F52", 1.4f) } },
            { "plus", new[] { S("M12 5v14M5 12h14", "currentColor", 2.6f) } },
            { "minus", new[] { S("M5 12h14", "currentColor", 2.6f) } },
            { "check", new[] { S("M5 12.5l4.5 4.5L19 7.5", "currentColor", 3.2f) } },
            { "gift", new[] { F(RectPath(4, 9, 16, 12, 2), "#C8674E"), F(RectPath(3, 6, 18, 4.5f, 1.5f), "#D9A64A"), S("M12 6v15", "#FFF7EC", 2), S("M12 6c-2-4-6-3-5 0M12 6c2-4 6-3 5 0", "#D9A64A", 1.8f) } },
            { "gear", new[] { F(Circle(12, 12, 7.5f), "#33261D"), F(Circle(12, 12, 3), "#F7F0E4"), S("M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.3 5.3l2.1 2.1M16.6 16.6l2.1 2.1M5.3 18.7l2.1-2.1M16.6 7.4l2.1-2.1", "#33261D", 2.6f) } },
            { "star", new[] { F("M12 2l2.9 6.6 7.1.7-5.4 4.8 1.6 7L12 17.3 5.8 21.1l1.6-7L2 9.3l7.1-.7z", "currentColor") } },
            { "star_empty", new[] { S("M12 3.2l2.6 5.9 6.3.6-4.8 4.3 1.4 6.2L12 16.9l-5.5 3.3 1.4-6.2L3.1 9.7l6.3-.6z", "currentColor", 1.8f) } },
        };

        private static Layer F(string d, string fill) { return new Layer { d = d, fill = fill }; }
        private static Layer S(string d, string stroke, float w) { return new Layer { d = d, stroke = stroke, width = w }; }
        private static Layer FS(string d, string fill, float w, string stroke = null) { return new Layer { d = d, fill = fill, stroke = stroke ?? fill, width = w }; }

        private static string Circle(float cx, float cy, float r)
        {
            return string.Format(CultureInfo.InvariantCulture, "M{0} {1}a{2} {2} 0 1 0 {3} 0a{2} {2} 0 1 0 {4} 0z", cx - r, cy, r, 2 * r, -2 * r);
        }

        private static string RectPath(float x, float y, float w, float h, float r)
        {
            return string.Format(CultureInfo.InvariantCulture, "M{0} {1}h{2}a{3} {3} 0 0 1 {3} {3}v{4}a{3} {3} 0 0 1 -{3} {3}h-{2}a{3} {3} 0 0 1 -{3} -{3}v-{4}a{3} {3} 0 0 1 {3} -{3}z", x + r, y, w - 2 * r, r, h - 2 * r);
        }

        /// <summary>An icon element; <paramref name="color"/> replaces currentColor.</summary>
        public static Glyph Make(string name, float size, string color = "#33261D", float viewBox = 24)
        {
            var g = new Glyph(size, null, name == "lock" ? 12 : viewBox);
            Set(g, name, color);
            return g;
        }

        public static void Set(Glyph g, string name, string color = "#33261D")
        {
            var layers = Defs[name];
            var cur = Css.C(color);
            g.ViewBox = name == "lock" ? 12 : 24;
            g.Draw = (p, k) =>
            {
                foreach (var l in layers)
                {
                    if (l.fill != null)
                    {
                        p.fillColor = l.fill == "currentColor" ? cur : Css.C(l.fill);
                        Path(p, l.d, k);
                        p.Fill(FillRule.NonZero);
                    }
                    if (l.stroke != null)
                    {
                        p.strokeColor = l.stroke == "currentColor" ? cur : Css.C(l.stroke);
                        p.lineWidth = l.width * k;
                        p.lineCap = LineCap.Round;
                        p.lineJoin = LineJoin.Round;
                        Path(p, l.d, k);
                        p.Stroke();
                    }
                }
            };
            g.MarkDirtyRepaint();
        }

        private static readonly Regex Tok = new Regex(@"[MmLlHhVvCcSsQqAaZz]|-?(?:\d+\.?\d*|\.\d+)(?:e-?\d+)?", RegexOptions.Compiled);

        /// <summary>Builds an SVG path into the painter, scaled by k.</summary>
        public static void Path(Painter2D p, string d, float k)
        {
            var toks = new List<string>();
            foreach (Match m in Tok.Matches(d)) toks.Add(m.Value);
            int i = 0;
            char cmd = 'M';
            Vector2 cur = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero;
            char prevCmd = ' ';
            p.BeginPath();
            float N() { return float.Parse(toks[i++], CultureInfo.InvariantCulture); }
            Vector2 P(Vector2 v) { return v * k; }
            while (i < toks.Count)
            {
                if (char.IsLetter(toks[i][0])) cmd = toks[i++][0];
                bool rel = char.IsLower(cmd);
                Vector2 o = rel ? cur : Vector2.zero;
                switch (char.ToUpper(cmd))
                {
                    case 'M':
                        cur = o + new Vector2(N(), N());
                        start = cur;
                        p.MoveTo(P(cur));
                        cmd = rel ? 'l' : 'L';
                        break;
                    case 'L': cur = o + new Vector2(N(), N()); p.LineTo(P(cur)); break;
                    case 'H': cur = new Vector2((rel ? cur.x : 0) + N(), cur.y); p.LineTo(P(cur)); break;
                    case 'V': cur = new Vector2(cur.x, (rel ? cur.y : 0) + N()); p.LineTo(P(cur)); break;
                    case 'C':
                    {
                        Vector2 c1 = o + new Vector2(N(), N()), c2 = o + new Vector2(N(), N()), e = o + new Vector2(N(), N());
                        p.BezierCurveTo(P(c1), P(c2), P(e));
                        lastCtrl = c2;
                        cur = e;
                        break;
                    }
                    case 'S':
                    {
                        Vector2 c1 = "CcSs".IndexOf(prevCmd) >= 0 ? 2 * cur - lastCtrl : cur;
                        Vector2 c2 = o + new Vector2(N(), N()), e = o + new Vector2(N(), N());
                        p.BezierCurveTo(P(c1), P(c2), P(e));
                        lastCtrl = c2;
                        cur = e;
                        break;
                    }
                    case 'Q':
                    {
                        Vector2 c = o + new Vector2(N(), N()), e = o + new Vector2(N(), N());
                        p.QuadraticCurveTo(P(c), P(e));
                        cur = e;
                        break;
                    }
                    case 'A':
                    {
                        float r = N();
                        N(); N();
                        bool large = N() != 0, sweep = N() != 0;
                        var e = o + new Vector2(N(), N());
                        Arc(p, cur, r, large, sweep, e, k);
                        cur = e;
                        break;
                    }
                    case 'Z':
                        p.ClosePath();
                        cur = start;
                        break;
                }
                prevCmd = cmd;
            }
        }

        /// <summary>SVG circular arc (endpoint form) converted to a centre arc.</summary>
        private static void Arc(Painter2D p, Vector2 a, float r, bool large, bool sweep, Vector2 b, float k)
        {
            Vector2 h = (a - b) / 2;
            float d2 = h.sqrMagnitude;
            if (d2 < 1e-10f) return;
            if (d2 > r * r) r = Mathf.Sqrt(d2);
            float coef = Mathf.Sqrt(Mathf.Max(0, (r * r - d2) / d2)) * (large == sweep ? -1 : 1);
            Vector2 cp = new Vector2(coef * h.y, -coef * h.x);
            Vector2 c = cp + (a + b) / 2;
            float t1 = Mathf.Atan2(a.y - c.y, a.x - c.x) * Mathf.Rad2Deg, t2 = Mathf.Atan2(b.y - c.y, b.x - c.x) * Mathf.Rad2Deg;
            p.Arc(c * k, r * k, Angle.Degrees(t1), Angle.Degrees(t2), sweep ? ArcDirection.Clockwise : ArcDirection.CounterClockwise);
        }
    }
}
