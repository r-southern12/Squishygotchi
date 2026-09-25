using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Kitchen Tool", fileName = "Tool")]
    public class ToolAsset : DefAsset<ToolDef>
    {
        public Color color = Color.gray;
        public GameObject prefab;
    }
}
