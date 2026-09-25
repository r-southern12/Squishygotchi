using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Ingredient", fileName = "Ingredient")]
    public class IngredientAsset : DefAsset<IngredientDef>
    {
        public Color color = Color.white;
        public Sprite icon;
    }
}
