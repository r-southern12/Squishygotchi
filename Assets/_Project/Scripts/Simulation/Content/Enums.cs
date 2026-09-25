using System;

namespace Squishy.Simulation.Content
{
    public enum Rarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
    }

    public enum NeedKind
    {
        Hunger = 0,
        Play = 1,
        Rest = 2,
        Clean = 3,
    }

    [Flags]
    public enum NeedMask
    {
        None = 0,
        Hunger = 1,
        Play = 2,
        Rest = 4,
        Clean = 8,
    }

    public enum ItemKind
    {
        Station = 0,
        Toy = 1,
        Decor = 2,
        Keepsake = 3,
    }

    public enum SquishySize
    {
        Mini = 0,
        Standard = 1,
        Jumbo = 2,
        Giant = 3,
        SuperMega = 4,
    }

    public enum StyleGroup
    {
        World = 0,
        Mood = 1,
    }

    /// <summary>What kind of prize a steamer gives, before the specific item is picked.</summary>
    public enum RewardCategory
    {
        FurnitureSkin = 0,
        Ingredient = 1,
        Squishy = 2,
        Tool = 3,
        ToolSkin = 4,
        SteamerSkin = 5,
    }
}
