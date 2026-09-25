using System.Collections.Generic;
using UnityEngine;

namespace Squishy.Data
{
    /// <summary>
    /// The one asset the game loads to find all content. "Squishy > Content > Rebuild Database"
    /// refills these lists from every asset under Assets/_Project/Content, so new content
    /// just needs creating in that folder.
    /// </summary>
    [CreateAssetMenu(menuName = MenuPaths.Create + "Content Database", fileName = "ContentDatabase")]
    public class ContentDatabase : ScriptableObject
    {
        public EconomyAsset economy;
        public DropTableAsset dropTable;
        public ProgressionAsset progression;
        public CareAsset care;

        public ItemTypeAsset ItemType(string id)
        {
            return itemTypes.Find(t => t.def.id == id);
        }

        public StyleAsset Style(string id)
        {
            return styles.Find(s => s.def.id == id);
        }

        public List<StyleAsset> styles = new List<StyleAsset>();
        public List<ItemTypeAsset> itemTypes = new List<ItemTypeAsset>();
        public List<SkinAsset> skins = new List<SkinAsset>();
        public List<SquishyFinishAsset> finishes = new List<SquishyFinishAsset>();
        public List<IngredientAsset> ingredients = new List<IngredientAsset>();
        public List<SnackAsset> snacks = new List<SnackAsset>();
        public List<RecipeAsset> recipes = new List<RecipeAsset>();
        public List<ToolAsset> tools = new List<ToolAsset>();
        public List<ToolSkinAsset> toolSkins = new List<ToolSkinAsset>();
        public List<TaskAsset> tasks = new List<TaskAsset>();
    }
}
