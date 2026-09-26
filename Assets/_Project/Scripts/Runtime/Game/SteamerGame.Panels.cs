using System;
using System.Collections.Generic;
using System.Linq;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    public sealed partial class SteamerGame
    {
        private static readonly (string id, string label)[] TabList =
            { ("furniture", "Furniture"), ("walls", "Walls & floors"), ("tools", "Kitchen tools"), ("recipes", "Recipes"), ("pantry", "Pantry"), ("skins", "Steamers") };

        private sealed class Entry
        {
            public string key, name, rarity;
            public bool own;
            public int count, i = -1;
            public SteamerSkinData skin;
        }

        private string tab = "furniture", styleF = "all", selKey;
        private (string style, List<(Item it, string style)> list)? preview;
        private Action detailAct;
        private Item cookStoveOpen;

        // ---------------- HUD button handlers ----------------

        public void OnMainHome() { sfx.Tap(); EnterUnbox(); }
        public void OnMainDone() { ExitEdit(); }
        public void OnCoinPill() { if (mode == "home") { OpenShop(); sfx.Tap(); } }
        public void OnShop() { OpenShop(); sfx.Tap(); }
        public void OnTasks() { DrawTasks(); ui.OpenPanel("tasks"); sfx.Tap(); }
        public void OnOdds() { UpdatePity(); ui.OpenPanel("odds"); }
        public void OnPanelClosed(string id) { if (id != "cook" && id != "odds") sfx.Tap(); }

        public void OnSound(bool on)
        {
            sfx.SoundOn = on;
            sfx.MusicOn = S.musicOn;
            S.soundOn = on;
            if (on) sfx.Tap(); else sfx.Hum(0);
        }

        public void RestoreLamps() { if (mode == "unbox") SceneLighting_Clear(); else PlaceLamps(); }
        private static void SceneLighting_Clear() { World.SceneLighting.ClearLamps(); }

        private void CloseCook() { ui.ClosePanels(); }

        // ---------------- cooking ----------------

        private void OpenCook(Item stove)
        {
            if (S.dead) return;
            cookStoveOpen = stove;
            var list = ui.PanelBody("cook");
            ui.SetPanelSub("cook", "Kitchen level " + Rules.KitchenLvl() + " of 4 · " + Rules.ToolsOwned() + " of " + C.tools.Length + " tools");
            for (int i = 0; i < C.recipes.Length; i++)
            {
                var rc = C.recipes[i];
                var miss = Rules.MissingFor(rc);
                int lv = Rules.RecipeLvl(i);
                float mul = 1 + .1f * (lv - 1);
                var extra = new VisualElement();
                var stars = new VisualElement().Row().In(extra);
                for (int s = 0; s < 5; s++) stars.Add(Icons.Make(s < lv ? "star" : "star_empty", 12, "#D9A64A"));
                string fx = "Hunger +" + Mathf.RoundToInt(rc.hunger * mul * 100) + "%" + (rc.cap > 0 ? " (up to " + Mathf.RoundToInt(rc.cap * 100) + "%)" : "")
                    + (!string.IsNullOrEmpty(rc.bonusNeed) ? " · " + char.ToUpper(rc.bonusNeed[0]) + rc.bonusNeed.Substring(1) + " +" + Mathf.RoundToInt(rc.bonus * mul * 100) + "%" : "")
                    + (lv < 5 && rc.ing.Length > 0 ? " · " + (3 - S.recipeXP[i] % 3) + " more to level up" : "");
                var fxl = Css.Label(extra, fx, "Figtree", 600, 11, "#5F7F62");
                fxl.Wrap();
                var req = new VisualElement().Row().In(extra);
                req.style.flexWrap = Wrap.Wrap;
                if (rc.ing.Length == 0 && rc.tools.Length == 0) Hud.ReqChip(req, "Always free", false);
                foreach (var k in rc.ing) Hud.ReqChip(req, C.pantry[k].name + " (" + S.pantry[k] + ")", !(S.pantry[k] > 0));
                foreach (var k in rc.tools)
                {
                    bool own = Rules.Owned(Rules.ToolKey(k));
                    Hud.ReqChip(req, C.tools[k].name + (own ? " " + S.toolDur[k] + "/" + C.tools[k].maxDur : ""), !own || S.toolDur[k] <= 0);
                }
                if (Rules.KitchenLvl() < rc.lvl) Hud.ReqChip(req, "Kitchen Lv " + rc.lvl, true);
                var rcc = rc;
                var row = ui.Rec(list, "dish:" + i, rc.name, null, "Cook", () => { CloseCook(); UseItem(cookStoveOpen, true, rcc); sfx.Tap(); }, miss.Count > 0, extra);
            }
            list.Gap(8);
            ui.OpenPanel("cook");
        }

        // ---------------- tasks ----------------

        private void DrawTasks()
        {
            var body = ui.PanelBody("tasks");
            ui.SetPanelSub("tasks", "Finish 3 for " + C.rules.taskSetSteamers + " free steamers · " + S.setDone + " of 3 done · streak " + S.streak + " day" + (S.streak == 1 ? "" : "s") + " · this week " + Rules.WeekTasks() + "/" + C.rules.weeklyGoal);
            for (int i = 0; i < S.tasks.Count; i++)
            {
                var t = S.tasks[i];
                var d = Rules.TaskDef(t);
                if (!Rules.TaskReady(t))
                {
                    var w = Rules.TaskWait(t);
                    ui.Rec(body, null, "New task on its way", "Next care task in " + (int)w.TotalHours + "h " + w.Minutes.ToString("00") + "m", "…", null, true, null, true);
                    continue;
                }
                var pr = Hud.Bar(t.prog / d.goal, 8, "#6F9A74", 0, 6);
                string sub = d.time ? Mathf.FloorToInt(t.prog / 60) + ":" + Mathf.FloorToInt(t.prog % 60).ToString("00") + " of 3:00" : Mathf.FloorToInt(t.prog) + " of " + d.goal;
                int idx = i;
                ui.Rec(body, null, d.text, sub + " · +" + d.coins + " coins", t.done ? "Claim" : "…", () =>
                {
                    if (!S.tasks[idx].done) return;
                    sfx.Coin();
                    if (Rules.ClaimTask(idx)) { ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .5f), "+" + C.rules.taskSetSteamers + " steamers!"); sfx.Chime(); }
                    if (Rules.LastRewardMessage != null) { ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .42f), Rules.LastRewardMessage); sfx.Chime(); Rules.LastRewardMessage = null; }
                    ui.TaskDot(Rules.AnyTaskDone());
                    DrawTasks();
                }, !t.done, pr, true);
            }
            float inc = Rules.HappyRate(comfort);
            Hud.Sec(body, "While Happy");
            ui.Rec(body, null, "+" + inc.ToString("0.0") + " coins a minute", "Grows with Comfort (" + Mathf.RoundToInt(comfort) + "). Pauses when your squishy isn’t Happy.", null, null, false, null, true);
            body.Gap(8);
        }

        // ---------------- comfort + expansion ----------------

        public void OnComfort()
        {
            ui.SetPanelTitle("info", "Comfort");
            var body = ui.PanelBody("info");
            int decor = items.Count(i => i.a.comfort > 0);
            int slow = Mathf.RoundToInt(Mathf.Min(C.rules.comfortSlowMax, comfort * C.rules.comfortSlowPerPoint) * 100);
            Big(body, Mathf.RoundToInt(comfort).ToString(), "From " + decor + " furnished pieces" + (setBonus.Length > 0 ? ", plus a " + setBonus + " set bonus (+3) for three pieces in one style" : "") + ". Wilted plants count less.");
            Hud.Para(body, "Needs drain " + slow + "% slower (up to 40%).");
            Hud.Para(body, "While Happy you earn " + Rules.HappyRate(comfort).ToString("0.0") + " coins a minute.");
            Hud.Para(body, "Raise it with decor from steamers or the shop. Decor space grows when you expand the room or your squishy grows.");
            // Room progression at a glance.
            var cur = C.roomLevels[S.roomLv];
            int bonus = Rules.SizeDecorBonus(Rules.FavSizeIdx);
            Hud.Sec(body, "Room · level " + (S.roomLv + 1) + " of " + C.roomLevels.Length);
            Hud.Para(body, "Decor space " + DecorCount() + " of " + Rules.DecorSlots() + ": " + cur.slots + " from the room" + (bonus > 0 ? ", +" + bonus + " from " + Rules.Fav.name + "'s " + C.sizes[Rules.FavSizeIdx].name + " size" : "") + ".");
            if (S.roomLv + 1 < C.roomLevels.Length)
            {
                var nx = C.roomLevels[S.roomLv + 1];
                Hud.Para(body, "Next level: " + Rules.SquishKinds + " of " + nx.need + " squishies" + (Rules.SquishKinds >= nx.need ? " ✓" : "") + " · " + S.coins + " of " + nx.cost + " coins" + (S.coins >= nx.cost ? " ✓" : "") + ". Wider steamer, " + (nx.slots + bonus) + " decor spaces.");
                if (Rules.CanExpand()) Hud.Button(body, "Expand now · " + nx.cost + " coins", "#6F9A74", "#4C7552", Hud.Cream, 14, 44, 16, () => { if (!Spend(nx.cost)) return; ExpandRoom(); ui.ClosePanel("info"); }, false, 4);
            }
            else Hud.Para(body, "Your steamer is at its largest.");
            ui.OpenPanel("info");
            sfx.Tap();
        }

        internal static void BigText(VisualElement body, string big, string rest) { Big(body, big, rest); }

        private static void Big(VisualElement body, string big, string rest)
        {
            var p = new VisualElement().In(body);
            p.style.marginBottom = 8;
            Css.Label(p, big, "Gluten", 700, 30).Margin(2, 0, 6, 0);
            var l = Css.Label(p, rest, "Figtree", 400, 13.5f, "#4F4036");
            l.Wrap();
        }

        public void OnExpand()
        {
            var cur = C.roomLevels[S.roomLv];
            var nx = S.roomLv + 1 < C.roomLevels.Length ? C.roomLevels[S.roomLv + 1] : null;
            ui.SetPanelTitle("info", "Expand room");
            var body = ui.PanelBody("info");
            int bonus = Rules.SizeDecorBonus(Rules.FavSizeIdx);
            Big(body, "Level " + (S.roomLv + 1), "Decor space " + DecorCount() + " of " + Rules.DecorSlots() + (bonus > 0 ? " (" + cur.slots + " from the room, +" + bonus + " for " + Rules.Fav.name + "'s " + C.sizes[Rules.FavSizeIdx].name + " size)" : "") + ". Bigger squishies unlock more decor space.");
            if (nx == null) { Hud.Para(body, "Your steamer is at its largest."); ui.OpenPanel("info"); return; }
            int have = Rules.SquishKinds;
            Hud.Para(body, "Level " + (S.roomLv + 2) + " makes the steamer wider and gives " + (nx.slots + bonus) + " decor spaces. It needs " + nx.need + " squishies in your collection (you have " + have + ") and " + nx.cost + " coins.");
            Hud.Button(body, "Expand · " + nx.cost + " coins", "#6F9A74", "#4C7552", Hud.Cream, 14, 48, 17, () =>
            {
                if (!Spend(nx.cost)) return;
                ExpandRoom();
                ui.ClosePanel("info");
            }, have < nx.need || S.coins < nx.cost, 4);
            ui.OpenPanel("info");
        }

        // ---------------- shop ----------------

        private void OpenShop()
        {
            var body = ui.PanelBody("shop");
            ui.SetPanelSub("shop", S.coins + " coins");
            var R = C.rules;
            Hud.Sec(body, "Steamers");
            ui.Rec(body, "steamerbox", "Steamer", "You have " + S.steamers + ". Also earned from care tasks.", R.steamerPrice.ToString(), () => { if (Spend(R.steamerPrice)) { Rules.SetSteamers(S.steamers + 1); OpenShop(); } }, S.coins < R.steamerPrice);
            Hud.Sec(body, "Snacks");
            for (int i = 0; i < C.snacks.Length; i++)
            {
                int k = i;
                ui.Rec(body, "snack:" + i, C.snacks[i].name, "Quick snack from the pantry cupboard · you have " + S.snacks[i], R.snackPrice.ToString(), () => { if (Spend(R.snackPrice)) { S.snacks[k]++; OpenShop(); } }, S.coins < R.snackPrice);
            }
            Hud.Sec(body, "Meal kits");
            for (int i = 0; i < C.recipes.Length; i++)
            {
                var rc = C.recipes[i];
                if (!Rules.ShopKit(rc)) continue;
                int price = Rules.ShopKitPrice(rc);
                string contents = string.Join(", ", rc.ing.Select(k => R.shopKitCooks + " " + C.pantry[k].name));
                ui.Rec(body, "dish:" + i, rc.name + " kit", "Ingredients for " + R.shopKitCooks + " cooks · " + contents, price.ToString(), () => { if (Rules.BuyKit(rc)) { sfx.Coin(); OpenShop(); } else sfx.Bonk(); }, S.coins < price);
            }
            ShopCosmetics(body);
            Hud.Sec(body, "Kitchen tools");
            for (int i = 0; i < C.tools.Length; i++)
            {
                var t = C.tools[i];
                int k = i;
                bool own = Rules.Owned(Rules.ToolKey(i));
                int d = S.toolDur[i];
                if (!own)
                {
                    if (t.basic)
                        ui.Rec(body, "tool:" + i, t.name, "New tool · " + t.maxDur + " uses", R.newToolPrice.ToString(), () =>
                        {
                            if (!Spend(R.newToolPrice)) return;
                            Rules.AddOwned(Rules.ToolKey(k));
                            S.toolDur[k] = C.tools[k].maxDur;
                            DecorateStoves();
                            OpenShop();
                        }, S.coins < R.newToolPrice);
                }
                else if (d < t.maxDur)
                {
                    int cost = Rules.RepairCost(i);
                    var dur = Hud.Bar((float)d / t.maxDur, 6, (float)d / t.maxDur < .25f ? "#C8412F" : "#6F9A74", 140, 5);
                    ui.Rec(body, "tool:" + i, t.name + (d <= 0 ? " (broken)" : ""), d + " of " + t.maxDur + " uses left", "Fix " + cost, () => { if (Spend(cost)) { S.toolDur[k] = C.tools[k].maxDur; OpenShop(); } }, S.coins < cost, dur);
                }
            }
            body.Gap(8);
            if (!ui.PanelOpen("shop")) ui.OpenPanel("shop");
        }

        // ---------------- catalogue ----------------

        public void OnCatalogue() { if (tab == "sq") { tab = "furniture"; selKey = null; } OpenCatalogue(); }

        /// <summary>The squishy collection opens from the name chip (top left), not the catalogue.</summary>
        public void OnSquishies()
        {
            if (mode != "home") return;
            tab = "sq";
            selKey = null;
            OpenCatalogue();
        }

        private void OpenCatalogue()
        {
            if (S.dead) return;
            CloseCook();
            ui.OpenSheet();
            DrawCatalogue();
            sfx.Tap();
        }

        public void OnCatClose()
        {
            CloseCatalogue();
            ui.SetHint("Tap furniture to send " + Rules.Fav.name + " there");
        }

        private void CloseCatalogue()
        {
            ui.CloseSheet();
            selKey = null;
            if (preview.HasValue) EndPreview();
            homeWall.Skin(curSkin);
        }

        private void EndPreview()
        {
            foreach (var (it, st) in preview.Value.list) if (items.Contains(it)) Restyle(it, st);
            homeWall.Skin(curSkin);
            preview = null;
            RebuildObstacles();
        }

        public void OnPreview()
        {
            if (preview.HasValue) { EndPreview(); DrawCatalogue(); return; }
            var s = C.Style(styleF);
            if (s == null) return;
            preview = (s.id, items.Where(it => it.arch != "tomb").Select(it => (it, it.style)).ToList());
            foreach (var it in items) if (it.arch != "tomb") Restyle(it, s.id);
            homeWall.Floor(s.pal[4], Models.SteamerModel.Mix(s.pal[4], s.pal[0], .3f), s.pal[0]);
            RebuildObstacles();
            ui.CloseSheet();
            camS.zoomT = 1;
            ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .4f), "Previewing " + s.shortName);
            ui.SetHint("Preview · open the catalogue to go back");
        }

        private List<Entry> Entries()
        {
            var list = new List<Entry>();
            if (tab == "sq") { for (int i = 0; i < C.finishes.Length; i++) list.Add(new Entry { key = "sq:" + i, name = C.finishes[i].name, rarity = C.FinishRarity(C.finishes[i]), own = Rules.SquishCount(i) > 0, i = i }); return list; }
            if (tab == "recipes") { for (int i = 0; i < C.recipes.Length; i++) list.Add(new Entry { key = "dish:" + i, name = C.recipes[i].name, rarity = "Lv " + C.recipes[i].lvl, own = Rules.MissingFor(C.recipes[i]).Count == 0, i = i }); return list; }
            if (tab == "tools") { for (int i = 0; i < C.tools.Length; i++) list.Add(new Entry { key = "tool:" + i, name = C.tools[i].name, rarity = C.tools[i].rarity, own = Rules.Owned("tool:" + i), i = i }); return list; }
            if (tab == "pantry")
            {
                for (int i = 0; i < C.pantry.Length; i++) list.Add(new Entry { key = "food:" + i, name = C.pantry[i].name, rarity = C.pantry[i].rarity, own = S.pantry[i] > 0, count = S.pantry[i], i = i });
                for (int i = 0; i < C.snacks.Length; i++) list.Add(new Entry { key = "snack:" + i, name = C.snacks[i].name, rarity = "Snack", own = S.snacks[i] > 0, count = S.snacks[i], i = i });
                return list;
            }
            if (tab == "skins") { for (int i = 0; i < C.skins.Length; i++) list.Add(new Entry { key = "skin:" + i, name = C.skins[i].name, rarity = C.skins[i].rarity, own = Rules.Owned("skin:" + i), skin = C.skins[i], i = i }); return list; }
            var cats = tab == "walls" ? new[] { "Wall", "Floor" } : new[] { "Furniture", "Station", "Decor", "Toy" };
            foreach (var c in C.Catalogue)
                if (cats.Contains(c.cat) && (styleF == "all" || c.style == styleF))
                    list.Add(new Entry { key = c.key, name = c.name, rarity = c.rarity, own = Rules.Owned(c.key) });
            return list;
        }

        private bool ownedOnly;

        private void DrawCatalogue()
        {
            bool squishies = tab == "sq";
            ui.CatTitle.text = squishies ? "Squishies" : "Catalogue";
            ui.Tabs.Shown(!squishies);
            ui.Tabs.Clear();
            foreach (var (id, label) in TabList) { string t = id; ui.Tab(label, id == tab, () => { tab = t; selKey = null; DrawCatalogue(); }); }
            bool styled = tab == "furniture" || tab == "walls";
            ui.ChipScroll.Shown(styled);
            ui.Chips.Clear();
            if (styled)
            {
                ui.StyleChip("All styles", null, null, styleF == "all", () => { styleF = "all"; selKey = null; DrawCatalogue(); });
                foreach (var s in C.styles) { string id = s.id; ui.StyleChip(s.shortName, s.pal[1], s.pal[0], styleF == id, () => { styleF = id; selKey = null; DrawCatalogue(); }); }
            }
            ui.PvBar.Shown((styled && styleF != "all") || preview.HasValue);
            var sf = C.Style(styleF);
            ui.PvBtnLbl.text = preview.HasValue ? "Back to my room" : "Preview " + (sf != null ? sf.shortName : "") + " in my room";
            ui.PvNote.text = preview.HasValue ? "Previewing " + C.Style(preview.Value.style).name : (sf != null ? sf.name : "");
            var list = Entries();
            var grid = ui.Grid.contentContainer;
            grid.Clear();
            ui.ClearThumbQueue();
            if (tab == "sq") { ui.CatCount.text = "Your squishy tree"; DrawTree(grid); ui.Detail.Shown(selKey != null); return; }
            int own = list.Count(e => e.own);
            ui.CatCount.text = own + " of " + list.Count + (styled ? " skins" : "") + " found" + (tab == "furniture" && styleF == "all" ? " · " + C.styles.Length + " styles" : "");
            // Owned-only filter: with hundreds of skins, finding your own things must be one tap.
            var bar = new VisualElement().Row(Align.Center, Justify.FlexEnd).In(grid);
            bar.style.marginBottom = 8;
            Hud.Button(bar, ownedOnly ? "Owned only · on" : "Owned only · off", ownedOnly ? "#6F9A74" : "#EADCC6", ownedOnly ? "#4C7552" : "#CDB999",
                ownedOnly ? Hud.Cream : Hud.Ink, 99, 32, 13, () => { ownedOnly = !ownedOnly; selKey = null; DrawCatalogue(); sfx.Tap(); }).Size(150, null);
            if (ownedOnly) list = list.Where(e => e.own).ToList();
            if (list.Count == 0) Hud.Para(grid, ownedOnly ? "Nothing owned here yet. Open steamers to find some!" : "Nothing here yet.", 13, "#6F5F52");
            VisualElement row = null;
            var cells = new Dictionary<string, Frame>();
            for (int n = 0; n < list.Count; n++)
            {
                if (n % 3 == 0) { row = new VisualElement().Row(Align.Stretch).In(grid); row.style.marginBottom = 8; }
                var e = list[n];
                var cell = ui.Cell(row, e.key, e.name + (e.count > 0 ? " ×" + e.count : ""), e.rarity, e.own, e.key == selKey,
                    e.skin != null ? (e.skin.a, e.skin.b, e.skin.t) : ((string, string, string)?)null, () =>
                    {
                        if (selKey != null && cells.TryGetValue(selKey, out var was)) Hud.SetCellSelected(was, false);
                        selKey = e.key;
                        Hud.SetCellSelected(cells[e.key], true);
                        ShowDetail(e);
                        sfx.Tap();
                    });
                cells[e.key] = cell;
                if (n % 3 != 0) cell.style.marginLeft = 8;
            }
            if (row != null) for (int k = list.Count % 3; k > 0 && k < 3; k++) { var f = new VisualElement().In(row); f.style.flexGrow = 1; f.style.flexBasis = 0; f.style.marginLeft = 8; }
            if (selKey == null) ui.Detail.Shown(false);
        }

        public void OnDetailAct() { detailAct?.Invoke(); }

        private void ShowDetail(Entry e)
        {
            ui.Detail.Shown(true);
            var th = ui.DetailThumb;
            th.Clear();
            if (e.skin != null) Hud.Swatch(th, e.skin.a, e.skin.b, e.skin.t, .62f);
            else { var img = new Image { scaleMode = ScaleMode.ScaleToFit, image = Thumbs.Get(e.key) }.In(th); img.style.width = img.style.height = Length.Percent(100); }
            ui.DName.text = e.name;
            ui.DAct.Shown(false);
            detailAct = null;
            string meta = "", note = "";
            string kind = e.key.Split(':')[0];
            void Act(string label, Action a) { ui.DAct.Shown(true); ui.DActLbl.text = label; detailAct = a; }
            if (kind == "sq")
            {
                var f = C.finishes[e.i];
                int c = Rules.SquishCount(e.i), si = C.SizeIdxFor(Mathf.Max(1, c));
                var nxt = si + 1 < C.sizes.Length ? C.sizes[si + 1] : null;
                meta = f.tier + " tier";
                note = c > 0 ? C.sizes[si].name + " · " + c + " cop" + (c > 1 ? "ies" : "y") + (nxt != null ? " · " + nxt.at + " for " + nxt.name : "") + " · +" + C.sizes[si].decor + " decor space" + (nxt != null ? " (" + nxt.name + ": +" + nxt.decor + ")" : "") : "Not found yet. Find it in steamers.";
                if (c > 0 && e.i != S.favIdx && !S.dead) Act("Make favourite", () => { SetPet(e.i); Floater("Now " + f.name + "!"); CloseCatalogue(); });
                if (e.i == S.favIdx && !S.dead)
                {
                    if (S.tucked) Act("Wake up", () => { Wake(); CloseCatalogue(); });
                    else if (Rules.CanTuck()) Act("Tuck in (pauses needs)", () => { Tuck(); CloseCatalogue(); });
                    else note = "Too weak to rest now: look after it first. " + note;
                }
                if (e.i == S.favIdx) note = "Your favourite · " + note;
            }
            else if (kind == "tool")
            {
                var t = C.tools[e.i];
                var uses = C.recipes.Where(r => r.tools.Contains(e.i)).Select(r => r.name).ToList();
                meta = "Kitchen tool · " + t.rarity;
                var sk = Enumerable.Range(0, C.toolSkins.Length).Where(j => j == 0 || Rules.Owned("tskin:" + e.i + ":" + j)).ToList();
                int cur = S.toolSkin[e.i];
                note = (e.own ? S.toolDur[e.i] + " of " + t.maxDur + " uses left. Skin: " + C.toolSkins[cur].name + " (" + sk.Count + " of " + C.toolSkins.Length + " unlocked). "
                               : "Not found yet. " + (t.basic ? "Sold in the shop. " : "Rare: steamers only. "))
                       + (uses.Count > 0 ? "Used for: " + string.Join(", ", uses) + "." : "");
                if (e.own && sk.Count > 1)
                    Act("Equip next skin", () =>
                    {
                        S.toolSkin[e.i] = sk[(sk.IndexOf(cur) + 1) % sk.Count];
                        Thumbs.Forget(e.key);
                        DecorateStoves();
                        sfx.Snap();
                        DrawCatalogue();
                        ShowDetail(e);
                    });
            }
            else if (kind == "food")
            {
                var f = C.pantry[e.i];
                var uses = C.recipes.Where(r => r.ing.Contains(e.i)).Select(r => r.name).ToList();
                meta = "Ingredient · " + f.rarity;
                note = (e.count > 0 ? e.count + " in your pantry. " : "None left. ") + (uses.Count > 0 ? "Used for: " + string.Join(", ", uses) + "." : "");
                note += f.rarity == "Common" ? " Comes in kitchen kits and shop meal kits." : " Rare: steamers only.";
            }
            else if (kind == "snack") { meta = "Snack"; note = e.count + " in the pantry cupboard. A quick bite: hunger up to 60%. Sold in the shop."; }
            else if (kind == "dish")
            {
                var rc = C.recipes[e.i];
                var miss = Rules.MissingFor(rc);
                meta = "Recipe · Kitchen Lv " + rc.lvl;
                var needs = rc.ing.Select(i => C.pantry[i].name).Concat(rc.tools.Select(i => C.tools[i].name)).ToList();
                note = "Needs " + (needs.Count > 0 ? string.Join(", ", needs) : "nothing") + ". " + (miss.Count > 0 ? "Missing: " + string.Join(", ", miss) + "." : "Ready to cook at the stove.");
            }
            else if (kind == "skin")
            {
                meta = "Steamer skin · " + e.rarity;
                note = e.own ? "Reskins your whole steamer." : "Not found yet. Tap to preview it.";
                Act(e.own ? "Use this steamer" : "Preview", () => { homeWall.Skin(e.skin); if (e.own) { curSkin = e.skin; WriteSave(); } sfx.Snap(); });
            }
            else
            {
                var c = C.Cat(e.key);
                var a = C.Type(c.arch);
                var s = C.Style(c.style);
                meta = c.rarity + " · " + s.name;
                var roles = new Dictionary<string, string>
                {
                    { "bed", "Naps here (Rest)" }, { "seat", "Seat for tea time" }, { "tea", "Tea time with a seat nearby (Rest, Hunger)" }, { "eat", "Cook and eat (Hunger)" },
                    { "snack", "Snacks (Hunger, up to 60%)" }, { "wash", "Quick wash (Clean, up to 65%)" }, { "bath", "Baths, with scrubbing (Clean)" }, { "shower", "Showers, with scrubbing (Clean)" },
                    { "lamp", "Lights off for a full night’s rest" }, { "plant", "Needs watering or it wilts" }, { "play", "Kick it or flick it (Play)" }, { "bounce", "Bouncing (Play)" },
                    { "lounge", "Lounging (Rest)" }, { "wand", "Wave the pom-pom and it chases (Play)" }, { "bubbles", "Bubbles to chase and pop (Play)" }, { "music", "Tap the bars to make it dance (Play)" }, { "slide", "Climbs up and slides down (Play)" }, { "decor", "Decor" },
                };
                string role = !string.IsNullOrEmpty(a.role) && roles.TryGetValue(a.role, out var rr) ? rr : a.cat == "Wall" ? "Divides the room" : "Decor";
                note = role + (a.comfort > 0 ? " · +" + a.comfort + " comfort" : "") + (a.size > 0 ? " · needs " + C.sizes[a.size].name + " size" : "");
                if (!Rules.StyleActive(s)) note += " · Event style: in steamers in " + string.Join(", ", s.eventMonths.Select(mo => new System.DateTime(2000, mo, 1).ToString("MMMM")));
                var inRoom = items.Find(i => i.key == c.key);
                var piece = items.Find(i => i.arch == c.arch);
                if (!e.own) note += ". Skin not found yet.";
                else if (inRoom != null) note += ". On your " + a.name.ToLowerInvariant() + " now.";
                else if (piece != null && !S.dead) Act("Use on my " + a.name.ToLowerInvariant(), () => { Restyle(piece, c.style); RebuildObstacles(); sfx.Snap(); DrawCatalogue(); ShowDetail(e); });
                else if (piece == null)
                {
                    int si = S.storage.FindIndex(k => k.Split(':')[0] == c.arch);
                    if (si >= 0 && !S.dead) Act("Place in room", () => { S.storage[si] = c.key; CloseCatalogue(); EnterEdit(); PlaceFromStorage(si); });
                    else note += ". You don’t have a " + a.name.ToLowerInvariant() + " yet: find one in steamers.";
                }
            }
            ui.DMeta.text = meta;
            ui.DNote.text = note;
        }
    }
}
