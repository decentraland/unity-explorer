using DCL.Profiles.Self;
using NUnit.Framework;

namespace DCL.Profiles.Tests
{
    public class RequestListsShould
    {
        private static readonly RequestId FIRST = new (1);
        private static readonly RequestId SECOND = new (2);
        private static readonly RequestId THIRD = new (3);

        [Test]
        public void KeepRequestsInArrivalOrder()
        {
            // Arrange
            RequestIds ids = default;

            // Act
            ids = ids.Add(FIRST).Add(SECOND).Add(THIRD);

            // Assert
            Assert.That(ids.Count, Is.EqualTo(3));
            Assert.That(ids[0], Is.EqualTo(FIRST));
            Assert.That(ids[1], Is.EqualTo(SECOND));
            Assert.That(ids[2], Is.EqualTo(THIRD));
            Assert.That(ids.Contains(SECOND), Is.True);
        }

        [Test]
        public void DropTheOldestRequestWhenFull()
        {
            // Arrange
            RequestIds ids = Full();
            var newest = new RequestId(RequestIds.CAPACITY + 1);

            // Act
            RequestIds next = ids.Add(newest);

            // Assert
            Assert.That(next.Count, Is.EqualTo(RequestIds.CAPACITY));
            Assert.That(next.Contains(FIRST), Is.False);
            Assert.That(next[0], Is.EqualTo(SECOND));
            Assert.That(next[RequestIds.CAPACITY - 1], Is.EqualTo(newest));
        }

        [Test]
        public void RemoveARequestAndCloseTheGap()
        {
            // Arrange
            RequestIds ids = default(RequestIds).Add(FIRST).Add(SECOND).Add(THIRD);

            // Act
            RequestIds next = ids.Remove(SECOND);

            // Assert
            Assert.That(next.Count, Is.EqualTo(2));
            Assert.That(next[0], Is.EqualTo(FIRST));
            Assert.That(next[1], Is.EqualTo(THIRD));
            Assert.That(next.Contains(SECOND), Is.False);
        }

        [Test]
        public void StayTheSameWhenRemovingAnUnknownRequest()
        {
            // Arrange
            RequestIds ids = default(RequestIds).Add(FIRST);

            // Act
            RequestIds next = ids.Remove(THIRD);

            // Assert
            Assert.That(next, Is.EqualTo(ids));
        }

        [Test]
        public void CompareRequestsByContent()
        {
            // Arrange
            RequestIds left = default(RequestIds).Add(FIRST).Add(SECOND);
            RequestIds right = default(RequestIds).Add(FIRST).Add(SECOND);
            RequestIds shorter = default(RequestIds).Add(FIRST);

            // Act
            bool sameContent = left.Equals(right);
            bool differentLength = left.Equals(shorter);

            // Assert
            Assert.That(sameContent, Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
            Assert.That(differentLength, Is.False);
        }

        [Test]
        public void StoreAndReturnResultsById()
        {
            // Arrange
            RequestResults<int> results = default;

            // Act
            results = results.With(FIRST, 10).With(SECOND, 20);

            // Assert
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results.TryGet(SECOND, out int second), Is.True);
            Assert.That(second, Is.EqualTo(20));
            Assert.That(results.TryGet(THIRD, out _), Is.False);
            Assert.That(results.Contains(FIRST), Is.True);
        }

        [Test]
        public void DropTheOldestResultWhenFull()
        {
            // Arrange
            RequestResults<int> results = default;

            for (var i = 1; i <= RequestResults<int>.CAPACITY; i++)
                results = results.With(new RequestId(i), i);

            var newest = new RequestId(RequestResults<int>.CAPACITY + 1);

            // Act
            RequestResults<int> next = results.With(newest, 99);

            // Assert
            Assert.That(next.Count, Is.EqualTo(RequestResults<int>.CAPACITY));
            Assert.That(next.Contains(FIRST), Is.False);
            Assert.That(next.TryGet(SECOND, out int second), Is.True);
            Assert.That(second, Is.EqualTo(2));
            Assert.That(next.TryGet(newest, out int last), Is.True);
            Assert.That(last, Is.EqualTo(99));
        }

        [Test]
        public void ForgetAResultOnceTaken()
        {
            // Arrange
            RequestResults<int> results = default(RequestResults<int>).With(FIRST, 10).With(SECOND, 20).With(THIRD, 30);

            // Act
            RequestResults<int> next = results.Without(SECOND);

            // Assert
            Assert.That(next.Count, Is.EqualTo(2));
            Assert.That(next.Contains(SECOND), Is.False);
            Assert.That(next.TryGet(THIRD, out int third), Is.True);
            Assert.That(third, Is.EqualTo(30));
        }

        private static RequestIds Full()
        {
            RequestIds ids = default;

            for (var i = 1; i <= RequestIds.CAPACITY; i++)
                ids = ids.Add(new RequestId(i));

            return ids;
        }
    }
}
