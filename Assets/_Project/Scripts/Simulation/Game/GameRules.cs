using System;
using System.Collections.Generic;
using Squishy.Simulation.Core;

namespace Squishy.Simulation.Game
{
    /// <summary>A steamer prize before it is revealed.</summary>
    public sealed class Reward
    {
        public string type; // sq, item, food, tool, tskin, skin
        public int i, j;
        public string key, rar;
    }

    /// <summary>What the reveal card shows, plus side effects for the presentation to play out.</summary>
    public sealed class RewardCard
    {
        public bool isNew;
        public string name, tier, dot, meta;
        public int delayedCoins;
        public int grewTo = -1;
        public bool kitchenChanged;
    }

    /// <summary>
    /// The prototype's game rules on plain data: needs, comfort, kitchen, steamers with pity,
    /// care tasks and the wallet. No Unity types, so it runs in tests and fast-forward.
    /// </summary>
    public sealed partial class GameRules
    {
        public readonly GameContent C;
        public readonly GameState S;
        private readonly HashSet<string> _owned;
        private readonly Pcg32 _rng;

        public event Action CoinsChanged, SteamersChanged, TasksChanged;

        /// <summary>Wall clock for task cooldowns (a server clock later).</summary>
        public IClock Clock = new SystemClock();

        public GameRules(GameContent content, GameState state)
        {
            C = content;
            S = state;
            _owned = new HashSet<string>(state.owned);
            if (state.trialStart == 0) state.trialStart = DateTime.UtcNow.Ticks;
            _rng = Pcg32.FromState(state.rng == 0 ? 0x9E3779B97F4A7C15UL : state.rng);
            // Content can grow between versions (new recipes, tools): keep per-item arrays the right length.
            state.pantry = Fit(state.pantry, content.pantry.Length);
            state.snacks = Fit(state.snacks, content.snacks.Length);
            state.toolSkin = Fit(state.toolSkin, content.tools.Length);
            state.recipeXP = Fit(state.recipeXP, content.recipes.Length);
            if (state.toolDur == null || state.toolDur.Length < content.tools.Length)
            {
                int old = state.toolDur == null ? 0 : state.toolDur.Length;
                state.toolDur = Fit(state.toolDur, content.tools.Length);
                for (int i = old; i < content.tools.Length; i++) state.toolDur[i] = content.tools[i].maxDur;
            }
            // Starter recipes are always known; the rest are learned from kitchen kits.
            for (int i = 0; i < content.recipes.Length; i++) if (content.recipes[i].starter) AddOwned(RecipeKey(i));
        }

        public RulesData R { get { return C.rules; } }

        private static int[] Fit(int[] a, int n)
        {
            if (a != null && a.Length >= n) return a;
            var b = new int[n];
            if (a != null) Array.Copy(a, b, a.Length);
            return b;
        }

        public static GameState NewState(GameContent c, ulong seed)
        {
            var st = c.starter;
            var s = new GameState
            {
                coins = st.coins, steamers = st.steamers, favIdx = st.favourite, roomLv = st.roomLevel, age = st.age,
                pantry = (int[])st.pantry.Clone(), snacks = (int[])st.snacks.Clone(), needs = (float[])st.needs.Clone(),
                toolDur = new int[c.tools.Length], toolSkin = new int[c.tools.Length], recipeXP = new int[c.recipes.Length],
                rng = new Pcg32(seed).State,
            };
            for (int i = 0; i < c.tools.Length; i++) s.toolDur[i] = c.tools[i].maxDur;
            s.owned.AddRange(st.owned);
            foreach (var q in st.squishies) s.squishOwned.Add(new CountData { i = q.i, n = q.n });
            foreach (var p in st.room) s.items.Add(new PieceState { key = p.key, x = p.x * st.roomScale, z = p.z * st.roomScale, ry = p.ry });
            s.storage.AddRange(st.storage);
            var rules = new GameRules(c, s);
            for (int i = 0; i < 3; i++) s.tasks.Add(rules.NewTask());
            s.rng = rules._rng.State;
            return s;
        }

        // ---- random ----
        public double Random() { double v = _rng.NextDouble(); S.rng = _rng.State; return v; }
        private T Pick<T>(IList<T> list) { return list[Math.Min(list.Count - 1, (int)(Random() * list.Count))]; }

