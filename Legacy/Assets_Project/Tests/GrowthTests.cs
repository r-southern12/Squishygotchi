using System.Collections.Generic;
using NUnit.Framework;
using Squishy.Simulation.Content;

namespace Squishy.Tests
{
    public class GrowthTests
    {
        private static readonly List<SizeTierDef> Tiers = new List<SizeTierDef>
        {
            new SizeTierDef { size = SquishySize.Mini, copiesNeeded = 1, relativeScale = 1f },
            new SizeTierDef { size = SquishySize.Standard, copiesNeeded = 3, relativeScale = 1.5f },
            new SizeTierDef { size = SquishySize.Jumbo, copiesNeeded = 6, relativeScale = 2.1f },
            new SizeTierDef { size = SquishySize.Giant, copiesNeeded = 12, relativeScale = 2.9f },
            new SizeTierDef { size = SquishySize.SuperMega, copiesNeeded = 25, relativeScale = 4f },
        };

        [TestCase(0, SquishySize.Mini)]
        [TestCase(1, SquishySize.Mini)]
        [TestCase(2, SquishySize.Mini)]
        [TestCase(3, SquishySize.Standard)]
        [TestCase(11, SquishySize.Jumbo)]
        [TestCase(12, SquishySize.Giant)]
        [TestCase(99, SquishySize.SuperMega)]
        public void SizeFor_UsesLargestTierReached(int copies, SquishySize expected)
        {
            Assert.AreEqual(expected, Growth.SizeFor(Tiers, copies).size);
        }
    }
}
