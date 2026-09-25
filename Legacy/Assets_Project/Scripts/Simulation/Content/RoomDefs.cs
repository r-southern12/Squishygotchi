using System;

namespace Squishy.Simulation.Content
{
    // Plain, serialisable content definitions. The Unity data assets (ScriptableObjects) wrap
    // these, so the simulation reads the same numbers without depending on Unity.
    // Cross-references use string ids, which are also what the save file stores.

    [Serializable]
    public class StyleDef
    {
        public string id;
        public string displayName;
        public string shortName;
        public StyleGroup group;
    }

    [Serializable]
    public class ItemTypeDef
    {
        public string id;
        public string displayName;
        public ItemKind kind;
        /// <summary>Needs this item can fill. None for pure decor.</summary>
        public NeedMask needs;
        public int comfort;
        /// <summary>Max pieces per room for stations and toys. 0 means limited by decor slots instead.</summary>
        public int maxPerRoom;
        public SquishySize minSquishySize;
        public bool storable = true;
        public bool sellable = true;
    }

    [Serializable]
    public class SkinDef
    {
        public string id;
        public string itemTypeId;
        public string styleId;
        public Rarity rarity;
    }

    [Serializable]
    public class SquishyFinishDef
    {
        public string id;
        public string displayName;
        /// <summary>Finish family, e.g. Common, Glitter, Galaxy, UV, Holographic, Pattern, Legendary.</summary>
        public string tier;
        public Rarity rarity;
    }

    [Serializable]
    public class SizeTierDef
    {
        public SquishySize size;
        public int copiesNeeded;
        public float relativeScale;
    }

    [Serializable]
    public class RoomLevelDef
    {
        public int level;
        public float relativeWidth;
        public int decorSlots;
        public int squishiesRequired;
        public int coinCost;
        public bool availableAtLaunch = true;
    }
}
