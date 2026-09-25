using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.UI
{
    /// <summary>Hard or soft CSS box-shadow: offset, blur, spread, colour.</summary>
    public struct Shadow
    {
        public float x, y, blur, spread;
        public Color color;
        public bool inset;

        public Shadow(float x, float y, float blur, float spread, Color color, bool inset = false)
        {
            this.x = x; this.y = y; this.blur = blur; this.spread = spread; this.color = color; this.inset = inset;
        }
    }

    /// <summary>
    /// A rounded box painted with its CSS box-shadows (UI Toolkit has none), then its fill.
    /// Radius -1 means a pill (999px). Children draw on top as usual.
    /// </summary>
    public class Frame : VisualElement
    {
        public Color Fill = Color.clear;
        public float Radius;
        public Vector4? Corners; // top-left, top-right, bottom-right, bottom-left
        public readonly List<Shadow> Shadows = new List<Shadow>();
        public Func<Rect, Painter2D, bool> Paint; // extra painting (stripes, gradients); return true to skip the fill

        public Frame()
        {
            generateVisualContent += Draw;
            pickingMode = PickingMode.Ignore;
        }

        public Frame Set(Color fill, float radius, params Shadow[] shadows)
        {
            Fill = fill;
            Radius = radius;
            Shadows.Clear();
            Shadows.AddRange(shadows);
            MarkDirtyRepaint();
            return this;
        }

        private void Draw(MeshGenerationContext mgc)
        {
            var r = new Rect(0, 0, layout.width, layout.height);
            if (r.width <= 0 || r.height <= 0) return;
            var p = mgc.painter2D;
            foreach (var s in Shadows)
            {
                if (s.inset) continue;
                if (s.blur <= 0) Rr(p, Expand(Offset(r, s.x, s.y), s.spread), s.color);
                else
                {
                    // Soft shadow: stacked translucent boxes from the outer to the inner blur edge.
                    const int layers = 6;
                    var c = s.color;
                    c.a = s.color.a * 1.2f / layers;
                    for (int k = 0; k < layers; k++)
                        Rr(p, Expand(Offset(r, s.x, s.y), s.spread + s.blur / 2 - s.blur * k / layers), c);
                }
            }
            bool skip = Paint != null && Paint(r, p);
            if (!skip && Fill.a > 0) Rr(p, r, Fill);
            foreach (var s in Shadows)
            {
                if (!s.inset) continue;
                // Inset band (the tombstone's darker base): clip by drawing the fill again, shrunk.
                var inner = new Rect(r.x, r.y, r.width, r.height + s.y);
                Rr(p, r, s.color);
                Rr(p, inner, Fill);
            }
        }

        private static Rect Offset(Rect r, float x, float y) { return new Rect(r.x + x, r.y + y, r.width, r.height); }
        private static Rect Expand(Rect r, float e) { return new Rect(r.x - e, r.y - e, r.width + 2 * e, r.height + 2 * e); }

        private void Rr(Painter2D p, Rect r, Color c)
        {
            if (r.width <= 0 || r.height <= 0) return;
            p.fillColor = c;
            float lim = Mathf.Min(r.width, r.height) / 2;
            Vector4 k = Corners ?? Vector4.one * (Radius < 0 ? lim : Radius);
            float grow = (r.width - layout.width) / 2;
            PathRoundRect(p, r, Mathf.Clamp(k.x + grow, 0, lim), Mathf.Clamp(k.y + grow, 0, lim), Mathf.Clamp(k.z + grow, 0, lim), Mathf.Clamp(k.w + grow, 0, lim));
            p.Fill();
        }

        public static void PathRoundRect(Painter2D p, Rect r, float tl, float tr, float br, float bl)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + tl, r.yMin));
            p.LineTo(new Vector2(r.xMax - tr, r.yMin));
            if (tr > 0) p.Arc(new Vector2(r.xMax - tr, r.yMin + tr), tr, Angle.Degrees(270), Angle.Degrees(360));
            p.LineTo(new Vector2(r.xMax, r.yMax - br));
            if (br > 0) p.Arc(new Vector2(r.xMax - br, r.yMax - br), br, Angle.Degrees(0), Angle.Degrees(90));
            p.LineTo(new Vector2(r.xMin + bl, r.yMax));
            if (bl > 0) p.Arc(new Vector2(r.xMin + bl, r.yMax - bl), bl, Angle.Degrees(90), Angle.Degrees(180));
            p.LineTo(new Vector2(r.xMin, r.yMin + tl));
            if (tl > 0) p.Arc(new Vector2(r.xMin + tl, r.yMin + tl), tl, Angle.Degrees(180), Angle.Degrees(270));
            p.ClosePath();
        }
    }

    /// <summary>A small vector drawing (the prototype's inline SVGs), painted with Painter2D in a 24x24 (or custom) viewBox.</summary>
    public class Glyph : VisualElement
    {
        public Action<Painter2D, float> Draw; // painter, scale from viewBox units to pixels
        public float ViewBox = 24;

        public Glyph(float size, Action<Painter2D, float> draw, float viewBox = 24)
        {
            ViewBox = viewBox;
            Draw = draw;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += m => { if (Draw != null) Draw(m.painter2D, layout.width / ViewBox); };
        }
    }

    /// <summary>Keyframe-style animations driven from the game loop (CSS animations and transitions).</summary>
    public sealed class Tweens
    {
        private sealed class T
        {
            public VisualElement el;
            public float t, dur;
            public bool loop, alternate;
            public Action<float> apply;
            public Action done;
        }

        private readonly List<T> _list = new List<T>();

        public void Run(VisualElement el, float dur, Action<float> apply, Action done = null, bool loop = false, bool alternate = false)
        {
            Stop(el, apply == null);
            _list.Add(new T { el = el, dur = Mathf.Max(.0001f, dur), apply = apply, done = done, loop = loop, alternate = alternate });
            apply?.Invoke(0);
        }

        /// <summary>Stops animations on an element (all of them, or only looping ones).</summary>
        public void Stop(VisualElement el, bool all = true)
        {
            _list.RemoveAll(x => x.el == el && (all || x.loop));
        }

        public void Update(float dt)
        {
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                if (i >= _list.Count) continue;
                var x = _list[i];
                x.t += dt;
                float u = x.t / x.dur;
                if (x.loop)
                {
                    float c = u % (x.alternate ? 2 : 1);
                    x.apply(x.alternate && c > 1 ? 2 - c : Mathf.Min(1, c));
                    continue;
                }
                if (u >= 1)
                {
                    _list.RemoveAt(i);
                    x.apply(1);
                    x.done?.Invoke();
                }
                else x.apply(u);
            }
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) easing.</summary>
        public static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float cx = Cub(x1, x2, t) - x, d = CubD(x1, x2, t);
                if (Mathf.Abs(cx) < 1e-5f) break;
                if (Mathf.Abs(d) < 1e-6f) break;
                t = Mathf.Clamp01(t - cx / d);
            }
            return Cub(y1, y2, t);
        }

        private static float Cub(float a, float b, float t) { return 3 * a * (1 - t) * (1 - t) * t + 3 * b * (1 - t) * t * t + t * t * t; }
        private static float CubD(float a, float b, float t) { return 3 * a * (1 - t) * (1 - t) + 6 * (b - a) * (1 - t) * t + 3 * (1 - b) * t * t; }
        public static float EaseOut(float x) { return Bezier(0, 0, .58f, 1, x); }
        public static float EaseInOut(float x) { return Bezier(.42f, 0, .58f, 1, x); }
        public static float Ease(float x) { return Bezier(.25f, .1f, .25f, 1, x); }
    }

    /// <summary>Small style helpers so the layout reads like the prototype's CSS.</summary>
    public static class Css
    {
        private static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();

        public static Color C(string hex)
        {
            if (hex.StartsWith("rgba", StringComparison.Ordinal)) return Three.Canvas2D.Css(hex);
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color C(string hex, float alpha) { var c = C(hex); c.a = alpha; return c; }

        public static Font Font(string face, int weight)
        {
            string key = face + "-" + weight;
            if (!Fonts.TryGetValue(key, out var f)) Fonts[key] = f = Resources.Load<Font>("Fonts/" + key);
            return f;
        }

        public static T Text<T>(this T el, string face, int weight, float size, string color = null) where T : VisualElement
        {
            var f = Font(face, weight);
            if (f != null) el.style.unityFontDefinition = FontDefinition.FromFont(f);
            el.style.fontSize = size;
            el.style.unityFontStyleAndWeight = FontStyle.Normal;
            if (color != null) el.style.color = C(color);
            return el;
        }

        public static T Pad<T>(this T el, float t, float r, float b, float l) where T : VisualElement
        {
            el.style.paddingTop = t; el.style.paddingRight = r; el.style.paddingBottom = b; el.style.paddingLeft = l;
            return el;
        }

        public static T Margin<T>(this T el, float t, float r, float b, float l) where T : VisualElement
        {
            el.style.marginTop = t; el.style.marginRight = r; el.style.marginBottom = b; el.style.marginLeft = l;
            return el;
        }

        public static T Row<T>(this T el, Align align = Align.Center, Justify justify = Justify.FlexStart) where T : VisualElement
        {
            el.style.flexDirection = FlexDirection.Row;
            el.style.alignItems = align;
            el.style.justifyContent = justify;
            return el;
        }

        public static T Col<T>(this T el, Align align = Align.Stretch) where T : VisualElement
        {
            el.style.flexDirection = FlexDirection.Column;
            el.style.alignItems = align;
            return el;
        }

        public static T Abs<T>(this T el, float? left = null, float? top = null, float? right = null, float? bottom = null) where T : VisualElement
        {
            el.style.position = Position.Absolute;
            if (left.HasValue) el.style.left = left.Value;
            if (top.HasValue) el.style.top = top.Value;
            if (right.HasValue) el.style.right = right.Value;
            if (bottom.HasValue) el.style.bottom = bottom.Value;
            return el;
        }

        public static T Size<T>(this T el, float? w, float? h) where T : VisualElement
        {
            if (w.HasValue) el.style.width = w.Value;
            if (h.HasValue) el.style.height = h.Value;
            return el;
        }

        public static T In<T>(this T el, VisualElement parent) where T : VisualElement { parent?.Add(el); return el; }
        public static T NoPick<T>(this T el) where T : VisualElement { el.pickingMode = PickingMode.Ignore; return el; }
        public static T Shown<T>(this T el, bool on) where T : VisualElement { el.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; return el; }
        public static bool IsShown(this VisualElement el) { return el.resolvedStyle.display != DisplayStyle.None && el.style.display != DisplayStyle.None; }

        /// <summary>CSS gap: spacing between children (UI Toolkit has no gap).</summary>
        public static T Gap<T>(this T el, float gap) where T : VisualElement
        {
            bool row = el.style.flexDirection == FlexDirection.Row;
            int i = 0;
            foreach (var c in el.Children())
            {
                if (c.style.position == Position.Absolute) continue;
                if (row) c.style.marginLeft = i == 0 ? 0 : gap; else c.style.marginTop = i == 0 ? 0 : gap;
                i++;
            }
            return el;
        }

        public static Label Label(VisualElement parent, string text, string face = "Figtree", int weight = 400, float size = 15, string color = "#33261D")
        {
            var l = new Label(text).Text(face, weight, size, color);
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.pickingMode = PickingMode.Ignore;
            parent?.Add(l);
            return l;
        }

        public static void Wrap(this Label l) { l.style.whiteSpace = WhiteSpace.Normal; }

        public static TextShadow Shadow(float y, string color, float x = 0, float blur = 0)
        {
            return new TextShadow { offset = new Vector2(x, y), blurRadius = blur, color = C(color) };
        }
    }
}
