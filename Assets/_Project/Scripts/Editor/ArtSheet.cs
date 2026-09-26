using System.IO;
using Squishy.Runtime.Game;
using Squishy.Simulation.Game;
using UnityEditor;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>Check sheet of the painted squishy portraits: every finish (happy), then Pinky in each mood and as a baby.</summary>
    public static class ArtSheet
    {
        [MenuItem("Squishy/Squishy Art Sheet")]
        public static void Make()
        {
            var c = JsonUtility.FromJson<GameContent>(File.ReadAllText("Assets/_Project/Resources/Content/game_content.json"));
            c.Init();
            const int T = 200, Cols = 6;
            int n = c.finishes.Length + 4, rows = (n + Cols - 1) / Cols;
            var sheet = new Texture2D(T * Cols, T * rows, TextureFormat.RGBA32, false);
            var bg = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(247, 240, 228, 255);
            sheet.SetPixels32(bg);
            for (int i = 0; i < n; i++)
            {
                var f = i < c.finishes.Length ? c.finishes[i] : c.finishes[8];
                var mood = i < c.finishes.Length ? SquishyArt.Mood.Happy : (SquishyArt.Mood)((i - c.finishes.Length) % 3);
                var t = SquishyArt.Paint(f, mood, i == n - 1, T);
                var px = t.GetPixels();
                int ox = (i % Cols) * T, oy = (rows - 1 - i / Cols) * T;
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
            Directory.CreateDirectory("Library/IconChecks");
            File.WriteAllBytes("Library/IconChecks/art.png", sheet.EncodeToPNG());
        }
    }
}
