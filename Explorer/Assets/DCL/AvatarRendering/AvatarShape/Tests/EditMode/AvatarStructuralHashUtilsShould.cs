using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Loading.Components;
using NUnit.Framework;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarStructuralHashUtilsShould
    {
        [Test]
        public void ProduceEqualHashForEqualInputs()
        {
            string[] wearables = { "urn:a", "urn:b", "urn:c" };

            int a = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, wearables);
            int b = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, wearables);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void DifferByBodyShape()
        {
            string[] wearables = { "urn:a" };

            int male = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, wearables);
            int female = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.FEMALE, wearables);

            Assert.AreNotEqual(male, female);
        }

        [Test]
        public void DifferByWearableSet()
        {
            int a = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, new[] { "urn:a", "urn:b" });
            int b = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, new[] { "urn:a", "urn:c" });

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void DifferByShowOnlyWearablesFlag()
        {
            string[] wearables = { "urn:a" };

            int off = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, wearables, showOnlyWearables: false);
            int on = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, wearables, showOnlyWearables: true);

            Assert.AreNotEqual(off, on);
        }

        [Test]
        public void DifferByWearableOrder()
        {
            // HashCode.Combine order matters — intentional, captures equip-order changes
            int forward = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, new[] { "urn:a", "urn:b" });
            int reversed = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, new[] { "urn:b", "urn:a" });

            Assert.AreNotEqual(forward, reversed);
        }

        [Test]
        public void HandleEmptyWearableList()
        {
            int hash = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, System.Array.Empty<string>());
            // No throw, stable output
            int same = AvatarStructuralHashUtils.ComputeStructuralHash(BodyShape.MALE, System.Array.Empty<string>());
            Assert.AreEqual(hash, same);
        }
    }
}