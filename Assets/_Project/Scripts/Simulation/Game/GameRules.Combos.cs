using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Game
{
    /// <summary>
    /// Furniture combos (user design, 29 Sep 2026): pieces placed near one another make a set that the squishy uses
    /// for a richer activity (tea for two, a quiet corner...), in place of the single piece's own use. A piece can
    /// count towards several combos at once. The Comfort panel shows progress: the pieces in place so far, the rest a mystery.
    /// </summary>
    public sealed partial class GameRules
    {
        public sealed class ComboMatch
        {
            public ComboData combo;
            public PieceState[] pieces; // one per slot; null where it's still missing
            public int have;
            public bool Done { get { return have == pieces.Length; } }
        }

        /// <summary>Whether a piece type fills a slot ("a|b" means either).</summary>
        public static bool Fits(string slot, string arch)
        {
            if (string.IsNullOrEmpty(slot)) return false;
            return Array.IndexOf(slot.Split('|'), arch) >= 0;
        }

        /// <summary>The best group of pieces for every combo.</summary>
        public List<ComboMatch> Combos(IList<PieceState> items)
        {
            var list = new List<ComboMatch>();
            if (C.combos == null) return list;
            foreach (var c in C.combos) list.Add(Match(c, items));
            return list;
        }

        /// <summary>
        /// The group filling the most slots: every piece in it within reach of one of them (the anchor), each piece
        /// used once per combo.
        /// </summary>
        public ComboMatch Match(ComboData c, IList<PieceState> items)
        {
            int n = c.slots.Length;
            float near = c.near > 0 ? c.near : R.comboNear;
            var best = new ComboMatch { combo = c, pieces = new PieceState[n] };
            var cur = new PieceState[n];
            var cands = new List<PieceState>[n];
            foreach (var anchor in items)
            {
                bool fits = false;
                for (int j = 0; j < n; j++) fits |= Fits(c.slots[j], anchor.Arch);
                if (!fits) continue;
                for (int j = 0; j < n; j++)
                {
                    cands[j] = new List<PieceState>();
                    foreach (var p in items) if (Fits(c.slots[j], p.Arch) && Near(anchor, p, near)) cands[j].Add(p);
                }
                Search(0, 0, cands, cur, best, anchor);
                if (best.have == n) break;
            }
            return best;
        }

        private static bool Near(PieceState a, PieceState b, float d)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz <= d * d;
        }

        private static void Search(int j, int have, List<PieceState>[] cands, PieceState[] cur, ComboMatch best, PieceState anchor)
        {
            int n = cur.Length;
            if (have + (n - j) <= best.have) return; // can't beat the best any more
            if (j == n)
            {
                if (Array.IndexOf(cur, anchor) < 0) return; // a group round this anchor must include it
                best.have = have;
                Array.Copy(cur, best.pieces, n);
                return;
            }
            foreach (var p in cands[j])
            {
                if (Array.IndexOf(cur, p) >= 0) continue;
                cur[j] = p;
                Search(j + 1, have + 1, cands, cur, best, anchor);
                cur[j] = null;
            }
            Search(j + 1, have, cands, cur, best, anchor);
        }

        /// <summary>Combos completed for the first time ever (each is celebrated once).</summary>
        public List<ComboData> NewlyFound(List<ComboMatch> matches)
        {
            var list = new List<ComboData>();
            foreach (var m in matches)
            {
                if (!m.Done || S.combosFound.Contains(m.combo.id)) continue;
                S.combosFound.Add(m.combo.id);
                list.Add(m.combo);
            }
            return list;
        }

        /// <summary>After a quiet moment, needs drain slower for a while.</summary>
        public void StartCalm(float minutes)
        {
            S.calmUntil = Math.Max(S.calmUntil, Clock.UtcNow.Ticks + TimeSpan.FromMinutes(minutes).Ticks);
        }
    }
}
