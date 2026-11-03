using NUnit.Framework;

namespace DCL.Nametags.Tests
{
    public class NearestTagsCapShould
    {
        [Test]
        public void AdmitEverythingWhileBelowCapacity()
        {
            // Arrange
            var cap = new NearestTagsCap(3);

            // Act
            cap.Add(10f);
            cap.Add(5f);

            // Assert
            Assert.That(cap.MaxSqrDistance, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void KeepTheLargestOfTheNearestValuesWhenOverCapacity()
        {
            // Arrange
            var cap = new NearestTagsCap(3);
            float[] sqrDistances = { 50f, 3f, 40f, 7f, 1f, 90f, 20f, 2f };

            // Act
            foreach (float sqrDistance in sqrDistances)
                cap.Add(sqrDistance);

            // Assert
            Assert.That(cap.MaxSqrDistance, Is.EqualTo(3f));
        }

        [Test]
        public void ReportTheCapacityValueWhenExactlyFull()
        {
            // Arrange
            var cap = new NearestTagsCap(3);

            // Act
            cap.Add(4f);
            cap.Add(9f);
            cap.Add(1f);

            // Assert
            Assert.That(cap.MaxSqrDistance, Is.EqualTo(9f));
        }

        [Test]
        public void StartOverAfterClear()
        {
            // Arrange
            var cap = new NearestTagsCap(2);
            cap.Add(1f);
            cap.Add(2f);

            // Act
            cap.Clear();
            cap.Add(30f);

            // Assert
            Assert.That(cap.MaxSqrDistance, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void KeepThePreviousFrameValueAsThreshold()
        {
            // Arrange
            var cap = new NearestTagsCap(2);
            cap.Add(4f);
            cap.Add(9f);

            // Act
            cap.NextFrame();
            cap.Add(1f);

            // Assert
            Assert.That(cap.Threshold, Is.EqualTo(9f));
            Assert.That(cap.MaxSqrDistance, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void StartWithAnUnlimitedThreshold()
        {
            // Arrange
            var cap = new NearestTagsCap(2);

            // Assert
            Assert.That(cap.Threshold, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void MatchSortedSelectionForManyValues()
        {
            // Arrange
            const int CAPACITY = 100;
            var cap = new NearestTagsCap(CAPACITY);
            var sqrDistances = new float[1000];
            var random = new System.Random(42);

            for (var i = 0; i < sqrDistances.Length; i++)
                sqrDistances[i] = (float)(random.NextDouble() * 10000);

            // Act
            foreach (float sqrDistance in sqrDistances)
                cap.Add(sqrDistance);

            // Assert
            System.Array.Sort(sqrDistances);
            Assert.That(cap.MaxSqrDistance, Is.EqualTo(sqrDistances[CAPACITY - 1]));
        }
    }
}
