using System.Collections.Generic;
using System.Linq;
using Squishy.Runtime.Three;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Arranging with combos in mind (user request, 29 Sep 2026): a slide docks onto a bathtub (Splash slide), and
    /// any move that makes, grows or breaks a combo says so on the spot.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private const float SlideExit = .33f, SlideOverRim = .06f; // where the slide's ramp ends (local +z); how far it reaches over the rim
        private Dictionary<string, int> arrangeHave;

        /// <summary>A tub's half length and half depth, from its footprint circles.</summary>
        private static void TubHalf(Item tub, out float hx, out float hz)
        {
            hx = tub.a.r;
            hz = tub.a.r;
            var c = tub.a.circles;
            if (c == null || c.Length < 3) return;
            hx = hz = 0;
            for (int k = 0; k + 2 < c.Length; k += 3) { hx = Mathf.Max(hx, Mathf.Abs(c[k]) + c[k + 2]); hz = Mathf.Max(hz, Mathf.Abs(c[k + 1]) + c[k + 2]); }
        }

        /// <summary>
        /// A slide brought near a bathtub docks onto it: square to the nearest side, facing in, the end of its ramp
        /// just over the rim, so it slides straight into the bath.
        /// </summary>
        private bool SnapSlideToTub(Item it, ref float x, ref float z)
        {
            foreach (var tub in items)
            {
                if (tub.arch != "tub") continue;
                float ux = Mathf.Cos(tub.ry), uz = -Mathf.Sin(tub.ry), nx = Mathf.Sin(tub.ry), nz = Mathf.Cos(tub.ry);
                float dx = x - tub.tx, dz = z - tub.tz, lx = dx * ux + dz * uz, lz = dx * nx + dz * nz;
                TubHalf(tub, out float hx, out float hz);
                if (Mathf.Abs(lx) > hx + .6f || Mathf.Abs(lz) > hz + .6f) continue;
                bool ends = Mathf.Abs(lx) / hx > Mathf.Abs(lz) / hz;
                float ox = ends ? Mathf.Sign(lx) : 0, oz = ends ? 0 : Mathf.Sign(lz); // outward from the side it docks to
                float reach = SlideExit - SlideOverRim;
                float cx = ends ? ox * (hx + reach) : Mathf.Clamp(lx, -hx + .14f, hx - .14f), cz = ends ? 0 : oz * (hz + reach);
                x = tub.tx + ux * cx + nx * cz;
                z = tub.tz + uz * cx + nz * cz;
                float fx = -(ux * ox + nx * oz), fz = -(uz * ox + nz * oz);
                it.ry = Mathf.Atan2(fx, fz);
                it.atWall = false;
                if (!it.docked)
                {
                    it.docked = true;
                    sfx.Snap();
                    Buzz(10);
                    Glints(new Vector3(x + fx * SlideExit, Y0 + .25f, z + fz * SlideExit), "#DDF1F7", 4);
                }
                return true;
            }
            it.docked = false;
            return false;
        }

        /// <summary>The tub this slide is docked onto (the end of its ramp is over the tub), if any.</summary>
        private Item DockedTub(Item slide)
        {
            if (slide == null || slide.arch != "slide") return null;
            float ex = slide.tx + Mathf.Sin(slide.ry) * SlideExit, ez = slide.tz + Mathf.Cos(slide.ry) * SlideExit;
            foreach (var tub in items)
            {
                if (tub.arch != "tub") continue;
                float ux = Mathf.Cos(tub.ry), uz = -Mathf.Sin(tub.ry), nx = Mathf.Sin(tub.ry), nz = Mathf.Cos(tub.ry);
                float dx = ex - tub.tx, dz = ez - tub.tz;
                TubHalf(tub, out float hx, out float hz);
                if (Mathf.Abs(dx * ux + dz * uz) <= hx + .02f && Mathf.Abs(dx * nx + dz * nz) <= hz + .02f) return tub;
            }
            return null;
        }

        /// <summary>A docked slide and its tub overlap on purpose: Settle leaves them be.</summary>
        private bool DockedPair(Item a, Item b)
        {
            return (a.arch == "slide" && b.arch == "tub" && DockedTub(a) == b) || (b.arch == "slide" && a.arch == "tub" && DockedTub(b) == a);
        }

        /// <summary>Sliding into the bath: the slide is docked and this is the Splash slide.</summary>
        private bool SplashDocked(Item slide)
        {
            return ai.act != null && ai.act.combo != null && ai.act.combo.combo.then == "splash" && DockedTub(slide) != null;
        }

        /// <summary>
        /// Zoomed in close, furniture between the camera and the squishy is hidden so it never blocks the view
        /// (user feedback); it comes back once it's out of the way or you zoom out.
        /// </summary>
        private void StepOccluders(float dt)
        {
            bool close = mode == "home" && CloseUp && cam != null && pet != null;
            Vector3 from = close ? ThreeWorld(cam.transform) : Vector3.zero, to = Vector3.zero;
            if (close) { var pp = PetWorld(); to = new Vector3(pp.x, pp.y + pet.Scale * pet.StageScale * .45f, pp.z); }
            foreach (var it in items)
            {
                bool block = close && it.a.cat != "Floor" && Occludes(it, from, to);
                it.hideT = block ? .35f : Mathf.Max(0, it.hideT - dt);
                bool hide = it.hideT > 0;
                if (hide == it.hidden) continue;
                it.hidden = hide;
                foreach (var r in it.g.GetComponentsInChildren<Renderer>(true)) r.enabled = !hide;
            }
        }

        /// <summary>Whether this piece stands in the line of sight to the squishy.</summary>
        private bool Occludes(Item it, Vector3 from, Vector3 to)
        {
            if (it.h <= 0) { var bb = Node.LocalBounds(it.g, room); it.h = Mathf.Max(.05f, bb.max.y - Y0); }
            Vector2 a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z), c = new Vector2(it.tx, it.tz);
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-6f) return false;
            float t = Mathf.Clamp01(Vector2.Dot(c - a, ab) / l2);
            if (t > .96f) return false; // under or behind the squishy
            float d = Vector2.Distance(a + ab * t, c), y = Mathf.Lerp(from.y, to.y, t);
            return d < it.a.r + .08f && y < Y0 + it.h + .02f;
        }

        private Dictionary<string, int> ComboSnapshot()
        {
            var d = new Dictionary<string, int>();
            foreach (var m in Rules.Combos(S.items)) d[m.combo.id] = m.have;
            return d;
        }

        /// <summary>
        /// After a change while arranging: a combo that grew or was finished says so on the spot. Its pieces bounce and
        /// sparkle, a note names it with its count ("Tea time 2/3", or "Tea time!" when done, with a chime), and one
        /// that was broken says so too. A lone piece isn't news.
        /// </summary>
        private void ComboFeedback(Item at)
        {
            var now = Rules.Combos(S.items);
            var was = arrangeHave ?? now.ToDictionary(m => m.combo.id, m => m.have);
            arrangeHave = now.ToDictionary(m => m.combo.id, m => m.have);
            var lines = new List<string>();
            bool done = false, grew = false;
            foreach (var m in now)
            {
                was.TryGetValue(m.combo.id, out int before);
                if (m.have == before) continue;
                if (m.have > before && m.have >= 2)
                {
                    grew = true;
                    if (m.Done) { done = true; lines.Insert(0, m.combo.name + "!"); }
                    else lines.Add(m.combo.name + " " + m.have + "/" + m.pieces.Length);
                    foreach (var p in m.pieces)
                    {
                        var i = ItemOf(p);
                        if (i == null) continue;
                        i.bv = m.Done ? -8 : -4;
                        Glints(new Vector3(i.tx, Y0 + .35f, i.tz), m.Done ? "#FFE08A" : "#FFF3D6", m.Done ? 5 : 2);
                    }
                }
                else if (m.have < before && before >= 2) lines.Add(m.combo.name + " " + m.have + "/" + m.pieces.Length);
            }
            if (lines.Count == 0) return;
            string text = lines[0]; // one short line (two joined were too long to read)
            if (at != null) FloaterAt(at, text, "combo");
            else Floater(text, "combo");
            RefreshComboRings();
            if (trayTab == "combos" || done) DrawTray();
            if (done) sfx.Chime();
            else if (grew) sfx.Note(4);
            Buzz(done ? 25 : 8);
        }
    }
}
