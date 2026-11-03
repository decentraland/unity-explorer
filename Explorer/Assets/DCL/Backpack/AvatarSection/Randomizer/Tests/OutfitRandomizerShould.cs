using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
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
        private const string BASE_BEARD = "urn:decentraland:off-chain:base-avatars:beard";
        private const string CUSTOM_UPPER_BODY = "urn:decentraland:matic:collections-v2:0xabc:upper_";

        private static readonly Func<URN, bool> NOT_PENDING = _ => false;
        private static readonly IReadOnlyDictionary<string, int> NO_OWNED = new Dictionary<string, int>();

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
                Roll(randomizer, picks);

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
                Roll(randomizer, picks);
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
                Roll(randomizer, picks);

                // Assert
                Assert.AreEqual(new URN(BASE_UPPER_BODY), picks[WearableCategories.Categories.UPPER_BODY]);
            }
        }

        [Test]
        public void LeaveBodyShapesSmartAndPendingGiftWearablesOutOfTheCatalog()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(11));
            const string KEPT = "urn:decentraland:matic:collections-v2:0xabc:kept";
            const string SMART = "urn:decentraland:matic:collections-v2:0xabc:smart";
            const string PENDING = "urn:decentraland:matic:collections-v2:0xabc:pending";

            ITrimmedWearable[] wearables =
            {
                RandomizerTestWearables.Trimmed(BodyShape.MALE, WearableCategories.Categories.BODY_SHAPE),
                RandomizerTestWearables.Trimmed(KEPT, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(SMART, WearableCategories.Categories.UPPER_BODY, isSmart: true),
                RandomizerTestWearables.Trimmed(PENDING, WearableCategories.Categories.UPPER_BODY),
            };

            // Act
            randomizer.BuildCatalog(wearables, BodyShape.MALE, urn => urn.Equals(new URN(PENDING)));
            HashSet<URN> upperBodies = PickedOver(randomizer, WearableCategories.Categories.UPPER_BODY);

            // Assert
            CollectionAssert.AreEquivalent(new[] { new URN(KEPT) }, upperBodies);
            Assert.IsEmpty(PickedOver(randomizer, WearableCategories.Categories.BODY_SHAPE));
        }

        [Test]
        public void LeaveWearablesIncompatibleWithTheBodyShapeOutOfTheCatalog()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(13));
            const string MALE_ONLY = "urn:decentraland:matic:collections-v2:0xabc:male";
            const string UNISEX = "urn:decentraland:matic:collections-v2:0xabc:unisex";

            ITrimmedWearable[] wearables =
            {
                RandomizerTestWearables.Trimmed(MALE_ONLY, WearableCategories.Categories.UPPER_BODY, bodyShapes: new string[] { BodyShape.MALE }),
                RandomizerTestWearables.Trimmed(UNISEX, WearableCategories.Categories.UPPER_BODY),
            };

            // Act
            randomizer.BuildCatalog(wearables, BodyShape.FEMALE, NOT_PENDING);

            // Assert
            CollectionAssert.AreEquivalent(new[] { new URN(UNISEX) }, PickedOver(randomizer, WearableCategories.Categories.UPPER_BODY));
        }

        [Test]
        public void SkipFacialHairOnlyForTheFeminineBodyShape()
        {
            // Arrange
            ITrimmedWearable[] wearables = { RandomizerTestWearables.Trimmed(BASE_BEARD, WearableCategories.Categories.FACIAL_HAIR) };
            var feminine = new OutfitRandomizer(new Random(17));
            var masculine = new OutfitRandomizer(new Random(17));

            // Act
            feminine.BuildCatalog(wearables, BodyShape.FEMALE, NOT_PENDING);
            masculine.BuildCatalog(wearables, BodyShape.MALE, NOT_PENDING);

            // Assert
            Assert.IsEmpty(PickedOver(feminine, WearableCategories.Categories.FACIAL_HAIR));
            CollectionAssert.AreEquivalent(new[] { new URN(BASE_BEARD) }, PickedOver(masculine, WearableCategories.Categories.FACIAL_HAIR));
        }

        [Test]
        public void ApplyTheBodyTypePrefixOnlyToBaseWearables()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(19));
            const string BASE_MASCULINE = "urn:decentraland:off-chain:base-avatars:m_sweater";
            const string BASE_FEMININE = "urn:decentraland:off-chain:base-avatars:f_sweater";
            const string CUSTOM_PREFIXED = "urn:decentraland:matic:collections-v2:0xabc:m_jacket";

            ITrimmedWearable[] wearables =
            {
                RandomizerTestWearables.Trimmed(BASE_MASCULINE, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(BASE_FEMININE, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(CUSTOM_PREFIXED, WearableCategories.Categories.UPPER_BODY),
            };

            // Act
            randomizer.BuildCatalog(wearables, BodyShape.FEMALE, NOT_PENDING);

            // Assert
            CollectionAssert.AreEquivalent(new[] { new URN(BASE_FEMININE), new URN(CUSTOM_PREFIXED) }, PickedOver(randomizer, WearableCategories.Categories.UPPER_BODY));
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

        [Test]
        public void PickTheOwnedWearableItsPageWasFetchedFor()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new ScriptedRandom(0));
            ITrimmedWearable[] page = RandomizerTestWearables.Trimmed(CUSTOM_UPPER_BODY, WearableCategories.Categories.UPPER_BODY, 3);
            randomizer.BuildCatalog(new[] { RandomizerTestWearables.Trimmed(BASE_UPPER_BODY, WearableCategories.Categories.UPPER_BODY) }, BodyShape.MALE, NOT_PENDING);

            // Act
            randomizer.AddCustomPage(WearableCategories.Categories.UPPER_BODY, page, 2);

            // Assert
            CollectionAssert.AreEquivalent(new[] { page[2].GetUrn() }, PickedOver(randomizer, WearableCategories.Categories.UPPER_BODY));
        }

        [Test]
        public void KeepOnlyTheUsableWearablesOfThePageCategory()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(29));
            const string KEPT = CUSTOM_UPPER_BODY + "kept";

            ITrimmedWearable[] page =
            {
                RandomizerTestWearables.Trimmed(KEPT, WearableCategories.Categories.UPPER_BODY),
                RandomizerTestWearables.Trimmed(CUSTOM_UPPER_BODY + "smart", WearableCategories.Categories.UPPER_BODY, isSmart: true),
                RandomizerTestWearables.Trimmed(CUSTOM_UPPER_BODY + "hat", WearableCategories.Categories.HAT),
            };

            randomizer.BuildCatalog(Array.Empty<ITrimmedWearable>(), BodyShape.MALE, NOT_PENDING);

            // Act
            randomizer.AddCustomPage(WearableCategories.Categories.UPPER_BODY, page, 0);

            // Assert
            CollectionAssert.AreEquivalent(new[] { new URN(KEPT) }, PickedOver(randomizer, WearableCategories.Categories.UPPER_BODY));
            Assert.IsEmpty(PickedOver(randomizer, WearableCategories.Categories.HAT));
        }

        [Test]
        public void DropThePreviousRollPagesOnANewCatalog()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new Random(31));
            randomizer.AddCustomPage(WearableCategories.Categories.UPPER_BODY, RandomizerTestWearables.Trimmed(CUSTOM_UPPER_BODY, WearableCategories.Categories.UPPER_BODY, 2), 0);

            // Act
            randomizer.BuildCatalog(Array.Empty<ITrimmedWearable>(), BodyShape.MALE, NOT_PENDING);

            // Assert
            Assert.IsEmpty(PickedOver(randomizer, WearableCategories.Categories.UPPER_BODY));
        }

        [Test]
        public void ChooseACategoryThatOnlyHasOwnedWearables()
        {
            // Arrange
            var randomizer = new OutfitRandomizer(new ScriptedRandom(0));
            var chosen = new HashSet<string>();

            // Act
            randomizer.ChooseCategories(new Dictionary<string, int> { [WearableCategories.Categories.FEET] = 4 }, chosen);

            // Assert
            CollectionAssert.AreEquivalent(new[] { WearableCategories.Categories.FEET }, chosen);
        }

        private static void Roll(OutfitRandomizer randomizer, Dictionary<string, URN> picks)
        {
            var chosen = new HashSet<string>();
            randomizer.ChooseCategories(NO_OWNED, chosen);
            randomizer.Pick(chosen, picks);
        }

        private static HashSet<URN> PickedOver(OutfitRandomizer randomizer, string category)
        {
            var picked = new HashSet<URN>();
            var picks = new Dictionary<string, URN>();

            for (var i = 0; i < ROLLS; i++)
            {
                Roll(randomizer, picks);

                if (picks.TryGetValue(category, out URN urn))
                    picked.Add(urn);
            }

            return picked;
        }
    }
}
