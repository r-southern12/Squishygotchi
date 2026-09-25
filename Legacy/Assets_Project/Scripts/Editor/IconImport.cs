using Unity.VectorGraphics.Editor;
using UnityEditor;

namespace Squishy.EditorTools
{
    /// <summary>UI icons are the prototype's SVGs, rasterised to sprites so plain uGUI Images can show them.</summary>
    public sealed class IconImport : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/UI/Icons/") || !assetPath.EndsWith(".svg")) return;
            var importer = assetImporter as SVGImporter;
            if (importer == null || importer.SvgType == SVGType.TexturedSprite) return;
            importer.SvgType = SVGType.TexturedSprite;
            importer.KeepTextureAspectRatio = true;
            importer.TextureSize = 128;
        }
    }
}
