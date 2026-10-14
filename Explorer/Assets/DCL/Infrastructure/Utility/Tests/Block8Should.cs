using DCL.Utility.Types;
using NUnit.Framework;
using System;

namespace Utility.Tests
{
    public class Block8Should
    {
        [Test]
        public void StoreEverySlotIndependently()
        {
            // Arrange
            var block = new Block8<int>();

            // Act
            for (var i = 0; i < Block8<int>.CAPACITY; i++)
                block[i] = (i + 1) * 10;

            // Assert
            for (var i = 0; i < Block8<int>.CAPACITY; i++)
                Assert.That(block[i], Is.EqualTo((i + 1) * 10));
        }

        [Test]
        public void CopyByValue()
        {
            // Arrange
            var original = new Block8<string>();
            original[3] = "three";

            // Act
            Block8<string> copy = original;
            copy[3] = "changed";

            // Assert
            Assert.That(original[3], Is.EqualTo("three"));
            Assert.That(copy[3], Is.EqualTo("changed"));
        }

        [Test]
        public void StartEmpty()
        {
            // Arrange
            var block = new Block8<string>();

            // Act
            string last = block[Block8<string>.CAPACITY - 1];

            // Assert
            Assert.That(last, Is.Null);
        }

        [Test]
        public void RejectAnIndexOutsideTheSlots()
        {
            // Arrange
            var block = new Block8<int>();

            // Act
            TestDelegate readPastTheEnd = () => _ = block[Block8<int>.CAPACITY];
            TestDelegate writeBeforeTheStart = () => block[-1] = 1;

            // Assert
            Assert.Throws<ArgumentOutOfRangeException>(readPastTheEnd);
            Assert.Throws<ArgumentOutOfRangeException>(writeBeforeTheStart);
        }
    }
}
