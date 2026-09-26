using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Calm, endless background music made on the fly: warm, slowly changing chords (soft detuned pads),
    /// occasional kalimba notes on a pentatonic scale and a faint bed of steam. Generated in the audio
    /// thread, so there are no music files and it never loops audibly.
    /// </summary>
    public sealed class AmbientMusic : MonoBehaviour
    {
        public float Volume;

        private const float ChordSeconds = 9f;
        // Cmaj9, Am9, Fmaj9, G6/9 in Hz (voiced low and open).
        private static readonly float[][] Chords =
        {
            new[] { 130.8f, 196.0f, 246.9f, 293.7f, 329.6f },
            new[] { 110.0f, 164.8f, 196.0f, 246.9f, 261.6f },
            new[] { 87.3f, 130.8f, 164.8f, 196.0f, 220.0f },
            new[] { 98.0f, 146.8f, 196.0f, 220.0f, 246.9f },
        };
        private static readonly float[] Pent = { 523.3f, 587.3f, 659.3f, 784.0f, 880.0f, 1046.5f, 1174.7f };

        private int _rate;
        private double _t;
        private readonly double[] _ph = new double[10];
        private float _vol, _lp, _noiseLp, _nextNote = 2;
        private readonly System.Random _rng = new System.Random();
        private readonly float[] _pluckF = new float[4];
        private readonly float[] _pluckT = { 99, 99, 99, 99 };
        private readonly double[] _pluckPh = new double[4];
        private int _pluckNext;

        private void Awake()
        {
            _rate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            src.loop = true;
            src.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float dt = 1f / _rate, target = Volume;
            float vk = 1 - Mathf.Exp(-dt / 1.5f); // gentle fade in and out
            for (int i = 0; i < data.Length; i += channels)
            {
                _t += dt;
                _vol += (target - _vol) * vk;
                if (_vol < 1e-5f) { for (int c = 0; c < channels; c++) data[i + c] = 0; continue; }

                // Pads: crossfade between the current and next chord over the last two seconds.
                double pos = _t / ChordSeconds;
                int ci = (int)pos % Chords.Length, ni = (ci + 1) % Chords.Length;
                float frac = (float)(pos - System.Math.Floor(pos)), xf = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.78f, 1f, frac));
                float pad = 0;
                for (int v = 0; v < 5; v++)
                {
                    float f = Mathf.Lerp(Chords[ci][v], Chords[ni][v], xf);
                    for (int d = 0; d < 2; d++)
                    {
                        int k = v * 2 + d;
                        _ph[k] += f * (d == 0 ? .997 : 1.003) / _rate;
                        float s = Mathf.Sin((float)(_ph[k] % 1.0 * 2 * System.Math.PI));
                        pad += s * (v == 0 ? .22f : .12f);
                    }
                }
                pad *= .5f + .12f * Mathf.Sin((float)_t * .3f); // slow breathing swell

                // Kalimba plucks: a note every couple of seconds, bell-like decay.
                _nextNote -= dt;
                if (_nextNote <= 0)
                {
                    _nextNote = 1.4f + (float)_rng.NextDouble() * 2.6f;
                    _pluckF[_pluckNext] = Pent[_rng.Next(Pent.Length)];
                    _pluckT[_pluckNext] = 0;
                    _pluckPh[_pluckNext] = 0;
                    _pluckNext = (_pluckNext + 1) % 4;
                }
                float pluck = 0;
                for (int p = 0; p < 4; p++)
                {
                    if (_pluckT[p] > 4) continue;
                    _pluckT[p] += dt;
                    _pluckPh[p] += _pluckF[p] / _rate;
                    float ph = (float)(_pluckPh[p] % 1.0 * 2 * System.Math.PI);
                    float env = Mathf.Exp(-_pluckT[p] * 2.2f) * Mathf.Min(1, _pluckT[p] * 300);
                    pluck += (Mathf.Sin(ph) + .25f * Mathf.Sin(ph * 2.76f)) * env * .22f;
                }

                // Soft steam: low-passed noise, barely there.
                float n = (float)(_rng.NextDouble() * 2 - 1);
                _noiseLp += .02f * (n - _noiseLp);

                float s0 = pad + pluck + _noiseLp * .35f;
                _lp += .18f * (s0 - _lp); // warm low-pass
                float o = _lp * _vol;
                for (int c = 0; c < channels; c++) data[i + c] = o;
            }
        }
    }
}
