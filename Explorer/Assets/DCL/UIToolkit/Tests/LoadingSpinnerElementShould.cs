using DCL.UIToolkit.Elements;
using NUnit.Framework;
using UnityEngine;

namespace DCL.UIToolkit.Tests
{
    [TestFixture]
    public class LoadingSpinnerElementShould
    {
        private const float TOLERANCE = 0.01f;

        private LoadingSpinnerElement spinner = null!;

        [SetUp]
        public void SetUp()
        {
            spinner = new LoadingSpinnerElement();
        }

        [Test]
        public void StartEmptyInTheFirstColour()
        {
            Assert.AreEqual(0f, spinner.Head, TOLERANCE);
            Assert.AreEqual(0f, spinner.Tail, TOLERANCE);
            Assert.AreEqual(new Color(1f, 0.737f, 0.357f), spinner.ArcColor);
            Assert.IsFalse(spinner.IsSpinning);
        }

        [Test]
        public void FillFromTheHeadThenEmptyFromTheTailWithinACycle()
        {
            //Act
            spinner.Advance(1f);

            //Assert
            Assert.AreEqual(1f, spinner.Head, TOLERANCE);
            Assert.AreEqual(0f, spinner.Tail, TOLERANCE);

            //Act
            spinner.Advance(0.8f);

            //Assert
            Assert.AreEqual(1f, spinner.Head, TOLERANCE);
            Assert.AreEqual(1f, spinner.Tail, TOLERANCE);
        }

        [Test]
        public void EaseTheHeadThroughTheFirstThird()
        {
            //Act
            spinner.Advance(1f / 3f);

            //Assert
            Assert.AreEqual(0.5f, spinner.Head, TOLERANCE);
            Assert.AreEqual(0f, spinner.Tail, TOLERANCE);
        }

        [Test]
        public void ChangeColourEachCycleAndWrapAfterThree()
        {
            //Act
            spinner.Advance(3f);

            //Assert
            Assert.AreEqual(new Color(1f, 0.176f, 0.333f), spinner.ArcColor);

            //Act
            spinner.Advance(2f);

            //Assert
            Assert.AreEqual(new Color(0.412f, 0.122f, 0.663f), spinner.ArcColor);

            //Act
            spinner.Advance(2f);

            //Assert
            Assert.AreEqual(new Color(1f, 0.737f, 0.357f), spinner.ArcColor);
        }

        [Test]
        public void StartOverWhenSetSpinning()
        {
            //Arrange
            spinner.Advance(5f);

            //Act
            spinner.IsSpinning = true;

            //Assert
            Assert.IsTrue(spinner.IsSpinning);
            Assert.AreEqual(0f, spinner.Head, TOLERANCE);
            Assert.AreEqual(new Color(1f, 0.737f, 0.357f), spinner.ArcColor);

            //Act
            spinner.IsSpinning = false;

            //Assert
            Assert.IsFalse(spinner.IsSpinning);
        }
    }
}
