using System;
using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    [Serializable] public sealed class SoundEvent { public string id, name; public float vol, len; public bool loop; public string[] options; }
    [Serializable] public sealed class SliceCount { public string clip; public int n; }
    [Serializable] public sealed class SoundTable { public string[] clips; public SliceCount[] slices; public SoundEvent[] events; }

    /// <summary>
    /// Recorded sound clips (Resources/Sfx) and which one plays for each sound event (Resources/Content/sounds.json,
    /// overridden by the Sound Lab picks in the save). "clip#n" is the n-th separate sound found in a longer
    /// recording, cut out automatically with soft fades, so one ASMR recording offers several options.
    /// </summary>
    public sealed class SoundBank
    {
        public readonly SoundTable Table;
        private readonly Dictionary<string, SoundEvent> _events = new Dictionary<string, SoundEvent>();
        private readonly Dictionary<string, int> _slices = new Dictionary<string, int>();
        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, (string clip, float vol)> _picks = new Dictionary<string, (string, float)>();

        public SoundBank()
        {
            var ta = Resources.Load<TextAsset>("Content/sounds");
            Table = ta != null ? JsonUtility.FromJson<SoundTable>(ta.text) : new SoundTable { clips = new string[0], slices = new SliceCount[0], events = new SoundEvent[0] };
            foreach (var e in Table.events) _events[e.id] = e;
            foreach (var s in Table.slices) _slices[s.clip] = s.n;
        }

        public SoundEvent Event(string id) { _events.TryGetValue(id, out var e); return e; }

        /// <summary>Every option the Sound Lab can assign: silence, then every clip (and each cut of the longer ones).</summary>
        public List<string> AllOptions()
        {
            var list = new List<string> { "none" };
            foreach (var c in Table.clips)
            {
                int n = _slices.TryGetValue(c, out var k) ? k : 1;
                if (n <= 1) list.Add(c);
                else for (int i = 1; i <= n; i++) list.Add(c + "#" + i);
            }
            return list;
        }

        public void SetPick(string ev, string clip, float vol)
        {
            if (clip == null) _picks.Remove(ev); else _picks[ev] = (clip, vol);
        }

        /// <summary>The clip and volume for an event: the player's pick, else the data's first option.</summary>
        public (string clip, float vol) Current(string ev)
        {
            var e = Event(ev);
            float dv = e != null ? e.vol : .4f;
            if (_picks.TryGetValue(ev, out var p)) return (p.clip, p.vol >= 0 ? p.vol : dv);
            return (e != null && e.options != null && e.options.Length > 0 ? e.options[0] : "none", dv);
        }

        /// <summary>The audio for an option, cut to the event's length (cached).</summary>
        public AudioClip Get(string option, float len, bool loop)
        {
            if (string.IsNullOrEmpty(option) || option == "none") return null;
            string key = option + "|" + len + "|" + loop;
            if (_cache.TryGetValue(key, out var hit)) return hit;
            string file = option;
            int nth = 1, hash = option.IndexOf('#');
            if (hash >= 0) { file = option.Substring(0, hash); int.TryParse(option.Substring(hash + 1), out nth); }
            var src = Resources.Load<AudioClip>("Sfx/" + file);
            AudioClip clip = null;
            if (src != null && src.LoadAudioData())
            {
                clip = Cut(src, Mathf.Max(1, nth), len, loop, option);
                Resources.UnloadAsset(src);
            }
            _cache[key] = clip;
            return clip;
        }

        /// <summary>Finds the n-th onset (a rise out of quiet) and cuts len seconds from it with soft fades.</summary>
        private static AudioClip Cut(AudioClip src, int nth, float len, bool loop, string name)
        {
            int ch = src.channels, n = src.samples, f = src.frequency;
            var raw = new float[n * ch];
            src.GetData(raw, 0);
            var mono = new float[n];
            for (int i = 0; i < n; i++) { float s = 0; for (int c = 0; c < ch; c++) s += raw[i * ch + c]; mono[i] = s / ch; }

            int win = Mathf.Max(1, f / 100), nw = n / win;
            var rms = new float[nw];
            float peak = 1e-6f;
            for (int w = 0; w < nw; w++)
            {
                float s = 0;
                for (int i = w * win; i < (w + 1) * win; i++) s += mono[i] * mono[i];
                rms[w] = Mathf.Sqrt(s / win);
                peak = Mathf.Max(peak, rms[w]);
            }
            float thr = peak * .12f;
            var onsets = new List<int>();
            int quiet = 99;
            for (int w = 0; w < nw; w++)
            {
                if (rms[w] > thr) { if (quiet >= 8) onsets.Add(w); quiet = 0; }
                else if (rms[w] < thr * .5f) quiet++;
            }
            if (onsets.Count == 0) onsets.Add(0);
            int start = Mathf.Max(0, (onsets[(nth - 1) % onsets.Count] - 1) * win);
            int length = Mathf.Clamp(Mathf.RoundToInt(len * f), win, n - start);
            var cut = new float[length];
            Array.Copy(mono, start, cut, 0, length);

            // Fades: a tiny one in, a long gentle one out (or short ones both ends for a loop).
            int fin = loop ? f / 25 : f / 200, fout = loop ? f / 25 : length / 4;
            for (int i = 0; i < fin && i < length; i++) cut[i] *= i / (float)fin;
            for (int i = 0; i < fout && i < length; i++) { float t = i / (float)fout; cut[length - 1 - i] *= .5f - .5f * Mathf.Cos(t * Mathf.PI); }
            // Gentle level: every clip peaks at the same height, so the event volume means the same thing for all.
            float pk = 1e-4f;
            foreach (var s in cut) pk = Mathf.Max(pk, Mathf.Abs(s));
            float g = .8f / pk;
            for (int i = 0; i < length; i++) cut[i] *= g;

            var clip = AudioClip.Create(name, length, 1, f, false);
            clip.SetData(cut, 0);
            return clip;
        }
    }
}