        // ---- collection ----
        public bool Owned(string key) { return _owned.Contains(key); }
        public void AddOwned(string key) { if (_owned.Add(key)) S.owned.Add(key); }

        public int SquishCount(int i) { foreach (var q in S.squishOwned) if (q.i == i) return q.n; return 0; }

        public void SetSquish(int i, int n)
        {
            foreach (var q in S.squishOwned) if (q.i == i) { q.n = n; return; }
            S.squishOwned.Add(new CountData { i = i, n = n });
        }

        public void RemoveSquish(int i) { S.squishOwned.RemoveAll(q => q.i == i); }
        public int SquishKinds { get { return S.squishOwned.Count; } }
        public int FavSizeIdx { get { return C.SizeIdxFor(Math.Max(1, SquishCount(S.favIdx))); } }
        public FinishData Fav { get { return C.finishes[S.favIdx]; } }

        // ---- wallet ----
        public void AddCoins(int n) { S.coins += n; if (CoinsChanged != null) CoinsChanged(); }

        public bool Spend(int n)
        {
            if (S.coins < n) return false;
            S.coins -= n;
            if (CoinsChanged != null) CoinsChanged();
            return true;
        }

        public void SetSteamers(int n) { S.steamers = n; if (SteamersChanged != null) SteamersChanged(); }

        // ---- needs ----
        public float Need(int k) { return S.needs[k]; }
        public void SetNeed(int k, float v) { S.needs[k] = Math.Max(0f, Math.Min(1f, v)); }
        public float Condition() { return Math.Min(Math.Min(S.needs[0], S.needs[1]), Math.Min(S.needs[2], S.needs[3])); }

        public int LowestNeed()
        {
            int best = 0;
            for (int k = 1; k < 4; k++) if (S.needs[k] < S.needs[best]) best = k;
            return best;
        }

        public float ComfortSlow(float comfort) { return 1f - Math.Min(R.comfortSlowMax, comfort * R.comfortSlowPerPoint); }

        /// <summary>Drains needs, ages the squishy and runs the death clock. Returns true when it dies.</summary>
        public bool StepCare(float sdt, float comfort)
        {
            if (S.tucked) return false;
            S.qolSum += Condition() * sdt;
            S.qolTime += sdt;
            float slow = ComfortSlow(comfort);
            float[] decay = { R.decayHunger, R.decayPlay, R.decayRest, R.decayClean };
            for (int k = 0; k < 4; k++) S.needs[k] = Math.Max(0f, S.needs[k] - decay[k] * slow * sdt);
            S.dayT += sdt;
            if (S.dayT > R.dayLength) { S.dayT = 0; S.age++; }
            bool empty = false;
            for (int k = 0; k < 4; k++) if (S.needs[k] <= 0f) empty = true;
            if (empty) { S.deathClock += sdt; if (S.deathClock > R.deathSeconds) return true; }
            else S.deathClock = Math.Max(0f, S.deathClock - sdt);
            return false;
        }

        /// <summary>Whether the squishy can be tucked in now (not while Critical).</summary>
        public bool CanTuck() { return !S.dead && Condition() >= R.tuckMinCondition; }

        /// <summary>Happy, Droopy, Flat or Critical with its colour.</summary>
        public static string[] Stage(float cond)
        {
            if (cond > .5f) return new[] { "Happy", "#6F9A74" };
            if (cond > .25f) return new[] { "Droopy", "#D9A64A" };
            if (cond > .1f) return new[] { "Flat", "#E07B39" };
            return new[] { "Critical", "#C8412F" };
        }

        /// <summary>Comfort from furnished pieces (wilted plants count less), plus a set bonus for three in one style.</summary>
        public float Comfort(IEnumerable<PieceState> items, out string setBonus)
        {
            float comfort = 0;
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var it in items)
            {
                var t = C.Type(it.Arch);
                if (t == null) continue;
                comfort += t.comfort * (it.Arch == "plant" ? 1f - it.wilt : 1f);
                string st = it.Style;
                if (!counts.ContainsKey(st)) { counts[st] = 0; order.Add(st); }
                counts[st]++;
            }
            string best = null;
            int bestN = 0;
            foreach (var st in order) if (st.Length > 0 && counts[st] > bestN) { best = st; bestN = counts[st]; }
            setBonus = best != null && bestN >= R.setCount ? C.Style(best).shortName : "";
            if (setBonus.Length > 0) comfort += R.setBonus;
            return comfort;
        }

