using System;
using System.Collections.Generic;

namespace Squishy.Simulation.Game
{
    /// <summary>
    /// What a friend sees when they visit: the room, the squishy (type, size, age, accessories, needs) and the
    /// steamer skin. No personal details: friends know each other only by squishy and friend code.
    /// </summary>
    [Serializable]
    public class RoomSnapshot
    {
        public int version = 1, favIdx, copies, roomLv, roomSize, curSkin, age;
        public string code, avatar, hat, face, neck;
        public float[] needs;
        public List<PieceState> items = new List<PieceState>();
        public List<CosColor> colors = new List<CosColor>(); // accessory colours
    }

    /// <summary>Friends: snapshots for visiting, a stand-in state to host a visit in, and coins for being visited.</summary>
    public sealed partial class GameRules
    {
        public RoomSnapshot Snapshot()
        {
            var s = new RoomSnapshot
            {
                favIdx = S.favIdx, copies = Math.Max(1, SquishCount(S.favIdx)), roomLv = S.roomLv, roomSize = S.roomSize, curSkin = S.curSkin, age = S.age,
                code = EnsureFriendCode(), avatar = S.avatar, hat = S.hat, face = S.face, neck = S.neck,
                needs = (float[])S.needs.Clone(),
            };
            foreach (var p in S.items) s.items.Add(new PieceState { key = p.key, x = p.x, z = p.z, ry = p.ry, wilt = p.wilt, lampOn = p.lampOn });
            foreach (var cc in S.cosColors) s.colors.Add(new CosColor { id = cc.id, color = cc.color });
            return s;
        }

        /// <summary>
        /// A throwaway game state that shows a friend's room while you visit. It is never saved; your own state is
        /// untouched and swapped back when you leave.
        /// </summary>
        public static GameState GuestState(GameContent c, RoomSnapshot snap)
        {
            int fav = Math.Max(0, Math.Min(c.finishes.Length - 1, snap.favIdx));
            var s = new GameState
            {
                favIdx = fav, roomLv = Math.Max(0, Math.Min(c.roomLevels.Length - 1, snap.roomLv)), roomSize = Math.Max(0, Math.Min(c.steamerSizes.Length - 1, snap.roomSize)), curSkin = snap.curSkin, age = Math.Max(1, snap.age),
                hat = snap.hat ?? "", face = snap.face ?? "", neck = snap.neck ?? "",
                needs = snap.needs != null && snap.needs.Length == 4 ? (float[])snap.needs.Clone() : new[] { .7f, .7f, .7f, .7f },
                pantry = new int[c.pantry.Length], snacks = new int[c.snacks.Length], toolDur = new int[c.tools.Length],
                toolSkin = new int[c.tools.Length], recipeXP = new int[c.recipes.Length], trialStart = DateTime.UtcNow.Ticks, premium = true,
            };
            s.squishOwned.Add(new CountData { i = fav, n = Math.Max(1, snap.copies) });
            if (snap.colors != null) foreach (var cc in snap.colors) if (cc != null) s.cosColors.Add(new CosColor { id = cc.id, color = cc.color });
            foreach (var id in new[] { s.hat, s.face, s.neck }) if (!string.IsNullOrEmpty(id)) s.cosmetics.Add(id); // worn, so its colour applies
            foreach (var p in snap.items)
                if (p != null && !string.IsNullOrEmpty(p.key) && (p.key.StartsWith("tomb") || c.Catalogue.Exists(e => e.key == p.key || p.key.StartsWith(e.arch + ":"))))
                    s.items.Add(new PieceState { key = p.key, x = p.x, z = p.z, ry = p.ry, wilt = p.wilt, lampOn = p.lampOn });
            return s;
        }

        /// <summary>
        /// A friend visited and cared for your squishy: pay coins for each thing they did, once per visit.
        /// Returns the coins paid (0 if this visit was already counted).
        /// </summary>
        /// <summary>What a friend's visit did for you, for the "while you were away" card.</summary>
        public sealed class VisitGift { public string friendId, name, sticker; public int coins; public float hunger, play, together, clean; public bool watered; }

