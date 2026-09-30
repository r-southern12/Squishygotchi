using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Squishy.Simulation.Game;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Friends online (Unity Gaming Services): an anonymous sign-in (no account, no email), and public Cloud Save
    /// player data holding your friend code, a snapshot of your room and squishy, and your latest visit. Friends
    /// find each other by code and never see personal details. Needs the project linked to a Unity Cloud project
    /// with Authentication and Cloud Save on, and Cloud Save indexes on the public keys "code", "visitTo" and "lastSeen"
    /// (docs/release.md). Everything fails soft: without a connection the game simply plays offline.
    /// </summary>
    public static class Online
    {
        public static bool Ready { get; private set; }
        public static string PlayerId { get; private set; }
        public static string Status { get; private set; } = "Connecting…";

        private const string KCode = "code", KRoom = "room", KVisitTo = "visitTo", KVisitAt = "visitAt", KVisitActs = "visitActs", KVisitWhat = "visitWhat", KVisitName = "visitName", KSeen = "lastSeen";
        private static Task _init;

        public static Task Init()
        {
            return _init ?? (_init = InitAsync());
        }

        private static async Task InitAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerId = AuthenticationService.Instance.PlayerId;
                Ready = true;
                Status = "Online";
            }
            catch (Exception e)
            {
                Ready = false;
                Status = e.GetType().Name.Contains("NotLinked") || e.Message.Contains("project")
                    ? "Friends aren't switched on in this test build yet."
                    : "Couldn't reach the friends service. Check your internet connection.";
                Debug.LogWarning("Online: " + e.Message);
                _init = null; // try again next time
            }
        }

        /// <summary>
        /// Shares your friend code and a snapshot of your room so friends can visit, and when you last played so
        /// other active players can find you as a neighbour (0 when you've switched that off in Settings).
        /// </summary>
        public static async Task Publish(RoomSnapshot snap, bool findable)
        {
            if (!Ready) return;
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { KCode, snap.code },
                    { KRoom, JsonUtility.ToJson(snap) },
                    { KSeen, findable ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0L },
                }, new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions()));
            }
            catch (Exception e) { Debug.LogWarning("Online publish: " + e.Message); }
        }

        /// <summary>Finds a friend by code. Returns their player id and room, or null.</summary>
        public static async Task<(string id, RoomSnapshot room)?> Find(string code)
        {
            if (!Ready || string.IsNullOrEmpty(code)) return null;
            try
            {
                var query = new Query(new List<FieldFilter> { new FieldFilter(KCode, code.Trim().ToUpperInvariant(), FieldFilter.OpOptions.EQ, true) }, new HashSet<string> { KRoom });
                var found = await CloudSaveService.Instance.Data.Player.QueryAsync(query, new QueryOptions());
                foreach (var e in found)
                {
                    if (e.Id == PlayerId) continue;
                    foreach (var item in e.Data)
                        if (item.Key == KRoom) return (e.Id, JsonUtility.FromJson<RoomSnapshot>(item.Value.GetAs<string>()));
                }
            }
            catch (Exception e) { Debug.LogWarning("Online find: " + e.Message); }
            return null;
        }

        /// <summary>
        /// Random players who played in the last few days (never you, never anyone in skip): up to count of them, each
        /// with their room. There's no ranking: it's a fresh random pick every time.
        /// </summary>
        public static async Task<List<(string id, RoomSnapshot room)>> Strangers(int count, int activeDays, HashSet<string> skip)
        {
            var list = new List<(string, RoomSnapshot)>();
            if (!Ready || count <= 0) return list;
            try
            {
                long since = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - activeDays * 86400L;
                int sample = Math.Min(50, count + skip.Count + 1);
                var query = new Query(new List<FieldFilter> { new FieldFilter(KSeen, since, FieldFilter.OpOptions.GE, true) }, new HashSet<string> { KRoom }, 0, sample, sample);
                var found = await CloudSaveService.Instance.Data.Player.QueryAsync(query, new QueryOptions());
                foreach (var e in found)
                {
                    if (e.Id == PlayerId || skip.Contains(e.Id) || list.Count >= count) continue;
                    foreach (var item in e.Data)
                        if (item.Key == KRoom) { var room = JsonUtility.FromJson<RoomSnapshot>(item.Value.GetAs<string>()); if (room != null) list.Add((e.Id, room)); }
                }
            }
            catch (Exception e) { Debug.LogWarning("Online strangers: " + e.Message); }
            return list;
        }

        /// <summary>A friend's latest room, by player id.</summary>
        public static async Task<RoomSnapshot> Load(string playerId)
        {
            if (!Ready) return null;
            try
            {
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { KRoom }, new LoadOptions(new PublicReadAccessClassOptions(playerId)));
                if (data.TryGetValue(KRoom, out var item)) return JsonUtility.FromJson<RoomSnapshot>(item.Value.GetAs<string>());
            }
            catch (Exception e) { Debug.LogWarning("Online load: " + e.Message); }
            return null;
        }

        /// <summary>Records that you visited a friend and how many caring things you did (they're paid when they next open the game).</summary>
        public static async Task RecordVisit(string friendId, int acts, string what = "", string name = "")
        {
            if (!Ready) return;
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { KVisitTo, friendId },
                    { KVisitAt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    { KVisitActs, acts },
                    { KVisitWhat, what ?? "" },
                    { KVisitName, name ?? "" },
                }, new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions()));
            }
            catch (Exception e) { Debug.LogWarning("Online visit: " + e.Message); }
        }

        /// <summary>Friends whose latest visit was to you: (their id, when, how many caring things).</summary>
        public static async Task<List<(string id, long at, int acts, string what, string name)>> Visitors()
        {
            var list = new List<(string, long, int, string, string)>();
            if (!Ready) return list;
            try
            {
                var query = new Query(new List<FieldFilter> { new FieldFilter(KVisitTo, PlayerId, FieldFilter.OpOptions.EQ, true) }, new HashSet<string> { KVisitAt, KVisitActs, KVisitWhat, KVisitName }, 0, 50);
                var found = await CloudSaveService.Instance.Data.Player.QueryAsync(query, new QueryOptions());
                foreach (var e in found)
                {
                    long at = 0;
                    int acts = 0;
                    string what = "", name = "";
                    foreach (var item in e.Data)
                    {
                        if (item.Key == KVisitAt) at = item.Value.GetAs<long>();
                        else if (item.Key == KVisitActs) acts = item.Value.GetAs<int>();
                        else if (item.Key == KVisitWhat) what = item.Value.GetAs<string>();
                        else if (item.Key == KVisitName) name = item.Value.GetAs<string>();
                    }
                    list.Add((e.Id, at, acts, what, name));
                }
            }
            catch (Exception e) { Debug.LogWarning("Online visitors: " + e.Message); }
            return list;
        }
    }
}
