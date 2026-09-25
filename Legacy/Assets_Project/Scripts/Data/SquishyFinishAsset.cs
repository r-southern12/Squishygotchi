using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Squishy Finish", fileName = "Finish")]
    public class SquishyFinishAsset : DefAsset<SquishyFinishDef>
    {
        public Color baseColor = Color.white;
        [Range(0f, 1f)] public float smoothness = 0.15f;
        public Color[] sparkleColors = new Color[0];
        [Tooltip("UV finishes glow; leave black for none.")]
        public Color glowColor = Color.black;
        [Tooltip("Seconds to rise back after a squish. Foam is slow; jelly is quick and wobbly.")]
        public float riseSeconds = 2.5f;
        public Texture2D patternMap;
    }
}
