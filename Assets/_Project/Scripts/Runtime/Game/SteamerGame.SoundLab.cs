using System.Collections.Generic;
using System.Text;
using Squishy.Runtime.UI;
using Squishy.Simulation.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Sound Lab (admin): choose the sound for every moment in the game, in the game. Arrows step through the best
    /// guesses first, then every other clip (any clip can go to any moment); tapping the name plays it. Picks are
    /// saved, and "Copy picks" puts them on the clipboard to paste back so they become the defaults.
    /// </summary>
    public sealed partial class SteamerGame
    {
        private void ApplySoundPicks()
        {
            foreach (var p in S.soundPicks) sfx.Bank.SetPick(p.ev, p.clip, p.vol);
        }

        public void OnSoundLab()
        {
            ui.SetPanelTitle("info", "Sound Lab");
            var body = ui.PanelBody("info");
            if (!S.soundOn) Hud.Para(body, "Sound is off: switch it on in Settings to hear anything.").Margin(0, 0, 8, 0);
            Hud.Para(body, "Arrows try the next sound for that moment (best guesses first, then every other clip). Tap a name to hear it again. − and + set its volume.");
            Row(body, "When you're happy", ("Copy picks", CopySoundPicks), ("Reset all", () => { S.soundPicks.Clear(); foreach (var e in sfx.Bank.Table.events) sfx.Bank.SetPick(e.id, null, -1); for (int i = 0; i < C.skins.Length; i++) sfx.Bank.SetPick("music:" + i, null, -1); UpdateMusic(); WriteSave(); OnSoundLab(); }), ("Back", OnAdmin));
            Css.Label(body, "Music", "Gluten", 800, 18).Margin(12, 0, 0, 0);
            Hud.Para(body, S.musicOn && S.soundOn ? "One track per steamer skin. Arrows try the next track; tap the name to listen. Style and artist are shown under each." : "Switch Music on in Settings to hear the tracks.");
            for (int i = 0; i < C.skins.Length; i++) MusicRow(body, i);
            Css.Label(body, "Sounds", "Gluten", 800, 18).Margin(14, 0, 0, 0);
            foreach (var e in sfx.Bank.Table.events) LabRow(body, e);
            ui.OpenPanel("info");
        }

        private void LabRow(VisualElement body, SoundEvent e)
        {
            Css.Label(body, e.name, "Gluten", 800, 14).Margin(10, 0, 2, 0);
            var row = new VisualElement().Row(Align.Center).In(body);
            row.Gap(4);
            var all = LabOrder(e);
            Label nameLbl = null, volLbl = null;
            void Refresh()
            {
                var (clip, vol) = sfx.Bank.Current(e.id);
                int at = all.IndexOf(clip);
                bool guess = at >= 0 && at < e.options.Length;
                nameLbl.text = (clip == "none" ? "(silent)" : clip) + (guess ? "" : " *");
                volLbl.text = Mathf.RoundToInt(vol * 100) + "%";
            }
            void Hear() { var (clip, vol) = sfx.Bank.Current(e.id); if (e.id == "note") sfx.Note(Random.Range(0, 6)); else sfx.PlayClip(e.id, clip, vol); }
            void Set(string clip, float vol)
            {
                sfx.Bank.SetPick(e.id, clip, vol);
                S.soundPicks.RemoveAll(p => p.ev == e.id);
                S.soundPicks.Add(new SoundPick { ev = e.id, clip = clip, vol = vol });
                WriteSave();
                Refresh();
            }
            void Step(int d)
            {
                var (clip, vol) = sfx.Bank.Current(e.id);
                int at = Mathf.Max(0, all.IndexOf(clip));
                Set(all[(at + d + all.Count) % all.Count], vol);
                Hear();
            }
            void Vol(float d) { var (clip, vol) = sfx.Bank.Current(e.id); Set(clip, Mathf.Clamp(Mathf.Round((vol + d) * 20) / 20, .05f, 1)); Hear(); }

            Hud.Button(row, "‹", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 18, () => Step(-1)).Size(38, null);
            var mid = Hud.Button(row, "", "#FFF1DD", "#E2CFB3", Hud.Ink, 12, 36, 13, Hear);
            mid.style.flexGrow = 1;
            nameLbl = (Label)mid.userData;
            Hud.Button(row, "›", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 18, () => Step(1)).Size(38, null);
            Hud.Button(row, "−", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 16, () => Vol(-.05f)).Size(32, null);
            volLbl = Css.Label(row, "", "Figtree", 700, 12);
            volLbl.style.width = 34;
            volLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            Hud.Button(row, "+", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 16, () => Vol(.05f)).Size(32, null);
            Refresh();
        }

        private void MusicRow(VisualElement body, int skin)
        {
            Css.Label(body, C.skins[skin].name + " steamer", "Gluten", 800, 14).Margin(10, 0, 2, 0);
            var row = new VisualElement().Row(Align.Center).In(body);
            row.Gap(4);
            var tracks = sfx.Bank.Table.music;
            Label nameLbl = null, styleLbl = null;
            void Refresh()
            {
                var t = sfx.Bank.Track(SkinTrack(skin));
                nameLbl.text = t != null ? t.name : SkinTrack(skin);
                styleLbl.text = t != null ? t.style : "";
            }
            void Step(int d)
            {
                int at = 0;
                for (int k = 0; k < tracks.Length; k++) if (tracks[k].id == SkinTrack(skin)) at = k;
                string id = tracks[(at + d + tracks.Length) % tracks.Length].id;
                sfx.Bank.SetPick("music:" + skin, id, -1);
                S.soundPicks.RemoveAll(p => p.ev == "music:" + skin);
                S.soundPicks.Add(new SoundPick { ev = "music:" + skin, clip = id, vol = -1 });
                WriteSave();
                Refresh();
                sfx.PlayMusic(id);
            }
            Hud.Button(row, "‹", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 18, () => Step(-1)).Size(38, null);
            var mid = Hud.Button(row, "", "#FFF1DD", "#E2CFB3", Hud.Ink, 12, 36, 13, () => sfx.PlayMusic(SkinTrack(skin)));
            mid.style.flexGrow = 1;
            nameLbl = (Label)mid.userData;
            Hud.Button(row, "›", "#EADCC6", "#CDB999", Hud.Ink, 12, 36, 18, () => Step(1)).Size(38, null);
            styleLbl = Css.Label(body, "", "Figtree", 600, 11.5f, Hud.Muted);
            Refresh();
        }

        /// <summary>This moment's guesses first, then silence and every other clip.</summary>
        private List<string> LabOrder(SoundEvent e)
        {
            var list = new List<string>(e.options);
            foreach (var o in sfx.Bank.AllOptions()) if (!list.Contains(o)) list.Add(o);
            return list;
        }

        private void CopySoundPicks()
        {
            var sb = new StringBuilder("Squishiotchi sound picks\n");
            foreach (var e in sfx.Bank.Table.events)
            {
                var (clip, vol) = sfx.Bank.Current(e.id);
                sb.Append(e.id).Append(": ").Append(clip).Append(" @ ").Append(Mathf.RoundToInt(vol * 100)).Append("%\n");
            }
            for (int i = 0; i < C.skins.Length; i++) sb.Append("music ").Append(C.skins[i].name).Append(": ").Append(SkinTrack(i)).Append('\n');
            GUIUtility.systemCopyBuffer = sb.ToString();
            ui.FloatAt(new Vector2(ui.Width / 2, ui.Height * .4f), "Copied · paste it to Claude");
        }
    }
}
