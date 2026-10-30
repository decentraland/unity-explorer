using DCL.AvatarRendering.Wearables.Equipped;
using NUnit.Framework;
using Runtime.Wearables;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class EquippedWearablesShould
    {
        [Test]
        public void AnswerAnEmptySlotForEveryCategoryAfterClear()
        {
            // Arrange
            var equippedWearables = new EquippedWearables();

            // Act
            equippedWearables.Clear();

            // Assert
            foreach (string category in WearableCategories.CATEGORIES_PRIORITY)
                Assert.That(equippedWearables.Wearable(category), Is.Null, category);

            Assert.That(equippedWearables.Items().Count, Is.EqualTo(WearableCategories.CATEGORIES_PRIORITY.Count));
        }
    }
}