        public float HappyRate(float comfort) { return R.happyBase * (1f + comfort / R.happyComfortDivisor); }

        // ---- room ----
        public int PieceCount(string arch)
        {
            int n = 0;
            foreach (var it in S.items) if (it.Arch == arch) n++;
            foreach (var k in S.storage) if (k.Split(':')[0] == arch) n++;
            return n;
        }

        public int DecorCount() { int n = 0; foreach (var it in S.items) if (C.IsDecor(it.Arch)) n++; return n; }
        public RoomLevelData RoomLevel { get { return C.roomLevels[S.roomLv]; } }

        /// <summary>Decor space: the room level's slots plus a bonus for the favourite's size (you play, it grows, the room gets better).</summary>
        /// <summary>True when the next room level's collection and coin needs are both met.</summary>
        public bool CanExpand()
        {
            if (S.roomLv + 1 >= C.roomLevels.Length) return false;
            var nx = C.roomLevels[S.roomLv + 1];
            return SquishKinds >= nx.need && S.coins >= nx.cost;
        }

        public int DecorSlots() { return RoomLevel.slots + SizeDecorBonus(FavSizeIdx); }
        public int SizeDecorBonus(int sizeIdx) { return C.sizes[sizeIdx].decor; }

        // ---- kitchen ----
        public string ToolKey(int i) { return "tool:" + i; }
        public int ToolsOwned() { int n = 0; for (int i = 0; i < C.tools.Length; i++) if (Owned(ToolKey(i))) n++; return n; }
        public int KitchenLvl() { int n = ToolsOwned(); return 1 + (n >= 2 ? 1 : 0) + (n >= 4 ? 1 : 0) + (n >= 6 ? 1 : 0); }
        public string RecipeKey(int i) { return "recipe:" + i; }
        public bool Knows(int i) { return Owned(RecipeKey(i)); }
        public int KnownRecipes() { int n = 0; for (int i = 0; i < C.recipes.Length; i++) if (Knows(i)) n++; return n; }

        public int RecipeLvl(int i) { return Math.Min(5, 1 + S.recipeXP[i] / 3); }

        public List<string> MissingFor(RecipeData rc)
        {
            var miss = new List<string>();
            foreach (var i in rc.ing) if (!(S.pantry[i] > 0)) miss.Add(C.pantry[i].name);
            foreach (var i in rc.tools)
            {
                if (!Owned(ToolKey(i))) miss.Add(C.tools[i].name);
                else if (S.toolDur[i] <= 0) miss.Add(C.tools[i].name + " (broken)");
            }
            if (KitchenLvl() < rc.lvl) miss.Add("Kitchen Lv " + rc.lvl);
            return miss;
        }

        public int RepairCost(int t)
        {
            var tool = C.tools[t];
            return Math.Max(R.minRepair, (int)Math.Ceiling(tool.price * (1.0 - (double)S.toolDur[t] / tool.maxDur) * .5));
        }

        // ---- steamers ----
        public string RollRarity()
        {
            double r = Random();
            string rar = r < R.pLegendary ? "Legendary" : r < R.pLegendary + R.pEpic ? "Epic" : r < R.pLegendary + R.pEpic + R.pRare ? "Rare" : "Common";
            if (S.sinceEpic >= R.pityEpic - 1 && GameContent.RarityRank(rar) < 2) rar = "Epic";
            else if (S.sinceRare >= R.pityRare - 1 && GameContent.RarityRank(rar) < 1) rar = "Rare";
            S.sinceRare = GameContent.RarityRank(rar) >= 1 ? 0 : S.sinceRare + 1;
            S.sinceEpic = GameContent.RarityRank(rar) >= 2 ? 0 : S.sinceEpic + 1;
            return rar;
        }

        /// <summary>How many tiers (prizes) the next steamer has: usually 1, sometimes 2 or 3.</summary>
        public int RollLayers()
        {
            double r = Random();
            return r < R.pThreeLayers ? 3 : r < R.pThreeLayers + R.pTwoLayers ? 2 : 1;
        }

