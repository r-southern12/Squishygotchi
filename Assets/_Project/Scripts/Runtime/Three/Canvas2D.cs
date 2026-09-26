using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Squishy.Runtime.Three
{
    /// <summary>
    /// A small software version of the HTML canvas 2D calls the prototype uses to paint its textures
    /// (fillRect, arc, ellipse, lines with butt caps, strokeRect, paths, save/translate/rotate).
    /// Shapes are anti-aliased with 4x4 supersampling and blended source-over in display (sRGB) space,
    /// like a browser canvas. <see cref="ToTexture"/> flips rows the way three.js CanvasTexture does.
    /// </summary>
    public sealed class Canvas2D
    {
        public readonly int Width, Height;
        private readonly Color[] _px;
        private float _a = 1, _b, _c, _d = 1, _e, _f;
        private readonly Stack<float[]> _stack = new Stack<float[]>();

        public Color FillStyle = Color.black;
        public Color StrokeStyle = Color.black;
        public float LineWidth = 1f;

        public Canvas2D(int width, int height)
        {
            Width = width;
            Height = height;
            _px = new Color[width * height];
        }

        // ---- transform ----

        public void Save() { _stack.Push(new[] { _a, _b, _c, _d, _e, _f }); }

        public void Restore()
        {
            if (_stack.Count == 0) return;
            var t = _stack.Pop();
            _a = t[0]; _b = t[1]; _c = t[2]; _d = t[3]; _e = t[4]; _f = t[5];
        }

        public void Translate(float x, float y)
        {
            _e += _a * x + _c * y;
            _f += _b * x + _d * y;
        }

        public void Rotate(float r)
        {
            float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            float a = _a * cs + _c * sn, b = _b * cs + _d * sn, c = -_a * sn + _c * cs, d = -_b * sn + _d * cs;
            _a = a; _b = b; _c = c; _d = d;
        }

        // ---- drawing ----

        public void FillRect(float x, float y, float w, float h, Color? col = null)
        {
            Paint((ux, uy) => ux >= x && ux < x + w && uy >= y && uy < y + h, x, y, x + w, y + h, col ?? FillStyle);
        }

        /// <summary>arc(x, y, r, 0, 2pi) + fill.</summary>
        public void FillCircle(float x, float y, float r, Color? col = null)
        {
            float r2 = r * r;
            Paint((ux, uy) => (ux - x) * (ux - x) + (uy - y) * (uy - y) <= r2, x - r, y - r, x + r, y + r, col ?? FillStyle);
        }

        /// <summary>ellipse(x, y, rx, ry, rotation, 0, full) + fill.</summary>
        public void FillEllipse(float x, float y, float rx, float ry, float rotation, Color? col = null)
        {
            float cs = Mathf.Cos(-rotation), sn = Mathf.Sin(-rotation), m = Mathf.Max(rx, ry);
            Paint((ux, uy) =>
            {
                float dx = ux - x, dy = uy - y;
                float lx = dx * cs - dy * sn, ly = dx * sn + dy * cs;
                return (lx * lx) / (rx * rx) + (ly * ly) / (ry * ry) <= 1f;
            }, x - m, y - m, x + m, y + m, col ?? FillStyle);
        }

        /// <summary>A stroked straight line (butt caps).</summary>
        public void Line(float x1, float y1, float x2, float y2, Color? col = null, float? width = null)
        {
            float lw = width ?? LineWidth, hw = lw / 2f;
            float dx = x2 - x1, dy = y2 - y1, len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-5f) return;
            float ux0 = dx / len, uy0 = dy / len;
            Paint((ux, uy) =>
            {
                float px = ux - x1, py = uy - y1;
                float t = px * ux0 + py * uy0, p = -px * uy0 + py * ux0;
                return t >= 0f && t <= len && Mathf.Abs(p) <= hw;
            }, Mathf.Min(x1, x2) - hw, Mathf.Min(y1, y2) - hw, Mathf.Max(x1, x2) + hw, Mathf.Max(y1, y2) + hw, col ?? StrokeStyle);
        }

        /// <summary>strokeRect with miter joins.</summary>
        public void StrokeRect(float x, float y, float w, float h, Color? col = null, float? width = null)
        {
            float lw = width ?? LineWidth, hw = lw / 2f;
            Color c = col ?? StrokeStyle;
            FillRect(x - hw, y - hw, w + lw, lw, c);
            FillRect(x - hw, y + h - hw, w + lw, lw, c);
            FillRect(x - hw, y + hw, lw, h - lw, c);
            FillRect(x + w - hw, y + hw, lw, h - lw, c);
        }

        /// <summary>arc(x, y, r, start, end) + stroke, clockwise on screen like the canvas default.</summary>
        public void StrokeArc(float x, float y, float r, float start, float end, Color? col = null, float? width = null)
        {
            float lw = width ?? LineWidth, hw = lw / 2f;
            float sweep = end - start;
            while (sweep < 0f) sweep += Mathf.PI * 2f;
            if (sweep > Mathf.PI * 2f) sweep = Mathf.PI * 2f;
            float outer = r + hw;
            Paint((ux, uy) =>
            {
                float dx = ux - x, dy = uy - y, dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Abs(dist - r) > hw) return false;
                float ang = Mathf.Atan2(dy, dx) - start;
                ang = Mathf.Repeat(ang, Mathf.PI * 2f);
                return ang <= sweep;
            }, x - outer, y - outer, x + outer, y + outer, col ?? StrokeStyle);
        }

        /// <summary>A closed path + fill (nonzero; the prototype's paths are simple polygons).</summary>
        public void FillPolygon(IList<Vector2> pts, Color? col = null)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            var arr = new List<Vector2>(pts);
            Paint((ux, uy) =>
            {
                bool inside = false;
                for (int i = 0, j = arr.Count - 1; i < arr.Count; j = i++)
                {
                    Vector2 pi = arr[i], pj = arr[j];
                    if ((pi.y > uy) != (pj.y > uy) && ux < (pj.x - pi.x) * (uy - pi.y) / (pj.y - pi.y) + pi.x) inside = !inside;
                }
                return inside;
            }, x0, y0, x1, y1, col ?? FillStyle);
        }

        /// <summary>Paints every pixel with a colour chosen per pixel centre (gradients).</summary>
        public void FillShader(Func<float, float, Color> colorAt)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                Blend(x, y, colorAt(x + 0.5f, y + 0.5f), 1f);
        }

        // ---- core ----

        private void Paint(Func<float, float, bool> inside, float ux0, float uy0, float ux1, float uy1, Color col)
        {
            // Device-space bounds of the user-space box.
            float dx0 = float.MaxValue, dy0 = float.MaxValue, dx1 = float.MinValue, dy1 = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                float ux = (k & 1) == 0 ? ux0 : ux1, uy = (k & 2) == 0 ? uy0 : uy1;
                float px = _a * ux + _c * uy + _e, py = _b * ux + _d * uy + _f;
                dx0 = Mathf.Min(dx0, px); dy0 = Mathf.Min(dy0, py); dx1 = Mathf.Max(dx1, px); dy1 = Mathf.Max(dy1, py);
            }
            int xa = Mathf.Max(0, Mathf.FloorToInt(dx0) - 1), xb = Mathf.Min(Width - 1, Mathf.CeilToInt(dx1) + 1);
            int ya = Mathf.Max(0, Mathf.FloorToInt(dy0) - 1), yb = Mathf.Min(Height - 1, Mathf.CeilToInt(dy1) + 1);
            float det = _a * _d - _b * _c;
            if (Mathf.Abs(det) < 1e-9f) return;
            float ia = _d / det, ib = -_b / det, ic = -_c / det, id = _a / det;
            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float px = x + (sx + 0.5f) / 4f - _e, py = y + (sy + 0.5f) / 4f - _f;
                    if (inside(ia * px + ic * py, ib * px + id * py)) hits++;
                }
                if (hits > 0) Blend(x, y, col, hits / 16f);
            }
        }

        private void Blend(int x, int y, Color src, float coverage)
        {
            int i = y * Width + x;
            Color dst = _px[i];
            float sa = src.a * coverage;
            float oa = sa + dst.a * (1f - sa);
            if (oa <= 0f) { _px[i] = new Color(0, 0, 0, 0); return; }
            float k = dst.a * (1f - sa);
            _px[i] = new Color((src.r * sa + dst.r * k) / oa, (src.g * sa + dst.g * k) / oa, (src.b * sa + dst.b * k) / oa, oa);
        }

        /// <summary>Uploads the canvas. sRGB for colour maps; linear for textures three.js left in linear encoding.</summary>
        public Texture2D ToTexture(bool srgb, bool repeat, bool mipmaps, string name, bool readable = false)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, mipmaps, !srgb) { name = name };
            var data = new Color32[_px.Length];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                data[(Height - 1 - y) * Width + x] = _px[y * Width + x];
            tex.SetPixels32(data);
            tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(mipmaps, !readable);
            return tex;
        }

        /// <summary>Parses "#RRGGBB" or "rgba(r,g,b,a)" into a display-space colour.</summary>
        public static Color Css(string s)
        {
            if (s.StartsWith("rgba", StringComparison.Ordinal))
            {
                var parts = s.Substring(5, s.Length - 6).Split(',');
                float P(int i) => float.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
                return new Color(P(0) / 255f, P(1) / 255f, P(2) / 255f, P(3));
            }
            ColorUtility.TryParseHtmlString(s, out var c);
            return c;
        }
    }
}
