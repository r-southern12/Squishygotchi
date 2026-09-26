using System.Collections.Generic;
using System.IO;
using Squishy.Runtime.Three;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Android;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Paints the app icon in code: a round, glossy dumpling with a pleated swirl peeking out of a bamboo
    /// steamer, puffs of steam, on a warm mustard-to-peach ground. Writes a full icon (legacy, round, iOS)
    /// and Android adaptive layers, then assigns them. Menu: Squishy > Make App Icon (also run by BuildAndroid).
    /// </summary>
    public static class AppIcon
    {
        private const string Dir = "Assets/_Project/Art/Icon";
        public static int Variant = 0; // which painted design (AppIconPaint) is the app icon

        [MenuItem("Squishy/Make App Icon")]
        public static void Make()
        {
            Directory.CreateDirectory(Dir);
            Write("icon_full.png", AppIconPaint.Png(AppIconPaint.Draw(1024, Variant, true, true, 1f)));
            Write("icon_bg.png", AppIconPaint.Png(AppIconPaint.Draw(432, Variant, true, false, 1f)));
            Write("icon_fg.png", AppIconPaint.Png(AppIconPaint.Draw(432, Variant, false, true, .64f))); // inside the adaptive safe zone
            AssetDatabase.Refresh();
            var full = Load("icon_full.png");
            var bg = Load("icon_bg.png");
            var fg = Load("icon_fg.png");

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
            var android = NamedBuildTarget.Android;
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var i in adaptive) i.SetTextures(bg, fg);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            AssetDatabase.SaveAssets();
        }

        private static void Write(string name, byte[] png) { File.WriteAllBytes(Path.Combine(Dir, name), png); }

        private static Texture2D Load(string name)
        {
            string path = Dir + "/" + name;
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Draws in a 0..1 unit square (y down), scaled about the centre by <paramref name="k"/>.</summary>
        private static byte[] Paint(int size, bool ground, bool figure, float k)
        {
            var g = new Canvas2D(size, size);
            float S = size;
            System.Func<float, float> X = u => (.5f + (u - .5f) * k) * S, Y = X, R = u => u * k * S;
            Color Hex(string h, float a = 1) { var c = Canvas2D.Css(h); c.a = a; return c; }

            if (ground)
            {
                Color top = Hex("#F6D9A0"), bottom = Hex("#E9A27A");
                g.FillShader((x, y) =>
                {
                    float v = y / S, dx = x / S - .5f, dy = y / S - .38f;
                    var c = Color.Lerp(top, bottom, Mathf.SmoothStep(0, 1, v));
                    float glow = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) / .5f);
                    return Color.Lerp(c, Hex("#FFF3DA"), glow * glow * .55f);
                });
            }
            if (!figure) return Encode(g);

            // Steam.
            foreach (var p in new[] { new Vector3(.24f, .2f, .05f), new Vector3(.31f, .13f, .035f), new Vector3(.77f, .17f, .045f), new Vector3(.71f, .1f, .03f) })
                g.FillCircle(X(p.x), Y(p.y), R(p.z), Hex("#FFFFFF", .75f));

            // Back rim of the steamer (the opening).
            g.FillEllipse(X(.5f), Y(.6f), R(.4f), R(.085f), 0, Hex("#B88350"));
            g.FillEllipse(X(.5f), Y(.605f), R(.37f), R(.07f), 0, Hex("#6E4A2A"));

            // The dumpling: soft dome, subtle base shade, gloss.
            g.FillEllipse(X(.5f), Y(.56f), R(.3f), R(.25f), 0, Hex("#EFE2CC"));
            g.FillEllipse(X(.5f), Y(.545f), R(.29f), R(.235f), 0, Hex("#FFF8EE"));
            g.FillEllipse(X(.4f), Y(.43f), R(.08f), R(.045f), -.5f, Hex("#FFFFFF", .9f));
            // Pleats gathering to a swirl on top.
            var pleat = Hex("#E3D0B2");
            for (int i = -2; i <= 2; i++)
                g.Line(X(.5f + i * .045f), Y(.39f + Mathf.Abs(i) * .012f), X(.5f + i * .012f), Y(.33f), pleat, R(.012f));
            g.FillCircle(X(.5f), Y(.315f), R(.035f), Hex("#FFF8EE"));
            g.StrokeArc(X(.505f), Y(.31f), R(.022f), Mathf.PI * .9f, Mathf.PI * 2.6f, pleat, R(.011f));
            g.StrokeArc(X(.52f), Y(.29f), R(.028f), Mathf.PI * 1.1f, Mathf.PI * 1.75f, pleat, R(.011f));

            // Face: big glossy eyes, rosy cheeks, a tiny smile.
            var ink = Hex("#33261D");
            foreach (float ex in new[] { .41f, .59f })
            {
                g.FillEllipse(X(ex), Y(.52f), R(.033f), R(.042f), 0, ink);
                g.FillCircle(X(ex + .011f), Y(.506f), R(.012f), Hex("#FFFFFF"));
                g.FillCircle(X(ex - .009f), Y(.535f), R(.006f), Hex("#FFFFFF", .8f));
            }
            g.FillEllipse(X(.35f), Y(.575f), R(.042f), R(.024f), 0, Hex("#F2A0A0", .75f));
            g.FillEllipse(X(.65f), Y(.575f), R(.042f), R(.024f), 0, Hex("#F2A0A0", .75f));
            g.StrokeArc(X(.5f), Y(.55f), R(.03f), Mathf.PI * .2f, Mathf.PI * .8f, ink, R(.012f));

            // Front of the steamer: a woven band hiding the dumpling's bottom.
            var band = new List<Vector2>();
            for (int i = 0; i <= 24; i++) { float a = Mathf.PI * i / 24; band.Add(new Vector2(X(.5f - .4f * Mathf.Cos(a)), Y(.64f + .085f * Mathf.Sin(a)))); }
            for (int i = 24; i >= 0; i--) { float a = Mathf.PI * i / 24; band.Add(new Vector2(X(.5f - .4f * Mathf.Cos(a)), Y(.86f + .085f * Mathf.Sin(a)))); }
            g.FillPolygon(band, Hex("#D6A468"));
            for (int s = 1; s <= 3; s++)
            {
                float y0 = .64f + s * .055f;
                for (int i = 1; i < 24; i++)
                {
                    float a0 = Mathf.PI * (i - 1) / 24, a1 = Mathf.PI * i / 24;
                    g.Line(X(.5f - .4f * Mathf.Cos(a0)), Y(y0 + .085f * Mathf.Sin(a0)), X(.5f - .4f * Mathf.Cos(a1)), Y(y0 + .085f * Mathf.Sin(a1)), Hex("#B88350", .7f), R(.008f));
                }
            }
            // Rim lip catching the light.
            var lip = new List<Vector2>();
            for (int i = 0; i <= 24; i++) { float a = Mathf.PI * i / 24; lip.Add(new Vector2(X(.5f - .41f * Mathf.Cos(a)), Y(.6f + .09f * Mathf.Sin(a)))); }
            for (int i = 24; i >= 0; i--) { float a = Mathf.PI * i / 24; lip.Add(new Vector2(X(.5f - .41f * Mathf.Cos(a)), Y(.655f + .09f * Mathf.Sin(a)))); }
            g.FillPolygon(lip, Hex("#E9C48C"));
            return Encode(g);
        }

        private static byte[] Encode(Canvas2D g)
        {
            var t = g.ToTexture(true, false, false, "icon", true);
            var png = t.EncodeToPNG();
            Object.DestroyImmediate(t);
            return png;
        }
    }
}