        public Reward RollReward()
        {
            string rar = RollRarity();
            double r = Random();
            if (rar == "Legendary") return Random() < .5 ? new Reward { type = "sq", i = C.finishes.Length - 1, rar = rar } : Skin(rar);
            if (rar == "Common")
            {
                if (r < .52) return Item(rar);
                if (r < .86) return Kit(rar); // kitchen kits replace loose common ingredients and single tools
                if (r < .96) return Sq(rar);
                return ToolSkin(rar);
            }
            if (rar == "Rare")
            {
                if (r < .5) return Item(rar);
                if (r < .64)
                {
                    var o = new List<int>();
                    for (int k = 0; k < C.pantry.Length; k++) if (C.pantry[k].rarity == "Rare") o.Add(k);
                    return new Reward { type = "food", i = Pick(o), rar = rar };
                }
                if (r < .8) return Sq(rar);
                if (r < .9) return Kit(rar);
                if (r < .97) return ToolSkin(rar);
                return Skin(rar);
            }
            if (r < .62) return Item(rar);
            if (r < .9) return Sq(rar);
            if (r < .96) return ToolSkin(rar);
            return Skin(rar);
        }

        private Reward Sq(string rar)
        {
            if (Random() < R.favouriteChance) return new Reward { type = "sq", i = S.favIdx, rar = rar };
            var o = new List<int>();
            for (int k = 0; k < C.finishes.Length; k++) if (C.FinishRarity(C.finishes[k]) == rar) o.Add(k);
            return new Reward { type = "sq", i = o.Count > 0 ? Pick(o) : S.favIdx, rar = rar };
        }

        private Reward Item(string rar)
        {
            var o = C.Catalogue.FindAll(c => c.rarity == rar && StyleActive(C.Style(c.style)));
            return new Reward { type = "item", key = Pick(o).key, rar = rar };
        }

        /// <summary>A recipe kit: its tools plus ingredients. Common steamers give easier recipes.</summary>
        private Reward Kit(string rar)
        {
            var o = new List<int>();
            for (int k = 0; k < C.recipes.Length; k++)
                if (C.recipes[k].tools.Length > 0 && (rar != "Common" || C.recipes[k].lvl <= 2)) o.Add(k);
            // Steamers are how you learn recipes: favour ones you don't know yet.
            var unknown = o.FindAll(k => !Knows(k));
            if (unknown.Count > 0 && Random() < .75) o = unknown;
            return new Reward { type = "kit", i = Pick(o), rar = rar };
        }

        private Reward Tool(string rar)
        {
            var o = new List<int>();
            for (int k = 0; k < C.tools.Length; k++) if (C.tools[k].rarity == rar) o.Add(k);
            return o.Count > 0 ? new Reward { type = "tool", i = Pick(o), rar = rar } : Item(rar);
        }

        private Reward Skin(string rar)
        {
            var o = new List<int>();
            for (int k = 0; k < C.skins.Length; k++) if (C.skins[k].rarity == rar) o.Add(k);
            return o.Count > 0 ? new Reward { type = "skin", i = Pick(o), rar = rar } : Item(rar);
        }

        private Reward ToolSkin(string rar)
        {
            var o = new List<int>();
            for (int k = 1; k < C.toolSkins.Length; k++) if (C.toolSkins[k].rarity == rar) o.Add(k);
            if (o.Count == 0) return Item(rar);
            return new Reward { type = "tskin", i = (int)(Random() * C.tools.Length), j = Pick(o), rar = rar };
        }

