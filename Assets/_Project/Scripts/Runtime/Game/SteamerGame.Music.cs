using System;
using Squishy.Runtime.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Music: every steamer skin unlocks its own track. By default the music follows the steamer in use; the player
    /// can also pick any unlocked track in Settings. On a visit you hear your friend's steamer.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private string SkinTrack(int i) { return sfx.Bank.MusicFor(i, C.skins[i].music); }

        private string TrackName(string id) { var t = sfx.Bank.Track(id); return t != null ? t.name : id; }

        private int MusicSkin()
        {
            int cur = Array.IndexOf(C.skins, curSkin);
            if (visiting) return cur;
            var open = Rules.UnlockedTracks();
            if (!string.IsNullOrEmpty(S.track))
                foreach (int i in open) if (SkinTrack(i) == S.track) return i;
            return open.Contains(cur) ? cur : open.Count > 0 ? open[0] : -1;
        }

        private void UpdateMusic()
        {
            int i = MusicSkin();
            sfx.PlayMusic(i >= 0 ? SkinTrack(i) : null);
        }

        /// <summary>Settings row: taps cycle through "Match steamer" and every unlocked track.</summary>
        private void TrackRow(VisualElement body)
        {
            var open = Rules.UnlockedTracks();
            var row = new VisualElement().Row(Align.Center, Justify.SpaceBetween).In(body);
            row.style.marginBottom = 8;
            Css.Label(row, "Track", "Figtree", 700, 15);
            string label = string.IsNullOrEmpty(S.track) ? "Match steamer" : TrackName(SkinTrack(Mathf.Max(0, MusicSkin())));
            Hud.Button(row, label + "  ›", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 14, () =>
            {
                int at = -1;
                for (int k = 0; k < open.Count; k++) if (SkinTrack(open[k]) == S.track) at = k;
                S.track = at + 1 < open.Count ? SkinTrack(open[at + 1]) : "";
                UpdateMusic();
                WriteSave();
                OnSettings();
            }).Size(190, null);
            Css.Label(body, open.Count + " of " + C.skins.Length + " tracks · each steamer skin unlocks one", "Figtree", 600, 12, Hud.Muted).Margin(-4, 0, 8, 0);
        }
    }
}
