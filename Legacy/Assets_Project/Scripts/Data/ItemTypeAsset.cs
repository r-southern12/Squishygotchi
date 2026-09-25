using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Item Type", fileName = "ItemType")]
    public class ItemTypeAsset : DefAsset<ItemTypeDef>
    {
        public Sprite icon;
    }
}