        /// <summary>Adds the prize to the collection and describes it for the reveal card.</summary>
        public RewardCard Claim(Reward rw)
        {
            var card = new RewardCard { isNew = true, dot = "#D8C7AE" };
            if (rw.type == "sq")
            {
                var f = C.finishes[rw.i];
                card.name = f.name;
                card.tier = C.FinishRarity(f) + " · " + f.tier;
                card.dot = C.TierColor(f.tier);
                int had = SquishCount(rw.i);
                SetSquish(rw.i, had + 1);
                card.isNew = had == 0;
                if (rw.i == S.favIdx)
                {
                    int before = C.SizeIdxFor(had), after = C.SizeIdxFor(had + 1);
                    var nxt = after + 1 < C.sizes.Length ? C.sizes[after + 1] : null;
                    card.meta = after > before ? "Your favourite grows to " + C.sizes[after].name + "!"
                        : nxt != null ? "Your favourite · " + (had + 1) + " of " + nxt.at + " to " + nxt.name : "Your favourite is fully grown";
                    if (after > before) { card.grewTo = after; S.prestige += R.sizePrestige; card.meta += " +" + R.sizePrestige + " prestige"; }
                }
                else card.meta = card.isNew ? "Added to your collection · " + SquishKinds + " of " + C.finishes.Length : "Duplicate · " + (had + 1) + " copies";
            }
            else if (rw.type == "item")
            {
                var c = C.Cat(rw.key);
                var s = C.Style(c.style);
                card.name = c.name;
                card.tier = c.rarity + " · " + s.shortName + " skin";
                card.dot = s.pal[1];
                card.isNew = !Owned(c.key);
                bool had = PieceCount(c.arch) > 0;
                if (card.isNew)
                {
                    AddOwned(c.key);
                    if (!had) { S.storage.Add(c.key); card.meta = "New item! It’s in storage · place it from Arrange"; }
                    else card.meta = "New skin for your " + C.Type(c.arch).name.ToLowerInvariant() + " · swap it in Arrange";
                }
                else { card.meta = "Duplicate skin · +" + R.dupeItemCoins + " coins"; card.delayedCoins = R.dupeItemCoins; }
            }
            else if (rw.type == "tskin")
            {
                var t = C.tools[rw.i];
                var k = C.toolSkins[rw.j];
                string key = "tskin:" + rw.i + ":" + rw.j;
                card.name = k.name + " " + t.name.ToLowerInvariant();
                card.tier = k.rarity + " · Tool skin";
                card.dot = k.col;
                card.isNew = !Owned(key);
                AddOwned(key);
                card.meta = card.isNew ? "Equip it in the catalogue under Kitchen tools" : "Duplicate · +" + R.dupeToolSkinCoins + " coins";
                if (!card.isNew) card.delayedCoins = R.dupeToolSkinCoins;
            }
            else if (rw.type == "skin")
            {
                var k = C.skins[rw.i];
                string key = "skin:" + rw.i;
                card.name = k.name + " steamer";
                card.tier = k.rarity + " · Steamer skin";
                card.dot = k.a;
                card.isNew = !Owned(key);
                AddOwned(key);
                card.meta = card.isNew ? "Reskins your whole room · use it from the catalogue" : "Duplicate · +" + R.dupeSteamerSkinCoins + " coins";
                if (!card.isNew) card.delayedCoins = R.dupeSteamerSkinCoins;
            }
            else if (rw.type == "kit")
            {
                var rc = C.recipes[rw.i];
                card.name = rc.name + " kit";
                card.tier = rw.rar + " · Kitchen kit";
                card.dot = rc.col;
                int lv0 = KitchenLvl();
                var parts = new List<string>();
                bool anyNew = false;
                if (!Knows(rw.i)) { AddOwned(RecipeKey(rw.i)); anyNew = true; parts.Add("New recipe!"); }
                foreach (var t in rc.tools)
                {
                    bool isNewTool = !Owned(ToolKey(t));
                    anyNew |= isNewTool;
                    AddOwned(ToolKey(t));
                    bool repaired = !isNewTool && S.toolDur[t] < C.tools[t].maxDur;
                    S.toolDur[t] = C.tools[t].maxDur;
                    parts.Add(C.tools[t].name + (isNewTool ? " (new!)" : repaired ? " (repaired)" : ""));
                }
                foreach (var i in rc.ing)
                {
                    S.pantry[i] += R.kitIngredients;
                    AddOwned("food:" + i);
                    parts.Add(R.kitIngredients + " " + C.pantry[i].name);
                }
                int lv1 = KitchenLvl();
                card.isNew = anyNew;
                card.meta = (lv1 > lv0 ? "Kitchen upgraded to level " + lv1 + "! · " : "") + string.Join(" · ", parts);
                card.kitchenChanged = true;
            }
            else if (rw.type == "food")
            {
                var t = C.pantry[rw.i];
                card.name = t.name;
                card.tier = t.rarity + " · Ingredient";
                card.dot = t.color;
                S.pantry[rw.i] += R.foodPerDrop;
                card.meta = "+" + R.foodPerDrop + " to your pantry · used for cooking";
                AddOwned("food:" + rw.i);
            }
            else
            {
                var t = C.tools[rw.i];
                card.name = t.name;
                card.tier = t.rarity + " · Kitchen tool";
                card.dot = t.color;
                card.isNew = !Owned(ToolKey(rw.i));
                int lv0 = KitchenLvl();
                AddOwned(ToolKey(rw.i));
                int lv1 = KitchenLvl();
                card.meta = card.isNew ? (lv1 > lv0 ? "Kitchen upgraded to level " + lv1 + "!" : "New kitchen tool · hangs on your stove rack") : "Duplicate · +" + R.dupeToolCoins + " coins";
                if (!card.isNew) card.delayedCoins = R.dupeToolCoins;
                card.kitchenChanged = true;
            }
            return card;
        }

