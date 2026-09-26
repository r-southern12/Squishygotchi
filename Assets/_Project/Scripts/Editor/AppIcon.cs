using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>
    /// The approved Squishiotchi icon (supplied pack in Art/Icon/Pack: violet glitter dumpling popping out of a
    /// bamboo steamer on yellow; used as supplied, never redrawn). The square art is the default (iOS and
    /// fallback) icon. Android's adaptive icon gets a plain yellow background layer and the round art, shrunk into
    /// the launcher's safe zone, as the foreground, so circle, squircle and rounded-square masks never crop the
    /// lid or steamer. Menu: Squishy > Apply App Icon (also run by BuildAndroid).
    /// </summary>
    public static class AppIcon
    {
        private const string Dir = "Assets/_Project/Art/Icon", Pack = Dir + "/Pack";
        private const int Layer = 432;          // adaptive icon layer size (108 dp at xxxhdpi)
        private const float SafeFraction = .64f; // the round art fits inside the 66 dp safe circle

        [MenuItem("Squishy/Apply App Icon")]
        public static void Make()
        {
            var square = Import(Pack + "/squishiotchi-square-1024.png");
            var round = Readable(Pack + "/squishiotchi-round-1024.png");

            // Background: the artwork's own yellow, sampled from the square's corner.
            var sq = Readable(Pack + "/squishiotchi-square-1024.png");
            Color yellow = sq.GetPixel(4, 4);
            var bg = new Texture2D(Layer, Layer, TextureFormat.RGBA32, false);
            var fill = new Color[Layer * Layer];
            for (int i = 0; i < fill.Length; i++) fill[i] = yellow;
            bg.SetPixels(fill);
            File.WriteAllBytes(Dir + "/icon_bg.png", bg.EncodeToPNG());

            // Foreground: the round art, downsampled (never upscaled) into the safe zone.
            var fg = new Texture2D(Layer, Layer, TextureFormat.RGBA32, false);
            var px = new Color[Layer * Layer];
            float size = Layer * SafeFraction, off = (Layer - size) / 2;
            for (int y = 0; y < Layer; y++)
            for (int x = 0; x < Layer; x++)
            {
                float u = (x + .5f - off) / size, v = (y + .5f - off) / size;
                px[y * Layer + x] = u < 0 || v < 0 || u > 1 || v > 1 ? Color.clear : round.GetPixelBilinear(u, v);
            }
            fg.SetPixels(px);
            File.WriteAllBytes(Dir + "/icon_fg.png", fg.EncodeToPNG());
            AssetDatabase.Refresh();

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { square }, IconKind.Any);
            var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            var bgT = Import(Dir + "/icon_bg.png");
            var fgT = Import(Dir + "/icon_fg.png");
            foreach (var i in adaptive) i.SetTextures(bgT, fgT);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, adaptive);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The PNG as a CPU-readable texture (for sampling), straight from disk.</summary>
        private static Texture2D Readable(string path)
        {
            var t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        private static Texture2D Import(string path)
        {
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