        /// <summary>
        /// A friend's visit while you were away: coins, plus the care itself carries over to your squishy
        /// (a snack tops up Hunger, a squish tops up Play, watering waters your plant). Each visit counts once.
        /// </summary>
        public VisitGift CreditVisit(string friendId, long at, int acts, string what, string name)
        {
            int coins = CreditVisit(friendId, at, acts);
            if (coins <= 0) return null;
            var g = new VisitGift { friendId = friendId, name = string.IsNullOrEmpty(name) ? "A friend" : name, coins = coins };
            what = what ?? "";
            if (what.Contains("feed")) { g.hunger = Math.Min(R.visitFeed, 1 - S.needs[Needs.Hunger]); S.needs[Needs.Hunger] += g.hunger; }
            if (what.Contains("pet")) { g.play = Math.Min(R.visitPet, 1 - S.needs[Needs.Play]); S.needs[Needs.Play] += g.play; }
            if (what.Contains("water")) { g.watered = true; foreach (var p in S.items) if (p.Arch == "plant") p.wilt = 0; }
            // Brought their squishy and played together; pampered yours; left a sticker (user request, 2 Oct 2026).
            if (what.Contains("play")) { g.together = Math.Min(R.visitPlay, 1 - S.needs[Needs.Play]); S.needs[Needs.Play] += g.together; }
            if (what.Contains("pamper")) { g.clean = Math.Min(R.visitPamper, 1 - S.needs[Needs.Clean]); S.needs[Needs.Clean] += g.clean; }
            int si = what.IndexOf("sticker:", StringComparison.Ordinal);
            if (si >= 0)
            {
                string kind = what.Substring(si + 8).Split(',')[0];
                if (!string.IsNullOrEmpty(kind)) { AddSticker(kind, g.name); g.sticker = kind; }
            }
            return g;
        }

        public int CreditVisit(string friendId, long at, int acts)
        {
            if (string.IsNullOrEmpty(friendId) || acts <= 0) return 0;
            var done = S.visitsCredited.Find(v => v.id == friendId);
            if (done != null && done.at >= at) return 0;
            if (done == null) S.visitsCredited.Add(done = new VisitCredit { id = friendId });
            done.at = at;
            int coins = Math.Min(acts, R.visitMaxActs > 0 ? R.visitMaxActs : 3) * R.visitHostCoins;
            AddCoins(coins);
            return coins;
        }

        /// <param name="byCode">Found by their friend code: a real friend (a neighbour you then add by code becomes one).</param>
        /// <summary>A friend's sticker on your floor for a day (the oldest goes when there are too many).</summary>
        public void AddSticker(string kind, string from)
        {
            PruneStickers();
            while (S.stickers.Count >= Math.Max(1, R.stickerMax)) S.stickers.RemoveAt(0);
            S.stickers.Add(new StickerState { kind = kind, from = from, until = Clock.UtcNow.AddHours(R.stickerHours > 0 ? R.stickerHours : 24).Ticks, a = (float)(Random() * Math.PI * 2), r = .45f + (float)Random() * .3f });
        }

        public void PruneStickers() { long now = Clock.UtcNow.Ticks; S.stickers.RemoveAll(x => x == null || x.until < now); }

        /// <param name="byCode">Found by their friend code: a real friend (a neighbour you then add by code becomes one).</param>
        public void RememberFriend(string id, RoomSnapshot snap, bool byCode = false)
        {
            if (string.IsNullOrEmpty(id)) return;
            var f = S.friends.Find(x => x.id == id);
            if (f == null) S.friends.Add(f = new FriendData { id = id });
            if (byCode) f.neighbour = false;
            f.code = snap.code;
            f.avatar = snap.avatar;
            f.finish = snap.favIdx;
        }

        /// <summary>How long until you can visit this friend again (zero when you can). Once every few hours each.</summary>
        public TimeSpan VisitWait(string id)
        {
            var f = S.friends.Find(x => x.id == id);
            if (f == null || f.visitedAt <= 0) return TimeSpan.Zero;
            long gap = TimeSpan.FromHours(R.visitCooldownHours).Ticks;
            long left = f.visitedAt + gap - Clock.UtcNow.Ticks;
            return TimeSpan.FromTicks(Math.Max(0, Math.Min(gap, left))); // a clock turned back never makes it longer
        }

        public void MarkVisited(string id)
        {
            var f = S.friends.Find(x => x.id == id);
            if (f != null) f.visitedAt = Clock.UtcNow.Ticks;
        }

        /// <summary>Neighbours you have (random players, not added by code).</summary>
        public int NeighbourCount()
        {
            int n = 0;
            foreach (var f in S.friends) if (f.neighbour) n++;
            return n;
        }

        public bool NeighboursFull() { return NeighbourCount() >= R.neighbourMax; }

        /// <summary>Adds a random player as a neighbour. False if they're already on your lists or you have the most neighbours.</summary>
        public bool AddNeighbour(string id, RoomSnapshot snap)
        {
            if (string.IsNullOrEmpty(id) || snap == null || S.friends.Exists(x => x.id == id) || NeighboursFull()) return false;
            S.friends.Add(new FriendData { id = id, neighbour = true });
            RememberFriend(id, snap);
            return true;
        }

        public void RemoveFriend(string id) { S.friends.RemoveAll(x => x.id == id); }
    }
}
