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
        /// The group filling the most slots, each piece used once. The pieces must be together: every one right next
        /// to another in the group (edge to edge within a small gap; on the rug counts), all joined up. They used to
        /// only need to be within 1.1 of one of them, which in a small room was nearly anywhere (user feedback).
        /// </summary>
        public ComboMatch Match(ComboData c, IList<PieceState> items)
        {
            int n = c.slots.Length;
            float gap = c.gap > 0 ? c.gap : R.comboGap;
            var best = new ComboMatch { combo = c, pieces = new PieceState[n] };
            var cands = new List<PieceState>[n];
            for (int j = 0; j < n; j++)
            {
                cands[j] = new List<PieceState>();
                foreach (var p in items) if (Fits(c.slots[j], p.Arch)) cands[j].Add(p);
            }
            Search(0, 0, cands, new PieceState[n], best, gap);
            return best;
        }

        /// <summary>Edge to edge (by footprint radius), these two pieces are within the gap.</summary>
        public bool Touching(PieceState a, PieceState b, float gap)
        {
            var ta = C.Type(a.Arch);
            var tb = C.Type(b.Arch);
            float dx = a.x - b.x, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dz * dz) - (ta != null ? ta.r : 0) - (tb != null ? tb.r : 0) <= gap;
        }

        /// <summary>Whether the chosen pieces form one group, each touching another.</summary>
        private bool Joined(PieceState[] cur, float gap)
        {
            var set = new List<PieceState>();
            foreach (var p in cur) if (p != null) set.Add(p);
            if (set.Count <= 1) return true;
            var reached = new List<PieceState> { set[0] };
            for (int k = 0; k < reached.Count; k++)
                foreach (var p in set)
                    if (!reached.Contains(p) && Touching(reached[k], p, gap)) reached.Add(p);
            return reached.Count == set.Count;
        }

        private void Search(int j, int have, List<PieceState>[] cands, PieceState[] cur, ComboMatch best, float gap)
        {
            int n = cur.Length;
            if (have + (n - j) <= best.have) return; // can't beat the best any more
            if (j == n)
            {
                if (!Joined(cur, gap)) return;
                best.have = have;
                Array.Copy(cur, best.pieces, n);
                return;
            }
            foreach (var p in cands[j])
            {
                if (Array.IndexOf(cur, p) >= 0) continue;
                cur[j] = p;
                Search(j + 1, have + 1, cands, cur, best, gap);
                cur[j] = null;
                if (best.have == n) return;
            }
            Search(j + 1, have, cands, cur, best, gap);
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
