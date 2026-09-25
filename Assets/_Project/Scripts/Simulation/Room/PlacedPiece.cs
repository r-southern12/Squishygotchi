using System;
using System.Collections.Generic;
using Squishy.Simulation.Content;

namespace Squishy.Simulation.Room
{
    /// <summary>One piece in the room (or in storage). Skins are "itemTypeId:styleId".</summary>
    [Serializable]
    public class PlacedPiece
    {
        public int instanceId;
        public string itemTypeId;
        public string skinId;
        public float x;
        public float z;
        public float yawDegrees;
        public bool inStorage;

        public string StyleId
        {
            get
            {
                int colon = skinId == null ? -1 : skinId.IndexOf(':');
                return colon >= 0 ? skinId.Substring(colon + 1) : null;
            }
        }

        public static string SkinIdFor(string itemTypeId, string styleId)
        {
            return itemTypeId + ":" + styleId;
        }
    }

    public static class RoomRules
    {
        /// <summary>Fills an empty room with the starter layout. Returns true if anything was added.</summary>
        public static bool EnsureStarterRoom(List<PlacedPiece> pieces, List<StarterPieceDef> starter)
        {
            if (pieces.Count > 0 || starter == null) return false;
            for (int i = 0; i < starter.Count; i++)
            {
                var s = starter[i];
                pieces.Add(new PlacedPiece
                {
                    instanceId = i + 1,
                    itemTypeId = s.itemTypeId,
                    skinId = PlacedPiece.SkinIdFor(s.itemTypeId, s.styleId),
                    x = s.x,
                    z = s.z,
                    yawDegrees = s.yawDegrees,
                });
            }
            return starter.Count > 0;
        }

        /// <summary>
        /// Comfort from placed pieces: each piece's comfort, plus a set bonus when enough pieces share a style.
        /// Only one set bonus counts (the best style), as in the prototype.
        /// </summary>
        public static int Comfort(List<PlacedPiece> pieces, Func<string, ItemTypeDef> typeById, EconomyDef economy)
        {
            int comfort = 0;
            var perStyle = new Dictionary<string, int>();
            int best = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.inStorage) continue;
                var type = typeById(p.itemTypeId);
                if (type != null) comfort += type.comfort;
                string style = p.StyleId;
                if (style == null) continue;
                int n;
                perStyle.TryGetValue(style, out n);
                perStyle[style] = ++n;
                if (n > best) best = n;
            }
            if (best >= economy.styleSetSize) comfort += economy.styleSetComfortBonus;
            return comfort;
        }
    }
}
