using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Calm, soothing sound (spec: soft recorded foley only, no beeps or synth tones). Each sound event plays the
    /// clip chosen for it (SoundBank: data defaults, Sound Lab picks), quietly, with a slight random pitch so
    /// repeats don't grate. Plus the looping steam while charging a steamer, and the background music.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private SoundBank _bank;
        /// <summary>Loaded on first use (Resources can't load while a MonoBehaviour is being constructed).</summary>
        public SoundBank Bank { get { return _bank ?? (_bank = new SoundBank()); } }
        private readonly AudioSource[] _pool = new AudioSource[8];
        private int _next;
        private AudioSource _hum;
        private float _humLevel, _humVol;
        private string _humClip;
        private MusicPlayer _music;
        public bool SoundOn;

        /// <summary>Background music (a track per steamer skin); plays only while sound is on.</summary>
        public bool MusicOn { get { return _musicOn; } set { _musicOn = value; } }
        private bool _musicOn = false;

        private void Awake()
        {
            for (int i = 0; i < _pool.Length; i++) { _pool[i] = gameObject.AddComponent<AudioSource>(); _pool[i].playOnAwake = false; }
            _hum = gameObject.AddComponent<AudioSource>();
            _hum.playOnAwake = false;
            _hum.loop = true;
            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(transform);
            _music = musicGo.AddComponent<MusicPlayer>();
        }

        private void Update()
        {
            if (_music != null) _music.Volume = SoundOn && _musicOn ? .35f : 0f;
            // The steam loop eases in and out rather than cutting.
            float target = SoundOn ? _humLevel * _humVol : 0;
            _hum.volume += (target - _hum.volume) * Mathf.Min(1, Time.unscaledDeltaTime * 6);
            if (_hum.isPlaying && _hum.volume < .002f && target <= 0) _hum.Stop();
        }

        public void PlayMusic(string id) { _music.Play(id); }

        public void Tap() { Play("tap"); }
        public void Hop() { Play("hop"); }
        public void Lift() { Play("lift"); }
        public void Drop() { Play("drop"); }
        public void Snap() { Play("snap"); }
        public void Thunk() { Play("thunk"); }
        /// <summary>"Can't do that" (not enough coins, a bad code...): gentle, never a buzzer.</summary>
        public void Bonk() { Play("nope"); }
        public void Pop() { Play("pop"); }
        public void Land() { Play("land"); }
        public void Squish() { Play("squish"); }
        public void Press() { Play("press"); }
        public void Kick() { Play("kick"); }
        public void Coin() { Play("coin"); }
        public void Sad() { Play("sad"); }
        public void Cook() { Play("cook"); }
        public void Chime() { Play("chime"); }
        public void Bath() { Play("bath"); }

        /// <summary>A toy note: the note clip pitched up a major pentatonic scale, one step per bar.</summary>
        public void Note(int bar)
        {
            int[] semis = { 0, 2, 4, 7, 9, 12 };
            Play("note", Mathf.Pow(2, semis[Mathf.Clamp(bar, 0, 5)] / 12f), false);
        }

        /// <summary>Steam building while you hold a steamer: 0 fades it out.</summary>
        public void Hum(float level)
        {
            _humLevel = Mathf.Clamp01(level);
            if (_humLevel <= 0 || !SoundOn) return;
            var (clip, vol) = Bank.Current("hum");
            var e = Bank.Event("hum");
            if (clip != _humClip) { _humClip = clip; _hum.Stop(); _hum.clip = Bank.Get(clip, e != null ? e.len : 3, true); }
            _humVol = vol;
            _hum.pitch = .9f + .2f * _humLevel;
            if (_hum.clip != null && !_hum.isPlaying) { _hum.volume = 0; _hum.Play(); }
        }

        public void Play(string ev, float pitch = 1, bool vary = true)
        {
            if (!SoundOn) return;
            var (clip, vol) = Bank.Current(ev);
            PlayClip(ev, clip, vol, pitch, vary);
        }

        /// <summary>Plays a specific clip as if it were chosen for an event (the Sound Lab's preview).</summary>
        public void PlayClip(string ev, string clip, float vol, float pitch = 1, bool vary = true)
        {
            if (!SoundOn) return;
            var e = Bank.Event(ev);
            var a = Bank.Get(clip, e != null ? e.len : 1, false);
            if (a == null) return;
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.Stop();
            src.clip = a;
            src.volume = vol;
            src.pitch = pitch * (vary ? Random.Range(.95f, 1.05f) : 1);
            src.Play();
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
