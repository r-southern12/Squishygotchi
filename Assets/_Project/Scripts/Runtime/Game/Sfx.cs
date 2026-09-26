using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// The prototype's synthesised sounds (WebAudio tone sweeps and filtered noise), rendered to clips once,
    /// plus the steam hum while charging a steamer. Off until the player turns sound on.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int Rate = 44100;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource _src;
        private Hum _hum;
        private AmbientMusic _music;
        public bool SoundOn;

        /// <summary>Calm generative background music; plays only while sound is on.</summary>
        public bool MusicOn { get { return _musicOn; } set { _musicOn = value; } }
        private bool _musicOn = true;

        private void Update() { if (_music != null) _music.Volume = SoundOn && _musicOn ? .16f : 0f; }

        private void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            var humGo = new GameObject("Hum");
            humGo.transform.SetParent(transform);
            humGo.AddComponent<AudioSource>().playOnAwake = false;
            _hum = humGo.AddComponent<Hum>();
            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(transform);
            musicGo.AddComponent<AudioSource>().playOnAwake = false;
            _music = musicGo.AddComponent<AmbientMusic>();
        }

        public void Tap() { Play("tap", c => Tone(c, 0, 660, 880, .09f, 'T', .12f)); }
        public void Hop() { Play("hop", c => Tone(c, 0, 420, 560, .06f, 'S', .05f)); }
        public void Lift() { Play("lift", c => Tone(c, 0, 500, 760, .12f, 'S', .12f)); }
        public void Drop() { Play("drop", c => { Tone(c, 0, 260, 140, .14f, 'S', .2f); Noise(c, 0, .08f, 500, 200, .12f, 1.2f); }); }
        public void Snap() { Play("snap", c => Tone(c, 0, 1100, 1100, .04f, 'T', .05f)); }
        public void Thunk() { Play("thunk", c => { Tone(c, 0, 140, 60, .25f, 'S', .35f); Noise(c, 0, .15f, 400, 120, .25f, 1.2f); }); }
        public void Bonk() { Play("bonk", c => Tone(c, 0, 520, 300, .08f, 'T', .14f)); }
        public void Pop() { Play("pop", c => { Noise(c, 0, .6f, 2400, 200, .5f, .8f); Tone(c, 0, 900, 180, .35f, 'T', .25f); Tone(c, .12f, 1200, 1600, .25f, 'S', .12f); }); }
        public void Land() { Play("land", c => { Tone(c, 0, 320, 110, .3f, 'S', .3f); Tone(c, .14f, 520, 780, .18f, 'S', .12f); }); }
        public void Squish() { Play("squish", c => Noise(c, 0, .3f, 700, 220, .35f, 2.5f)); }
        public void Kick() { Play("kick", c => { Tone(c, 0, 300, 160, .12f, 'T', .25f); Noise(c, 0, .06f, 900, 300, .15f, 1.2f); }); }
        public void Coin() { Play("coin", c => { Tone(c, 0, 990, 1320, .12f, 'T', .06f); Tone(c, .08f, 1320, 1760, .14f, 'T', .05f); }); }
        public void Sad() { Play("sad", c => { float[] f = { 520, 440, 370, 300 }; for (int i = 0; i < 4; i++) Tone(c, i * .26f, f[i], f[i] * .97f, .4f, 'S', .12f); }); }
        public void Chime() { Play("chime", c => { float[] f = { 660, 880, 1320 }; for (int i = 0; i < 3; i++) Tone(c, i * .09f, f[i], f[i], .35f, 'S', .1f); }); }

        /// <summary>A xylophone note (a major pentatonic run, one clip per bar).</summary>
        public void Note(int bar)
        {
            float[] f = { 523, 587, 659, 784, 880, 1047 };
            float fr = f[Mathf.Clamp(bar, 0, 5)];
            Play("note" + bar, c => { Tone(c, 0, fr, fr * .99f, .5f, 'S', .16f); Tone(c, 0, fr * 2, fr * 2, .2f, 'T', .04f); });
        }

        /// <summary>humUpdate(level): 0 fades the hum out.</summary>
        public void Hum(float level) { _hum.Level = SoundOn ? level : 0; }

        private void Play(string name, System.Action<List<float>> build)
        {
            if (!SoundOn) return;
            if (!_clips.TryGetValue(name, out var clip))
            {
                var buf = new List<float>();
                build(buf);
                Soften(buf);
                clip = AudioClip.Create(name, Mathf.Max(1, buf.Count), 1, Rate, false);
                clip.SetData(buf.ToArray(), 0);
                _clips[name] = clip;
            }
            _src.PlayOneShot(clip);
        }

        /// <summary>Gentler, roomier sound: quieter, a soft low-pass and a short three-tap echo.</summary>
        private static void Soften(List<float> b)
        {
            int tail = Rate / 3;
            for (int i = 0; i < tail; i++) b.Add(0);
            float lp = 0;
            for (int i = 0; i < b.Count; i++) { lp += .45f * (b[i] - lp); b[i] = lp * .7f; }
            int[] taps = { Rate / 14, Rate / 9, Rate / 6 };
            float[] gains = { .22f, .14f, .08f };
            for (int i = b.Count - 1; i >= 0; i--)
                for (int t = 0; t < 3; t++) if (i - taps[t] >= 0) b[i] += b[i - taps[t]] * gains[t];
        }

        private static void Ensure(List<float> b, int n) { while (b.Count < n) b.Add(0); }

        /// <summary>tone(f1, f2, dur, type, vol): exponential sweep with a 12 ms exponential attack and decay.</summary>
        private static void Tone(List<float> b, float at, float f1, float f2, float dur, char type, float vol)
        {
            int start = (int)(at * Rate), n = (int)((dur + .02f) * Rate);
            Ensure(b, start + n);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = Mathf.Clamp01(t / dur);
                float f = f1 * Mathf.Pow(f2 / f1, u);
                ph += f / Rate;
                float g = t < .012f ? .0001f * Mathf.Pow(vol / .0001f, t / .012f) : t < dur ? vol * Mathf.Pow(.0001f / vol, (t - .012f) / (dur - .012f)) : 0;
                float p = (float)(ph % 1.0), w;
                switch (type)
                {
                    case 'T': w = 1 - 4 * Mathf.Abs(p - .5f); w = -w; break;
                    case 'T': w = p < .5f ? 1 : -1; break;
                    case 'W': w = 2 * p - 1; break;
                    default: w = Mathf.Sin(2 * Mathf.PI * p); break;
                }
                b[start + i] += w * g;
            }
        }

        /// <summary>noise(dur, f1, f2, vol, q): fading white noise through a swept band-pass filter.</summary>
        private static void Noise(List<float> b, float at, float dur, float f1, float f2, float vol, float q)
        {
            int start = (int)(at * Rate), n = (int)(dur * Rate);
            Ensure(b, start + n);
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n, f = f1 * Mathf.Pow(f2 / f1, u);
                float w0 = 2 * Mathf.PI * f / Rate, alpha = Mathf.Sin(w0) / (2 * q), cs = Mathf.Cos(w0);
                float a0 = 1 + alpha, b0 = alpha / a0, b2 = -alpha / a0, a1 = -2 * cs / a0, a2 = (1 - alpha) / a0;
                float x = (Random.value * 2 - 1) * (1 - u);
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                b[start + i] += y * vol;
            }
        }
    }

    /// <summary>The charging hum: a sawtooth and a sine an octave up through a 700 Hz low-pass, smoothed like setTargetAtTime.</summary>
    public sealed class Hum : MonoBehaviour
    {
        public float Level;
        private double _p1, _p2;
        private float _f1 = 90, _f2 = 180, _g, _lp;
        private int _rate;

        private void Awake()
        {
            _rate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            src.loop = true;
            src.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float level = Level;
            float tf1 = 90 + level * 260, tf2 = 180 + level * 520, tg = level > 0 ? .04f + level * .07f : 0;
            float k = 1 - Mathf.Exp(-1f / (.05f * _rate));
            float rc = 1f / (2 * Mathf.PI * 700), dtS = 1f / _rate, a = dtS / (rc + dtS);
            for (int i = 0; i < data.Length; i += channels)
            {
                _f1 += (tf1 - _f1) * k;
                _f2 += (tf2 - _f2) * k;
                _g += (tg - _g) * k;
                _p1 = (_p1 + _f1 / _rate) % 1.0;
                _p2 = (_p2 + _f2 / _rate) % 1.0;
                float s = (float)(2 * _p1 - 1) + Mathf.Sin((float)(2 * Mathf.PI * _p2));
                _lp += a * (s - _lp);
                float v = _lp * _g;
                for (int c = 0; c < channels; c++) data[i + c] = v;
            }
        }
    }

    /// <summary>navigator.vibrate: short buzzes on phones.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        public static void Buzz(params int[] ms)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ms.Length == 0) Handheld.Vibrate(); // keeps the VIBRATE permission in the manifest
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vib = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (ms.Length == 1) vib.Call("vibrate", (long)ms[0]);
                    else
                    {
                        var pattern = new long[ms.Length + 1];
                        for (int i = 0; i < ms.Length; i++) pattern[i + 1] = ms[i];
                        vib.Call("vibrate", pattern, -1);
                    }
                }
            }
            catch (System.Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
            int total = 0;
            foreach (var m in ms) total += m;
            if (total >= 25) Handheld.Vibrate();
#endif
        }
    }
}
