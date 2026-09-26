using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Background music: one looping track (Resources/Music, unlocked by steamer skins), streamed, with a slow
    /// crossfade when the track changes. Volume is set every frame by Sfx (0 when sound or music is off).
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public float Volume;
        private AudioSource _a, _b; // _a is the current track; _b fades out the previous one
        private string _id;
        private float _fade = 1;

        private void Awake()
        {
            _a = New();
            _b = New();
        }

        private AudioSource New()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.volume = 0;
            return s;
        }

        /// <summary>Switches to a track by id; the old one fades out as the new one fades in.</summary>
        public void Play(string id)
        {
            if (id == _id) return;
            _id = id;
            var clip = string.IsNullOrEmpty(id) ? null : Resources.Load<AudioClip>("Music/" + id);
            var t = _a; _a = _b; _b = t;
            _a.Stop();
            _a.clip = clip;
            _a.volume = 0;
            if (clip != null) _a.Play();
            _fade = 0;
        }

        private void Update()
        {
            _fade = Mathf.Min(1, _fade + Time.unscaledDeltaTime / 2.5f);
            _a.volume = Volume * _fade;
            _b.volume = Volume * (1 - _fade);
            if (_fade >= 1 && _b.isPlaying) { _b.Stop(); _b.clip = null; }
            // Nothing plays while muted, so a silenced phone isn't decoding music.
            bool on = Volume > 0 && _a.clip != null;
            if (on && !_a.isPlaying) _a.Play();
            else if (!on && _a.isPlaying) _a.Pause();
        }
    }
}
