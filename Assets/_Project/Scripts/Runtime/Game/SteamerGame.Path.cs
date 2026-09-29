using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Finding the way round the room (user feedback, 29 Sep 2026: it clipped through a lot of the furniture): a grid
    /// over the floor. Tall pieces are walked round; low ones (tables, stools, cushions, beds, beanbags) can be hopped
    /// onto and across when that's the better way, and they bounce like a tap when it lands on them.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private const float NavCell = .06f;
        private const sbyte Floor = 0, Top = 1, Edge = 2, Wall = 3; // Edge: beside a low piece, only crossed by a jump
        private int navN;
        private float navR, navPetR = -1;
        private bool navDirty = true;
        private sbyte[] navKind;
        private Item[] navOn, navBy;
        private float[] navTop;

        /// <summary>How high it stands on top of a low piece (0: not one to hop onto).</summary>
        private float HopTop(Item it)
        {
            if (it == null) return 0;
            var st = C.Style(it.style);
            return st != null && st.low ? it.a.hopLow : it.a.hop;
        }

        private void BuildNav()
        {
            float pr = PetRadius();
            navR = FLOOR_R;
            navPetR = pr;
            navDirty = false;
            navN = Mathf.CeilToInt(2 * navR / NavCell) + 1;
            int n = navN * navN;
            navKind = new sbyte[n];
            navOn = new Item[n];
            navBy = new Item[n];
            navTop = new float[n];
            float lim = FLOOR_R - .12f;
            for (int j = 0; j < navN; j++)
            for (int i = 0; i < navN; i++)
            {
                int c = j * navN + i;
                float x = -navR + i * NavCell, z = -navR + j * NavCell;
                if (x * x + z * z > lim * lim) { navKind[c] = Wall; continue; }
                sbyte kind = Floor;
                Item on = null, by = null;
                float top = 0;
                foreach (var o in obstacles)
                {
                    float d = Dist(x - o.x, z - o.z), h = HopTop(o.it);
                    if (h > 0)
                    {
                        if (d < o.r + .02f) { if (kind != Top || h > top) { kind = Top; on = o.it; top = h; } }
                        else if (d < o.r + pr + .02f && kind == Floor) { kind = Edge; on = o.it; }
                    }
                    else if (d < o.r + pr + .03f) { kind = Wall; by = o.it; break; }
                }
                navKind[c] = kind;
                navOn[c] = on;
                navBy[c] = by;
                navTop[c] = top;
            }
        }

        private int NavCellAt(float x, float z)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt((x + navR) / NavCell), 0, navN - 1), j = Mathf.Clamp(Mathf.RoundToInt((z + navR) / NavCell), 0, navN - 1);
            return j * navN + i;
        }

        private Vector2 NavPos(int c) { return new Vector2(-navR + (c % navN) * NavCell, -navR + (c / navN) * NavCell); }

        /// <summary>A cell's kind for this walk: the piece it's heading to doesn't block its own approach.</summary>
        private sbyte NavKindFor(int c, Item skip)
        {
            var k = navKind[c];
            if (skip == null) return k;
            if (k == Wall && navBy[c] == skip) return Floor;
            if ((k == Top || k == Edge) && navOn[c] == skip) return Floor;
            return k;
        }

        /// <summary>
        /// The way from here to there: points to walk through (on the floor), and jumps onto and off low pieces
        /// where crossing them beats going round. The goal itself isn't included.
        /// </summary>
        private List<PathPt> NavWay(float sx, float sz, float gx, float gz, Item skip)
        {
            var way = new List<PathPt>();
            if (navDirty || navKind == null || Mathf.Abs(navPetR - PetRadius()) > .005f || Mathf.Abs(navR - FLOOR_R) > .001f) BuildNav();
            int start = NavCellAt(sx, sz), goal = NavCellAt(gx, gz), n = navN * navN;
            if (start == goal) return way;
            var cost = new float[n];
            var came = new int[n];
            for (int k = 0; k < n; k++) { cost[k] = float.MaxValue; came[k] = -1; }
            cost[start] = 0;
            var heap = new NavHeap();
            var gp = NavPos(goal);
            heap.Push(start, Vector2.Distance(NavPos(start), gp));
            int best = start;
            float bestH = float.MaxValue, pr = PetRadius();
            // Standing right up against something (in front of the stove, say) it may step out through its own
            // keep-clear ring; it can also step into one to reach a goal inside it. Nowhere else.
            var escaping = new bool[n];
            escaping[start] = NavKindFor(start, skip) == Wall;
            int[] di = { 1, -1, 0, 0, 1, 1, -1, -1 }, dj = { 0, 0, 1, -1, 1, -1, 1, -1 };
            int guard = 0;
            while (heap.Count > 0 && guard++ < 20000)
            {
                int c = heap.Pop();
                if (c == goal) { best = goal; break; }
                var cp = NavPos(c);
                float h = Vector2.Distance(cp, gp);
                if (h < bestH) { bestH = h; best = c; }
                int ci = c % navN, cj = c / navN;
                for (int q = 0; q < 8; q++)
                {
                    int ni = ci + di[q], nj = cj + dj[q];
                    if (ni < 0 || nj < 0 || ni >= navN || nj >= navN) continue;
                    int nc = nj * navN + ni;
                    var kind = NavKindFor(nc, skip);
                    bool nearGoal = Vector2.Distance(NavPos(nc), gp) < pr + .15f;
                    if (kind == Wall && nc != goal && !escaping[c] && !nearGoal) continue;
                    // Going over a low piece costs a bit more than going round; brushing its side (the edge ring) a lot more,
                    // so it hops up instead of clipping the edge; squeezing through a keep-clear ring most of all.
                    float mul = kind == Floor ? 1 : kind == Top ? 1 : kind == Edge ? 1.3f : 6; // in its way, it hops up rather than going the long way round
                    float step = (q < 4 ? 1 : 1.4142f) * NavCell * mul;
                    float nd = cost[c] + step;
                    if (nd >= cost[nc]) continue;
                    cost[nc] = nd;
                    came[nc] = c;
                    escaping[nc] = escaping[c] && kind == Wall;
                    heap.Push(nc, nd + Vector2.Distance(NavPos(nc), gp));
                }
            }
            // Back from the goal (or the nearest reachable cell) to the start.
            var cells = new List<int>();
            for (int c = best; c != -1; c = came[c]) cells.Add(c);
            cells.Reverse();
            if (cells.Count < 2) return way;
            // Turn the cells into a few points: straight lines across the floor, a jump up onto a low piece, along it, a jump down.
            int anchor = 0;
            Item onNow = null;
            bool overEdge = false;
            for (int k = 1; k < cells.Count; k++)
            {
                int c = cells[k];
                var kind = NavKindFor(c, skip);
                if (kind == Edge) { overEdge = true; continue; } // jumped over
                if (overEdge && kind == Floor && onNow == null)
                {
                    // Past the corner of a low piece without getting on it: a little hop over the corner, not through it.
                    var pc = NavPos(c);
                    way.Add(new PathPt { x = pc.x, z = pc.y, y = 0, big = true });
                    overEdge = false;
                    anchor = k;
                    continue;
                }
                overEdge = false;
                Item on = kind == Top ? navOn[c] : null;
                var p = NavPos(c);
                if (on != onNow)
                {
                    // Up onto a piece, off it, or from one to the next: a jump that lands here.
                    way.Add(new PathPt { x = p.x, z = p.y, y = on != null ? navTop[c] : 0, big = true, on = on });
                    onNow = on;
                    anchor = k;
                    continue;
                }
                bool last = k == cells.Count - 1;
                if (!last && NavClear(cells[anchor], cells[k + 1], skip, on)) continue; // still a straight line from the last point
                if (last) break; // the goal is added by the caller
                way.Add(new PathPt { x = p.x, z = p.y, y = on != null ? navTop[c] : 0, on = null });
                anchor = k;
            }
            // Ending on top of something (the goal on the floor): jump down to it.
            if (onNow != null) way.Add(new PathPt { x = gp.x, z = gp.y, y = 0, big = true });
            return way;
        }

        /// <summary>Whether a straight line between two cells stays on the same footing (floor, or the top of one piece).</summary>
        private bool NavClear(int a, int b, Item skip, Item on)
        {
            Vector2 pa = NavPos(a), pb = NavPos(b);
            int steps = Mathf.CeilToInt(Vector2.Distance(pa, pb) / (NavCell * .5f));
            for (int s = 1; s < steps; s++)
            {
                var p = Vector2.Lerp(pa, pb, s / (float)steps);
                int c = NavCellAt(p.x, p.y);
                var kind = NavKindFor(c, skip);
                if (on == null ? kind != Floor : kind != Top || navOn[c] != on) return false;
            }
            return true;
        }

        /// <summary>Render-check helper: plans random walks across the room and counts any floor stretch that cuts through a tall piece.</summary>
        public string NavSelfCheck(int count)
        {
            var rnd = new System.Random(5);
            int paths = 0, clips = 0, lowClips = 0, jumps = 0;
            float pr = PetRadius();
            for (int k = 0; k < count; k++)
            {
                float a1 = (float)rnd.NextDouble() * 6.283f, r1 = (float)rnd.NextDouble() * (FLOOR_R - .3f), a2 = (float)rnd.NextDouble() * 6.283f, r2 = (float)rnd.NextDouble() * (FLOOR_R - .3f);
                Vector2 s0 = new Vector2(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1), g0 = new Vector2(Mathf.Cos(a2) * r2, Mathf.Sin(a2) * r2);
                bool inside = false;
                foreach (var o in obstacles) if (Dist(s0.x - o.x, s0.y - o.z) < o.r + pr + .05f || Dist(g0.x - o.x, g0.y - o.z) < o.r + pr + .05f) inside = true;
                if (inside) continue;
                var way = NavWay(s0.x, s0.y, g0.x, g0.y, null);
                paths++;
                var pts = new List<Vector3> { new Vector3(s0.x, 0, s0.y) };
                foreach (var p in way) { pts.Add(new Vector3(p.x, p.y, p.z)); if (p.big) jumps++; }
                pts.Add(new Vector3(g0.x, 0, g0.y));
                for (int i = 1; i < pts.Count; i++)
                {
                    if (pts[i - 1].y > .01f || pts[i].y > .01f) continue; // on or jumping over a low piece
                    for (int t = 1; t < 20; t++)
                    {
                        var q = Vector3.Lerp(pts[i - 1], pts[i], t / 20f);
                        bool hit = false, low = false;
                        foreach (var o in obstacles) if (Dist(q.x - o.x, q.z - o.z) < o.r + pr * .5f) { if (HopTop(o.it) <= 0) hit = true; else low = true; }
                        if (hit) { clips++; break; }
                        if (low) { lowClips++; break; }
                    }
                }
            }
            int tops = 0, edges = 0, walls = 0;
            if (navKind != null) foreach (var k in navKind) { if (k == Top) tops++; else if (k == Edge) edges++; else if (k == Wall) walls++; }
            var lows = new List<string>();
            foreach (var it in items) if (HopTop(it) > 0) lows.Add(it.arch);
            return "NavCheck [cells top " + tops + " edge " + edges + " wall " + walls + "; low pieces: " + string.Join(",", lows) + "] " + paths + " walks, " + clips + " stretches through tall pieces, " + lowClips + " through low ones, " + jumps + " jumps";
        }

        /// <summary>Chasing the ball: it slides round tall pieces instead of going through them.</summary>
        private void PushOutOfFurniture(Item ignore)
        {
            float pr = PetRadius();
            foreach (var o in obstacles)
            {
                if (o.it == ignore) continue;
                float dx = ai.x - o.x, dz = ai.z - o.z, d = Dist(dx, dz), min = o.r + pr;
                if (d >= min) continue;
                if (d < 1e-4f) { dx = 1; dz = 0; d = 1; }
                ai.x = o.x + dx / d * min;
                ai.z = o.z + dz / d * min;
            }
        }

        /// <summary>A small binary heap of cells by estimated cost.</summary>
        private sealed class NavHeap
        {
            private readonly List<int> _c = new List<int>();
            private readonly List<float> _f = new List<float>();
            public int Count { get { return _c.Count; } }

            public void Push(int c, float f)
            {
                _c.Add(c);
                _f.Add(f);
                int i = _c.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_f[p] <= _f[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = _c[0], last = _c.Count - 1;
                Swap(0, last);
                _c.RemoveAt(last);
                _f.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < _c.Count && _f[l] < _f[m]) m = l;
                    if (r < _c.Count && _f[r] < _f[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
                return top;
            }

            private void Swap(int a, int b)
            {
                int c = _c[a]; _c[a] = _c[b]; _c[b] = c;
                float f = _f[a]; _f[a] = _f[b]; _f[b] = f;
            }
        }
    }
}
