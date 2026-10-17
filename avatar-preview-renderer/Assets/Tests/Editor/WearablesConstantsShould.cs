using Data;
using NUnit.Framework;
using Runtime.Wearables;

namespace Tests
{
    public class WearablesConstantsShould
    {
        [TestCase(WearableCategories.Categories.HAIR, BodyShape.Male, "urn:decentraland:off-chain:base-avatars:casual_hair_01")]
        [TestCase(WearableCategories.Categories.HAIR, BodyShape.Female, "urn:decentraland:off-chain:base-avatars:standard_hair")]
        [TestCase(WearableCategories.Categories.EYES, BodyShape.Male, "urn:decentraland:off-chain:base-avatars:eyes_00")]
        [TestCase(WearableCategories.Categories.FEET, BodyShape.Female, "urn:decentraland:off-chain:base-avatars:bun_shoes")]
        public void NameTheBabylonDefaultWearable(string category, BodyShape bodyShape, string expected)
        {
            // Act
            var urn = WearablesConstants.GetDefaultWearableUrn(category, bodyShape);

            // Assert
            Assert.AreEqual(expected, urn);
        }

        [TestCase(WearableCategories.Categories.HAT)]
        [TestCase(WearableCategories.Categories.SKIN)]
        [TestCase("")]
        public void LeaveTheOtherCategoriesEmpty(string category)
        {
            // Act
            var urn = WearablesConstants.GetDefaultWearableUrn(category, BodyShape.Male);

            // Assert
            Assert.IsNull(urn);
        }

        [Test]
        public void CoverEveryDefaultCategory()
        {
            foreach (var category in WearablesConstants.DEFAULT_BODY_CATEGORIES)
            {
                Assert.IsNotNull(WearablesConstants.GetDefaultWearableUrn(category, BodyShape.Male), category);
                Assert.IsNotNull(WearablesConstants.GetDefaultWearableUrn(category, BodyShape.Female), category);
            }

            foreach (var category in WearablesConstants.DEFAULT_FACIAL_CATEGORIES)
            {
                Assert.IsNotNull(WearablesConstants.GetDefaultWearableUrn(category, BodyShape.Male), category);
                Assert.IsNotNull(WearablesConstants.GetDefaultWearableUrn(category, BodyShape.Female), category);
            }
        }
    }
}
