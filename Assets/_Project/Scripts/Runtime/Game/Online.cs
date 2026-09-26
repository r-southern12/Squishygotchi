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
    /// with Authentication and Cloud Save on, and Cloud Save indexes on the public keys "code" and "visitTo"
    /// (docs/release.md). Everything fails soft: without a connection the game simply plays offline.
    /// </summary>
    public static class Online
    {
        public static bool Ready { get; private set; }
        public static string PlayerId { get; private set; }
        public static string Status { get; private set; } = "Connecting…";

        private const string KCode = "code", KRoom = "room", KVisitTo = "visitTo", KVisitAt = "visitAt", KVisitActs = "visitActs";
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

        /// <summary>Shares your friend code and a snapshot of your room so friends can visit.</summary>
        public static async Task Publish(RoomSnapshot snap)
        {
            if (!Ready) return;
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { KCode, snap.code },
                    { KRoom, JsonUtility.ToJson(snap) },
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
        public static async Task RecordVisit(string friendId, int acts)
        {
            if (!Ready) return;
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { KVisitTo, friendId },
                    { KVisitAt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    { KVisitActs, acts },
                }, new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new PublicWriteAccessClassOptions()));
            }
            catch (Exception e) { Debug.LogWarning("Online visit: " + e.Message); }
        }

        /// <summary>Friends whose latest visit was to you: (their id, when, how many caring things).</summary>
        public static async Task<List<(string id, long at, int acts)>> Visitors()
        {
            var list = new List<(string, long, int)>();
            if (!Ready) return list;
            try
            {
                var query = new Query(new List<FieldFilter> { new FieldFilter(KVisitTo, PlayerId, FieldFilter.OpOptions.EQ, true) }, new HashSet<string> { KVisitAt, KVisitActs }, 0, 50);
                var found = await CloudSaveService.Instance.Data.Player.QueryAsync(query, new QueryOptions());
                foreach (var e in found)
                {
                    long at = 0;
                    int acts = 0;
                    foreach (var item in e.Data)
                    {
                        if (item.Key == KVisitAt) at = item.Value.GetAs<long>();
                        else if (item.Key == KVisitActs) acts = item.Value.GetAs<int>();
                    }
                    list.Add((e.Id, at, acts));
                }
            }
            catch (Exception e) { Debug.LogWarning("Online visitors: " + e.Message); }
            return list;
        }
    }
}