        /// <summary>Recipes the shop sells as meal kits: common ingredients only (rare ones come from steamers).</summary>
        public bool ShopKit(RecipeData rc)
        {
            if (rc.ing.Length == 0) return false;
            foreach (var i in rc.ing) if (C.pantry[i].rarity != "Common") return false;
            return true;
        }

        public int ShopKitPrice(RecipeData rc) { return rc.ing.Length * R.shopKitCooks * R.shopKitPricePerIngredient; }

        public bool BuyKit(RecipeData rc)
        {
            if (!ShopKit(rc) || !Spend(ShopKitPrice(rc))) return false;
            foreach (var i in rc.ing) { S.pantry[i] += R.shopKitCooks; AddOwned("food:" + i); }
            return true;
        }

        // ---- tasks ----
        /// <summary>Streak or weekly-goal reward from the last claim, if any.</summary>
        public string LastRewardMessage;

        public TaskData TaskDef(TaskState t) { foreach (var d in C.tasks) if (d.id == t.id) return d; return C.tasks[0]; }

        public TaskState NewTask()
        {
            var opts = new List<TaskData>();
            foreach (var d in C.tasks) if (!S.tasks.Exists(t => t.id == d.id)) opts.Add(d);
            return new TaskState { id = Pick(opts).id };
        }

        /// <summary>Adds progress to matching tasks. Returns true if one was just finished.</summary>
        public bool TaskEvent(string id, float n)
        {
            if (S.dead) return false;
            bool fin = false;
            foreach (var t in S.tasks)
            {
                if (t.id != id || t.done || !TaskReady(t)) continue;
                var d = TaskDef(t);
                t.prog = Math.Min(d.goal, t.prog + n);
                if (t.prog >= d.goal) { t.done = true; fin = true; }
            }
            if (TasksChanged != null) TasksChanged();
            return fin;
        }

        public bool AnyTaskDone() { return S.tasks.Exists(t => t.done); }

        public bool TaskReady(TaskState t) { return t.readyAt <= Clock.UtcNow.Ticks; }

        /// <summary>Time until a resting slot gets its next task.</summary>
        public TimeSpan TaskWait(TaskState t) { return TimeSpan.FromTicks(Math.Max(0, t.readyAt - Clock.UtcNow.Ticks)); }

        /// <summary>Pays a finished task and replaces it. Returns true when the set earns its steamers.</summary>
        public bool ClaimTask(int i)
        {
            var t = S.tasks[i];
            if (!t.done) return false;
            AddCoins(TaskDef(t).coins);
            S.setDone++;
            LastRewardMessage = RecordTaskDone();
            var next = NewTask();
            next.readyAt = Clock.UtcNow.Ticks + TimeSpan.FromHours(R.taskCooldownHours).Ticks; // rate-limited: one new task per slot every few hours
            S.tasks[i] = next;
            bool steamer = false;
            if (S.setDone >= R.tasksPerSteamer) { S.setDone = 0; SetSteamers(S.steamers + R.taskSetSteamers); steamer = true; }
            if (TasksChanged != null) TasksChanged();
            return steamer;
        }
    }
}
