using System.Collections.Generic;
using System.Linq;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;
using static Squishy.Runtime.Game.Ease;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Furniture combos in the room (see GameRules.Combos): a finished combo takes precedence over its piece's own
    /// use, some carry on to a second piece (shower then shake dry on the rug), and two change cooking at the stove.
    /// The Comfort panel lists them with the pieces found so far.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private List<GameRules.ComboMatch> combos = new List<GameRules.ComboMatch>();
        private readonly List<Transform> comboProps = new List<Transform>();
        private bool comboDressed, combosPrimed;
        private int concertNote;
        private static readonly int[] ConcertTune = { 0, 2, 4, 2, 0, 2, 4, 4, 5, 4, 2, 3, 1, 0 };

        private void ComputeCombos()
        {
            combos = Rules.Combos(S.items);
            if (visiting || mode == "unbox") return;
            var found = Rules.NewlyFound(combos);
            if (!combosPrimed)
            {
                // Already in the room when the game opens: one gentle note rather than a stack of cards.
                combosPrimed = true;
                if (found.Count > 0) { string msg = found.Count == 1 ? found[0].name + " combo!" : found.Count + " combos found!"; Later(4, () => Floater(msg)); }
                return;
            }
            foreach (var c in found)
            {
                var m = combos.Find(x => x.combo == c);
                var lead = m != null ? LeadOf(m) : null;
                var cc = c;
                Later(.5f, () => { ui.Celebrate(lead != null ? lead.st.key : null, "New combo!", 0, cc.name, cc.text); Fanfare(); });
                TaskEvent("combo");
            }
        }

        private Item ItemOf(PieceState p) { return p == null ? null : items.Find(i => i.st == p); }
        private static bool IsLead(GameRules.ComboMatch m, Item it) { return GameRules.Fits(m.combo.lead, it.arch); }

        private Item LeadOf(GameRules.ComboMatch m)
        {
            foreach (var p in m.pieces) if (p != null && GameRules.Fits(m.combo.lead, p.Arch)) return ItemOf(p);
            return null;
        }

        /// <summary>
        /// The finished combo tapping this piece starts: any piece of a set starts it (the tub or the slide of a Splash
        /// slide), except a plant or lamp, which keep their own jobs (watering, lights), and walls. If it's in several
        /// finished sets, any one of them.
        /// </summary>
        private GameRules.ComboMatch ComboAt(Item it)
        {
            if (visiting || it == null || it.a.role == "plant" || it.a.role == "lamp" || it.a.cat == "Wall") return null;
            GameRules.ComboMatch pick = null;
            int n = 0;
            foreach (var m in combos)
            {
                if (!m.Done || m.combo.passive || System.Array.IndexOf(m.pieces, it.st) < 0) continue;
                if (Random.Range(0, ++n) == 0) pick = m;
            }
            return pick;
        }

        /// <summary>A finished passive combo round this stove (Dinner table, Chef's corner).</summary>
        private GameRules.ComboMatch StoveCombo(Item stove, System.Func<ComboData, bool> which)
        {
            if (visiting || stove == null) return null;
            return combos.Find(m => m.Done && m.combo.passive && which(m.combo) && System.Array.IndexOf(m.pieces, stove.st) >= 0);
        }

        /// <summary>Things it can do on its own now: every piece that starts a finished combo.</summary>
        private bool StartsCombo(Item it)
        {
            return combos.Any(m => m.Done && !m.combo.passive && System.Array.IndexOf(m.pieces, it.st) >= 0 && IsLead(m, it));
        }

        // ---------------- the activities ----------------

        private void ClearComboProps()
        {
            foreach (var p in comboProps) if (p != null) Node.Destroy(p);
            comboProps.Clear();
            if (comboDressed) { comboDressed = false; RefreshCosmetics(); }
        }

        private Transform Prop(string name)
        {
            var g = Node.Group(room, name);
            Node.SetLayer(g, HomeLayer);
            comboProps.Add(g);
            return g;
        }

        /// <summary>Just arrived at the combo's piece.</summary>
        private void StartCombo(Activity A)
        {
            if (A.combo == null) return;
            switch (A.act.role)
            {
                case "read":
                {
                    // A little open book held in front of it.
                    var b = Prop("book");
                    Node.Mesh(b, ThreeGeo.RBox(.2f, .014f, .15f, .006f), ThreeMat.M("#C8674E"), 0, 0, 0, shadow: false);
                    foreach (var s in new[] { -1f, 1f })
                    {
                        var page = Node.Mesh(b, ThreeGeo.RBox(.09f, .012f, .13f, .005f), ThreeMat.M("#FFF6E6"), s * .048f, .012f, 0, shadow: false);
                        page.RotZ(-s * .18f);
                        for (int k = 0; k < 3; k++) Node.Mesh(b, ThreeGeo.RBox(.06f, .002f, .008f, .001f), ThreeMat.M("#B9A58C"), s * .05f, .021f + (s < 0 ? .006f : .006f), -.035f + k * .032f, shadow: false).RotZ(-s * .18f);
                    }
                    var flip = Node.Mesh(b, ThreeGeo.RBox(.088f, .006f, .128f, .003f), ThreeMat.M("#FFF6E6"), .044f, .018f, 0, shadow: false);
                    flip.name = "flip";
                    Node.SetLayer(b, HomeLayer);
                    break;
                }
                case "spa":
                    Floater("Aaah…");
                    break;
                case "splash":
                {
                    // In with a big splash.
                    var wp = PetWorld();
                    for (int k = 0; k < 40; k++)
                    {
                        float a = Rnd(0, Mathf.PI * 2), sp = Rnd(.6f, 1.6f);
                        drops.Spawn(new Vector3(wp.x, wp.y + .15f, wp.z), new Vector3(Mathf.Cos(a) * sp, Rnd(1.2f, 2.4f), Mathf.Sin(a) * sp), .02f, .7f, 0, -6);
                    }
                    sfx.Bath();
                    Buzz(20);
                    pet.V += 5;
                    Floater("Splash!");
                    break;
                }
                case "cuddle":
                {
                    var pw = items.Find(i => i.arch == "pomwand");
                    var pp = Prop("cuddlePom");
                    ItemModels.Pompom(pp, C.Style(pw != null ? pw.style : null) ?? C.Style("minimal"), .06f);
                    Node.SetLayer(pp, HomeLayer);
                    pet.Express(SquishyModel.Mouth.Grin, 2);
                    break;
                }
                case "concert": concertNote = 0; break;
                case "teaparty": Floater("Tea for two"); break;
                case "meditate": pet.Express(SquishyModel.Mouth.Sleep, .5f); break;
            }
        }

        /// <summary>Per frame while a combo activity runs (after the piece's own animation).</summary>
        private void StepCombo(Activity A, float dt, float t, ref float lift, ref float extra)
        {
            if (A.combo == null) return;
            var pw = PetWorld();
            var it0 = A.it;
            float h = pet.Scale * pet.StageScale;
            Vector3 front = new Vector3(Mathf.Sin(petYawY), 0, Mathf.Cos(petYawY));
            switch (A.act.role)
            {
                case "meditate":
                    // Perfectly still: a slow breath and a gentle float, with a few calm sparkles rising.
                    extra = .05f * Mathf.Sin(t * 1.3f);
                    lift += .04f + .025f * Mathf.Sin(t * 1.3f);
                    if (Random.value < dt * 2.5f) Glints(new Vector3(pw.x + Rnd(-.25f, .25f), pw.y + Rnd(.1f, .5f) * h, pw.z + Rnd(-.25f, .25f)), "#CFE8D6", 1);
                    break;
                case "read":
                    extra = .03f * Mathf.Sin(t * 2);
                    if (comboProps.Count > 0)
                    {
                        var b = comboProps[0];
                        b.localPosition = new Vector3(pw.x + front.x * (PetRadius() + .06f), pw.y + .32f * h, pw.z + front.z * (PetRadius() + .06f));
                        b.localRotation = Quaternion.Euler(-35, petYawY * Mathf.Rad2Deg, 0);
                        var flip = b.Find("flip");
                        if (flip != null) { float u = Mathf.Repeat(t / 1.8f, 1); flip.localRotation = Quaternion.Euler(0, 0, u < .3f ? u / .3f * 180 : 180); flip.localPosition = new Vector3(u < .3f ? .044f * Mathf.Cos(u / .3f * Mathf.PI) : -.044f, .018f + (u < .3f ? .04f * Mathf.Sin(u / .3f * Mathf.PI) : 0), 0); }
                    }
                    break;
                case "teaparty":
                    if (Random.value < dt * 2) Glints(new Vector3(pw.x, pw.y + h * .9f, pw.z), "#F2A7B8", 1);
                    break;
                case "spa":
                    // A spa bath: lots of clear bubbles rising round it (bigger than a plain bath's).
                    if (it0 != null && it0.parts.water != null && Random.value < dt * 16)
                    {
                        var wp = ThreeWorld(it0.parts.water);
                        bathBubbles.Spawn(new Vector3(wp.x + Rnd(-.22f, .22f), wp.y + .03f, wp.z + Rnd(-.14f, .14f)), new Vector3(Rnd(-.04f, .04f), Rnd(.1f, .28f), Rnd(-.04f, .04f)), Rnd(.022f, .045f), Rnd(1.4f, 2.4f), 1.5f, .03f);
                    }
                    break;
                case "shake":
                    // A quick wobble that flings the drops off.
                    extra = .16f * Mathf.Sin(t * 34) * (1 - t / A.act.dur);
                    if (Random.value < dt * 30)
                    {
                        float a = Rnd(0, Mathf.PI * 2), sp = Rnd(.8f, 1.6f);
                        drops.Spawn(new Vector3(pw.x, pw.y + .5f * h, pw.z), new Vector3(Mathf.Cos(a) * sp, Rnd(.3f, 1.2f), Mathf.Sin(a) * sp), .016f, .5f, 0, -5);
                    }
                    break;
                case "cuddle":
                    extra = .06f * Mathf.Sin(t * 2.2f);
                    if (comboProps.Count > 0) comboProps[0].localPosition = new Vector3(pw.x + front.x * (PetRadius() + .02f), pw.y + .28f * h, pw.z + front.z * (PetRadius() + .02f));
                    if (Random.value < dt * 1.5f) Glints(new Vector3(pw.x, pw.y + h * .9f, pw.z), "#F2A7B8", 1);
                    break;
                case "dressup":
                {
                    // Showing off: it turns to face you.
                    if (cam != null) { var cp = ThreeWorld(cam.transform); YawTo(cp.x, cp.z, dt, 8); }
                    // Pops into the wardrobe, out again wearing something, a happy hop, then back to normal.
                    if (!comboDressed && t > .5f && t < A.act.dur - .6f)
                    {
                        var pick = PickDressUp();
                        if (pick != null)
                        {
                            comboDressed = true;
                            pet.SetCosmetics(pick.slot == "hat" ? Rules.Worn(pick.id) : Rules.Worn(S.hat), pick.slot == "face" ? Rules.Worn(pick.id) : Rules.Worn(S.face), pick.slot == "neck" ? Rules.Worn(pick.id) : Rules.Worn(S.neck));
                            Glints(new Vector3(pw.x, pw.y + h * .8f, pw.z), "#FFE08A", 5);
                            sfx.Pop();
                            pet.Express(SquishyModel.Mouth.Grin, 3);
                            Floater((Rules.HasCosmetic(pick.id) ? "Wearing " : "Trying on ") + pick.name);
                        }
                    }
                    if (comboDressed) lift += Mathf.Abs(Mathf.Sin(t * 5)) * .1f;
                    if (comboDressed && t >= A.act.dur - .6f)
                    {
                        comboDressed = false;
                        RefreshCosmetics();
                        Glints(new Vector3(pw.x, pw.y + h * .8f, pw.z), "#FFE08A", 3);
                        sfx.Pop();
                    }
                    break;
                }
            }
        }

        /// <summary>A few soft glints drifting up (calm sparkles, warm hearts' pink).</summary>
        private void Glints(Vector3 at, string hex, int n)
        {
            if (fxPool == null) return;
            for (int i = 0; i < n; i++)
                fxPool.Spawn(at, new Vector3(Rnd(-.08f, .08f), Rnd(.18f, .32f), Rnd(-.08f, .08f)), Rnd(.045f, .07f), Rnd(1.1f, 1.7f), 1.4f, .02f,
                    w: new Vector3(Rnd(0, 6), 0, Rnd(-1.5f, 1.5f)), col: ThreeMat.Hex(hex));
        }

        /// <summary>An accessory to try on: mostly one it could still earn (a glimpse of prestige), otherwise one of yours.</summary>
        private CosmeticData PickDressUp()
        {
            if (C.cosmetics == null || C.cosmetics.Length == 0) return null;
            var notYet = C.cosmetics.Where(c => !Rules.HasCosmetic(c.id)).ToList();
            var owned = C.cosmetics.Where(c => Rules.HasCosmetic(c.id)).ToList();
            var from = notYet.Count > 0 && (owned.Count == 0 || Random.value < .7f) ? notYet : owned;
            return from[Random.Range(0, from.Count)];
        }

        /// <summary>The concert plays a whole tune in order instead of random notes.</summary>
        private int ConcertBar() { return ConcertTune[concertNote++ % ConcertTune.Length]; }

        /// <summary>The combo's activity is over: its lasting effects.</summary>
        private void EndCombo(Activity A)
        {
            if (A.combo == null) return;
            var c = A.combo.combo;
            if (A.act.role == "meditate" && c.calmMinutes > 0)
            {
                Rules.StartCalm(c.calmMinutes);
                Floater("Calm for " + Mathf.RoundToInt(c.calmMinutes) + " min");
            }
            if (A.act.role == "concert") { Floater("Encore!"); pet.V += 4; sfx.Chime(); }
            if (!ai.self && !visiting) TaskEvent("combo_" + c.id);
        }

        /// <summary>A combo that carries on to its next piece (shower, then shake dry just outside it; the slide into the tub...).</summary>
        private bool ChainCombo(Activity A)
        {
            if (A == null || A.combo == null || A.chained || S.dead || visiting) return false;
            var c = A.combo.combo;
            Item next = null;
            if (!string.IsNullOrEmpty(c.then)) next = ItemOf(A.combo.pieces[c.thenSlot]);
            else if (c.chainLeads) foreach (var p in A.combo.pieces) { var i = ItemOf(p); if (i != null && i != A.it && IsLead(A.combo, i)) next = i; }
            if (next == null) return false;
            EndToys();
            var sh = items.Find(i => i.arch == "shower");
            if (sh != null && sh.parts.curtain != null) sh.parts.openT = 1;
            bool plop = A.role == "slide" && DockedTub(A.it) == next; // docked: it lands right in the bath
            UseItem(next, !ai.self, null, A);
            if (plop && ai.act != null && ai.act.chained && ai.spot != null)
            {
                ai.x = ai.spot.stand.x;
                ai.z = ai.spot.stand.y;
                ai.y = ai.spot.y;
                ai.path.Clear();
                ai.seg = null;
                ai.mode = "act";
                ai.actT = 0;
                if (ai.spot.approach.HasValue) ai.perch = ai.spot.approach;
                StartAct();
            }
            return ai.act != null && ai.act.chained;
        }

        /// <summary>Dinner table: once the food is cooked, it's carried to the table and eaten sitting on the stool.</summary>
        private bool MoveDinnerToTable(Activity A)
        {
            var m = StoveCombo(A.it, c => c.id == "dinner_table");
            if (m == null || cookDish == null) return false;
            Item table = null, seat = null;
            for (int j = 0; j < m.pieces.Length; j++)
            {
                var i = ItemOf(m.pieces[j]);
                if (i == null) continue;
                if (i.a.role == "tea") table = i; else if (i.a.role == "seat") seat = i;
            }
            if (table == null || seat == null) return false;
            if (cookTool != null) { Node.Destroy(cookTool); cookTool = null; }
            if (A.it.parts.pan != null) A.it.parts.pan.gameObject.SetActive(true);
            // The dish goes on the table, on the stool's side.
            float dx = seat.tx - table.tx, dz = seat.tz - table.tz, l = Mathf.Max(.001f, Dist(dx, dz));
            float top = table.parts.pot != null ? ThreeWorld(table.parts.pot).y : Y0 + .3f;
            cookDish.gameObject.SetActive(true);
            cookDish.localPosition = new Vector3(table.tx + dx / l * .14f, top, table.tz + dz / l * .14f);
            sfx.Drop();
            var act = C.Activity("dine");
            ai.act = new Activity { it = table, role = "dine", act = act, recipe = A.recipe, combo = m, chained = true };
            ai.actT = 0;
            ai.target = table;
            ai.spot = SeatSpot(seat, table);
            ai.mode = "walk";
            PlanPath(ai.spot.stand.x, ai.spot.stand.y, ai.spot.y, ai.spot.approach, seat);
            ui.ShowBubble("hunger", "Dinner at the table", false);
            return true;
        }

        /// <summary>Chef's corner at this stove: quicker cooking, less wear, and sometimes an ingredient saved.</summary>
        private ComboData ChefAt(Item stove) { var m = StoveCombo(stove, c => c.cookMul > 0); return m != null ? m.combo : null; }

        // ---------------- Comfort panel ----------------

        /// <summary>
        /// The combos by name with how many of their pieces are together: the ones in place are named, the rest
        /// stay a mystery until found (hinted, not spelled out). Finished ones say what they do.
        /// </summary>
        private void ComboList(VisualElement body)
        {
            if (combos.Count == 0) return;
            Hud.Sec(body, "Combos · " + combos.Count(m => m.Done) + " of " + combos.Count);
            Hud.Para(body, "Put pieces right next to each other · +" + Mathf.RoundToInt(C.rules.comboComfort) + " comfort each", 12, "#6F5F52");
            foreach (var m in combos.OrderByDescending(x => x.Done).ThenByDescending(x => x.have))
            {
                var row = new Frame().Set(Css.C(m.Done ? "#EEF3E6" : "#FFF9EF"), 12).Col().Pad(6, 10, 6, 10).In(body);
                row.style.marginTop = 5;
                var top = new VisualElement().Row(Align.Center).In(row);
                var name = Css.Label(top, m.combo.name, "Figtree", 700, 14);
                name.style.flexGrow = 1;
                if (m.Done && !string.IsNullOrEmpty(m.combo.@short)) Css.Label(top, m.combo.@short, "Figtree", 600, 12, "#5F7F62").Margin(0, 8, 0, 0);
                Css.Label(top, m.have + "/" + m.pieces.Length, "Gluten", 800, 13, m.Done ? "#4C7552" : m.have > 0 ? "#A07324" : Hud.Muted);
                if (m.have == 0) continue;
                var parts = new List<string>();
                for (int j = 0; j < m.pieces.Length; j++) parts.Add(m.pieces[j] != null ? (C.Type(m.pieces[j].Arch) != null ? C.Type(m.pieces[j].Arch).name : m.pieces[j].Arch) : "?");
                Css.Label(row, string.Join(" · ", parts), "Figtree", 600, 12, Hud.Muted).Wrap();
            }
        }
    }
}
