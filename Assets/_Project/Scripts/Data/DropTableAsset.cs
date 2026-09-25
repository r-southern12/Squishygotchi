using Squishy.Simulation.Gacha;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Drop Table", fileName = "DropTable")]
    public class DropTableAsset : DefAsset<DropTableDef>
    {
        private void OnValidate()
        {
            foreach (var problem in def.Validate())
                Debug.LogWarning(name + ": " + problem, this);
        }
    }
}
