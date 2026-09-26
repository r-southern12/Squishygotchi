using System.IO;
using Squishy.Runtime.Game;
using Squishy.Simulation.Game;
using UnityEditor;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>
    /// Check sheets of the painted squishy portraits (Library/IconChecks): every type (happy adult), and a grid of
    /// every mood x life stage for a couple of types. Menu: Squishy > Squishy Art Sheets.
    /// </summary>
    public static class ArtSheet
    {
        [MenuItem("Squishy/Squishy Art Sheets")]
        public static void Make()
        {
            var c = JsonUtility.FromJson<GameContent>(File.ReadAllText("Assets/_Project/Resources/Content/game_content.json"));
            c.Init();
            Directory.CreateDirectory("Library/IconChecks");

            int n = c.finishes.Length;
            Sheet("Library/IconChecks/art_types.png", 8, (n + 7) / 8, 160, i => i < n ? SquishyArt.Paint(c.finishes[i], SquishyArt.Mood.Happy, GameRules.Life.Adult, 160) : null);

            // Rows: each mood for Pinky, then for Violet Sparkle; columns: baby, young, adult, elder.
            var pinky = c.finishes[8];
            var violet = System.Array.Find(c.finishes, f => f.name == "Violet Sparkle") ?? pinky;
            Sheet("Library/IconChecks/art_states.png", 4, 6, 200, i =>
            {
                var f = i / 12 == 0 ? pinky : violet;
                return SquishyArt.Paint(f, (SquishyArt.Mood)(i / 4 % 3), (GameRules.Life)(i % 4), 200);
            });
        }

        private static void Sheet(string path, int cols, int rows, int T, System.Func<int, Texture2D> cell)
        {
            var sheet = new Texture2D(T * cols, T * rows, TextureFormat.RGBA32, false);
            var bg = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(247, 240, 228, 255);
            sheet.SetPixels32(bg);
            for (int i = 0; i < cols * rows; i++)
            {
                var t = cell(i);
                if (t == null) continue;
                var px = t.GetPixels();
                int ox = (i % cols) * T, oy = (rows - 1 - i / cols) * T;
                for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    var s = px[y * T + x];
                    if (s.a <= 0) continue;
                    var d = sheet.GetPixel(ox + x, oy + y);
                    sheet.SetPixel(ox + x, oy + y, Color.Lerp(d, new Color(s.r, s.g, s.b, 1), s.a));
                }
                Object.DestroyImmediate(t);
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
        }
    }
}
