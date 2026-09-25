using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Snack", fileName = "Snack")]
    public class SnackAsset : DefAsset<SnackDef>
    {
        public Sprite icon;
    }
}
