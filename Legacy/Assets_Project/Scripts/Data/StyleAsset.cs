using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Style", fileName = "Style")]
    public class StyleAsset : DefAsset<StyleDef>
    {
        [Tooltip("Wood/frame, main accent, secondary accent, dark, light.")]
        public Color[] palette = new Color[5];
    }
}
