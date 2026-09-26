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
        public int version = 1, favIdx, copies, roomLv, curSkin, age;
        public string code, avatar, hat, face, neck;
        public float[] needs;
        public List<PieceState> items = new List<PieceState>();
    }

    /// <summary>Friends: snapshots for visiting, a stand-in state to host a visit in, and coins for being visited.</summary>
    public sealed partial class GameRules
    {
        public RoomSnapshot Snapshot()
        {
            var s = new RoomSnapshot
            {
                favIdx = S.favIdx, copies = Math.Max(1, SquishCount(S.favIdx)), roomLv = S.roomLv, curSkin = S.curSkin, age = S.age,
                code = EnsureFriendCode(), avatar = S.avatar, hat = S.hat, face = S.face, neck = S.neck,
                needs = (float[])S.needs.Clone(),
            };
            foreach (var p in S.items) s.items.Add(new PieceState { key = p.key, x = p.x, z = p.z, ry = p.ry, wilt = p.wilt, lampOn = p.lampOn });
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
                favIdx = fav, roomLv = Math.Max(0, Math.Min(c.roomLevels.Length - 1, snap.roomLv)), curSkin = snap.curSkin, age = Math.Max(1, snap.age),
                hat = snap.hat ?? "", face = snap.face ?? "", neck = snap.neck ?? "",
                needs = snap.needs != null && snap.needs.Length == 4 ? (float[])snap.needs.Clone() : new[] { .7f, .7f, .7f, .7f },
                pantry = new int[c.pantry.Length], snacks = new int[c.snacks.Length], toolDur = new int[c.tools.Length],
                toolSkin = new int[c.tools.Length], recipeXP = new int[c.recipes.Length], trialStart = DateTime.UtcNow.Ticks, premium = true,
            };
            s.squishOwned.Add(new CountData { i = fav, n = Math.Max(1, snap.copies) });
            foreach (var p in snap.items)
                if (p != null && !string.IsNullOrEmpty(p.key) && (p.key.StartsWith("tomb") || c.Catalogue.Exists(e => e.key == p.key || p.key.StartsWith(e.arch + ":"))))
                    s.items.Add(new PieceState { key = p.key, x = p.x, z = p.z, ry = p.ry, wilt = p.wilt, lampOn = p.lampOn });
            return s;
        }

        /// <summary>
        /// A friend visited and cared for your squishy: pay coins for each thing they did, once per visit.
        /// Returns the coins paid (0 if this visit was already counted).
        /// </summary>
        public int CreditVisit(string friendId, long at, int acts)
        {
            if (string.IsNullOrEmpty(friendId) || acts <= 0) return 0;
            var done = S.visitsCredited.Find(v => v.id == friendId);
            if (done != null && done.at >= at) return 0;
            if (done == null) S.visitsCredited.Add(done = new VisitCredit { id = friendId });
            done.at = at;
            int coins = Math.Min(acts, 3) * R.visitHostCoins;
            AddCoins(coins);
            return coins;
        }

        public void RememberFriend(string id, RoomSnapshot snap)
        {
            if (string.IsNullOrEmpty(id)) return;
            var f = S.friends.Find(x => x.id == id);
            if (f == null) S.friends.Add(f = new FriendData { id = id });
            f.code = snap.code;
            f.avatar = snap.avatar;
            f.finish = snap.favIdx;
        }
    }
}
