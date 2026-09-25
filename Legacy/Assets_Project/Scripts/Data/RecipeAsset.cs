using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    [CreateAssetMenu(menuName = MenuPaths.Create + "Recipe", fileName = "Recipe")]
    public class RecipeAsset : DefAsset<RecipeDef>
    {
        public GameObject bowlPrefab;
        public Sprite icon;
    }
}
