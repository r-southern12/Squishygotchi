using UnityEditor;

namespace Squishy.EditorTools
{
    /// <summary>Videos (the startup animation) are imported without their sound: it's never used (user, 4 Oct 2026).</summary>
    public sealed class VideoImport : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (!(assetImporter is VideoClipImporter imp)) return;
            if (!assetPath.Replace("\\", "/").Contains("/Resources/Video/")) return;
            imp.importAudio = false;
        }
    }
}
