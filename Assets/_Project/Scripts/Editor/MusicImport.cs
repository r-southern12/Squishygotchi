using UnityEditor;
using UnityEngine;

namespace Squishy.EditorTools
{
    /// <summary>Music tracks stream from disk as mono Vorbis (small in the APK, little memory).</summary>
    public sealed class MusicImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace("\\", "/").Contains("/Resources/Music/")) return;
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
