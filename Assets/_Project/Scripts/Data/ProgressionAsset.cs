using System.Collections.Generic;
using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Data
{
    /// <summary>Tables for growth and levelling: squishy sizes, room levels, kitchen levels, mastery.</summary>
    [CreateAssetMenu(menuName = MenuPaths.Create + "Progression", fileName = "Progression")]
    public class ProgressionAsset : ScriptableObject
    {
        public List<SizeTierDef> sizeTiers = new List<SizeTierDef>();
        public List<RoomLevelDef> roomLevels = new List<RoomLevelDef>();
        public List<KitchenLevelDef> kitchenLevels = new List<KitchenLevelDef>();
        public MasteryDef mastery = new MasteryDef();
    }
}
