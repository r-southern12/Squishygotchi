using System.Collections.Generic;
using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    /// <summary>Needs tuning, activities, which item offers which activity, and the starting room layout.</summary>
    [CreateAssetMenu(menuName = MenuPaths.Create + "Care", fileName = "Care")]
    public class CareAsset : ScriptableObject
    {
        public CareDef care = new CareDef();
        public List<ActivityDef> activities = new List<ActivityDef>();
        public List<ItemActivityDef> itemActivities = new List<ItemActivityDef>();
        public List<StarterPieceDef> starterRoom = new List<StarterPieceDef>();

        public ActivityDef Activity(string id)
        {
            return activities.Find(a => a.id == id);
        }

        public ActivityDef ActivityForItem(string itemTypeId)
        {
            var link = itemActivities.Find(l => l.itemTypeId == itemTypeId);
            return link != null ? Activity(link.activityId) : null;
        }
    }
}
