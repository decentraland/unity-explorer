using NUnit.Framework;
using UnityEngine;

namespace Utility.Tests
{
    public class CanBeDirtyShould
    {
        [Test]
        public void PrintTheWrappedValue()
        {
            // Arrange
            var position = new CanBeDirty<Vector3>(new Vector3(1f, 2f, 3f));

            // Act
            var text = position.ToString();

            // Assert
            Assert.That(text, Is.EqualTo(new Vector3(1f, 2f, 3f).ToString()));
        }
    }
}
