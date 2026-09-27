using UnityEditor;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>Music streams from disk as mono Vorbis; sound clips decompress on load (SoundBank cuts them up).</summary>
    public sealed class MusicImport : AssetPostprocessor
    {
        private void SfxSettings()
        {
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = .6f;
            s.preloadAudioData = false;
            imp.defaultSampleSettings = s;
        }

        private void OnPreprocessAudio()
        {
            string p = assetPath.Replace("\\", "/");
            if (p.Contains("/Resources/Sfx/")) { SfxSettings(); return; }
            if (!p.Contains("/Resources/Music/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            imp.loadInBackground = true;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = .5f;
            s.preloadAudioData = false;
            imp.defaultSampleSettings = s;
        }
    }
}
