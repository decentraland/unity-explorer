using CommunicationData.URLHelpers;
using DCL.Backpack.AvatarSection.Randomizer;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.Tests
{
    [TestFixture]
    public class OutfitRandomizerShould
    {
        private const int ROLLS = 200;
        private const int PRIORITY_ROLLS = 2000;
        private const string BASE_UPPER_BODY = "urn:decentraland:off-chain:base-avatars:green_hoodie";

        private Dictionary<string, HashSet<string>> hidesByCategory = null!;
        private HashSet<string> dropped = null!;
        private HashSet<string> hidden = null!;

        [SetUp]
        public void SetUp()
        {
            hidesByCategory = new Dictionary<string, HashSet<string>>();
            dropped = new HashSet<string>();
            hidden = new HashSet<string>();
        }

        [Test]
        public void KeepAllPicksWhenNothingHides()
        {
            // Arrange
            hidesByCategory[WearableCategories.Categories.UPPER_BODY] = new HashSet<string>();
            hidesByCategory[WearableCategories.Categories.LOWER_BODY] = new HashSet<string>();
            hidesByCategory[WearableCategories.Categories.HAT] = new HashSet<string>();

            // Act
            OutfitRandomizer.ResolveConflicts(hidesByCategory, dropped, hidden);

            // Assert
            Assert.IsEmpty(dropped);
        }

        [Test]
        public void DropPicksHiddenBySkin()
        {
            // Arrange
            hidesByCategory[WearableCategories.Categories.SKIN] = new HashSet<string>
                { WearableCategories.Categories.UPPER_BODY, WearableCategories.Categories.LOWER_BODY, WearableCategories.Categories.HAIR };
            hidesByCategory[WearableCategories.Categories.UPPER_BODY] = new HashSet<string>();
            hidesByCategory[WearableCategories.Categories.HAIR] = new HashSet<string>();
            hidesByCategory[WearableCategories.Categories.EARRING] = new HashSet<string>();

            // Act
            OutfitRandomizer.ResolveConflicts(hidesByCategory, dropped, hidden);

            // Assert
            CollectionAssert.AreEquivalent(new[] { WearableCategories.Categories.UPPER_BODY, WearableCategories.Categories.HAIR }, dropped);
            Assert.IsTrue(hidden.Contains(WearableCategories.Categories.UPPER_BODY));
        }

        [Test]
        public void DropLowerPriorityPickThatHidesAKeptPick()
        {
            // Arrange
            hidesByCategory[WearableCategories.Categories.MASK] = new HashSet<string>();
            hidesByCategory[WearableCategories.Categories.EYEWEAR] = new HashSet<string> { WearableCategories.Categories.MASK };

            // Act
            OutfitRandomizer.ResolveConflicts(hidesByCategory, dropped, hidden);

            // Assert
            CollectionAssert.AreEquivalent(new[] { WearableCategories.Categories.EYEWEAR }, dropped);
            Assert.IsFalse(hidden.Contains(WearableCategories.Categories.MASK));
        }

        [Test]
        public void NotLetADroppedPickHideOthers()
        {
            // Arrange
            hidesByCategory[WearableCategories.Categories.HELMET] = new HashSet<string> { WearableCategories.Categories.HAT };
            hidesByCategory[WearableCategories.Categories.HAT] = new HashSet<string> { WearableCategories.Categories.HAIR };
            hidesByCategory[WearableCategories.Categories.HAIR] = new HashSet<string>();

            // Act
            OutfitRandomizer.ResolveConflicts(hidesByCategory, dropped, hidden);

            // Assert
            CollectionAssert.AreEquivalent(new[] { WearableCategories.Categories.HAT }, dropped);
            Assert.IsFalse(hidden.Contains(WearableCategories.Categories.HAIR));
        }

        [Test]
        public void AlwaysPickCoreCategoriesWhenAvailable()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(42));
            randomizer.AddEntry(WearableCategories.Categories.UPPER_BODY, new URN("urn:upper"));
            randomizer.AddEntry(WearableCategories.Categories.HAT, new URN("urn:hat"));
            var picks = new Dictionary<string, URN>();
            var hatRolls = 0;

            for (var i = 0; i < ROLLS; i++)
            {
                // Act
                randomizer.Pick(picks);

                // Assert
                Assert.IsTrue(picks.ContainsKey(WearableCategories.Categories.UPPER_BODY));
                if (picks.ContainsKey(WearableCategories.Categories.HAT)) hatRolls++;
            }

            Assert.Greater(hatRolls, 0);
            Assert.Less(hatRolls, ROLLS);
        }

        [Test]
        public void PreferCustomWearablesMostOfTheTime()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(3));
            var custom = new URN("urn:decentraland:matic:collections-v2:0xabc:0");
            randomizer.AddEntry(WearableCategories.Categories.UPPER_BODY, custom);
            randomizer.AddEntry(WearableCategories.Categories.UPPER_BODY, new URN(BASE_UPPER_BODY));
            var picks = new Dictionary<string, URN>();
            var customRolls = 0;

            // Act
            for (var i = 0; i < PRIORITY_ROLLS; i++)
            {
                randomizer.Pick(picks);
                if (picks[WearableCategories.Categories.UPPER_BODY].Equals(custom)) customRolls++;
            }

            // Assert
            float ratio = customRolls / (float)PRIORITY_ROLLS;
            Assert.That(ratio, Is.EqualTo(OutfitRandomizer.CUSTOM_PRIORITY_CHANCE).Within(0.05f));
        }

        [Test]
        public void PickBaseWearablesWhenNoCustomIsOwned()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(5));
            randomizer.AddEntry(WearableCategories.Categories.UPPER_BODY, new URN(BASE_UPPER_BODY));
            var picks = new Dictionary<string, URN>();

            for (var i = 0; i < ROLLS; i++)
            {
                // Act
                randomizer.Pick(picks);

                // Assert
                Assert.AreEqual(new URN(BASE_UPPER_BODY), picks[WearableCategories.Categories.UPPER_BODY]);
            }
        }

        [Test]
        public void RepickOnlyUntriedWearables()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(7));
            var first = new URN("urn:feet:1");
            var second = new URN("urn:feet:2");
            randomizer.AddEntry(WearableCategories.Categories.FEET, first);
            randomizer.AddEntry(WearableCategories.Categories.FEET, second);
            var tried = new HashSet<URN> { first };

            // Act
            bool repicked = randomizer.TryRepick(WearableCategories.Categories.FEET, tried, out URN urn);
            tried.Add(urn);
            bool repickedWhenExhausted = randomizer.TryRepick(WearableCategories.Categories.FEET, tried, out URN _);

            // Assert
            Assert.IsTrue(repicked);
            Assert.AreEqual(second, urn);
            Assert.IsFalse(repickedWhenExhausted);
        }
    }
}
