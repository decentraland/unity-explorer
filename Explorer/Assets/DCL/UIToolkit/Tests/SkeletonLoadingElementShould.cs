using DCL.UIToolkit.Elements;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DCL.UIToolkit.Tests
{
    [TestFixture]
    public class SkeletonLoadingElementShould
    {
        private const float TOLERANCE = 0.01f;

        private SkeletonLoadingElement skeleton = null!;

        [SetUp]
        public void SetUp()
        {
            skeleton = new SkeletonLoadingElement { BoneCount = 3 };
        }

        [Test]
        public void BuildABoneWithAShimmerPerCount()
        {
            Assert.AreEqual(3, skeleton.childCount);

            for (var i = 0; i < 3; i++)
            {
                Assert.IsTrue(skeleton[i].ClassListContains("skeleton-loading__bone"));
                Assert.AreEqual(1, skeleton[i].childCount);
                Assert.IsTrue(skeleton[i][0].ClassListContains("skeleton-loading__shimmer"));
                Assert.AreEqual(-200f, ShimmerLeft(i), TOLERANCE, "The gradient, twice the bone's width, starts off its left edge");
            }

            Assert.AreEqual(0f, skeleton.Phase, TOLERANCE);
            Assert.IsFalse(skeleton.IsAnimating);
        }

        [Test]
        public void RebuildTheBonesWhenTheCountChanges()
        {
            //Act
            skeleton.BoneCount = 2;

            //Assert
            Assert.AreEqual(2, skeleton.childCount);
        }

        [Test]
        public void SweepTheGradientAcrossEveryBoneWithinACycle()
        {
            //Act
            skeleton.Advance(0.25f);

            //Assert
            Assert.AreEqual(0.25f, skeleton.Phase, TOLERANCE);
            Assert.AreEqual(-125f, ShimmerLeft(0), TOLERANCE);
            Assert.AreEqual(-125f, ShimmerLeft(2), TOLERANCE);

            //Act
            skeleton.Advance(0.5f);

            //Assert
            Assert.AreEqual(0.75f, skeleton.Phase, TOLERANCE);
            Assert.AreEqual(25f, ShimmerLeft(0), TOLERANCE);
        }

        [Test]
        public void StartTheSweepOverAfterACycle()
        {
            //Act
            skeleton.Advance(1.25f);

            //Assert
            Assert.AreEqual(0.25f, skeleton.Phase, TOLERANCE);
            Assert.AreEqual(-125f, ShimmerLeft(1), TOLERANCE);
        }

        [Test]
        public void StartOverWhenAnimatingAgain()
        {
            //Arrange
            skeleton.Advance(0.5f);

            //Act
            skeleton.IsAnimating = true;

            //Assert
            Assert.IsTrue(skeleton.IsAnimating);
            Assert.AreEqual(0f, skeleton.Phase, TOLERANCE);
            Assert.AreEqual(-200f, ShimmerLeft(0), TOLERANCE);

            //Act
            skeleton.IsAnimating = false;

            //Assert
            Assert.IsFalse(skeleton.IsAnimating);
        }

        private float ShimmerLeft(int bone)
        {
            Length left = skeleton[bone][0].style.left.value;
            Assert.AreEqual(LengthUnit.Percent, left.unit);
            return left.value;
        }
    }
}
