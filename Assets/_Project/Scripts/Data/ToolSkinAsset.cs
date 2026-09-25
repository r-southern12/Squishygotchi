using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Tool Skin", fileName = "ToolSkin")]
    public class ToolSkinAsset : DefAsset<ToolSkinDef>
    {
        [Tooltip("Tint applied over the tool's own colour. Classic uses no tint.")]
        public bool useTint;
        public Color tint = Color.white;
    }
}
