using DCL.Optimization.Hashing;
using NUnit.Framework;

namespace DCL.Optimization.Tests
{
    [TestFixture]
    public class HashingShould
    {
        [Test]
        public void Copy()
        {
            // Arrange
            var hashKey = HashKey.FromString("test");
            var secondHashKey = hashKey.Copy();

            // Assert
            Assert.AreEqual(hashKey, secondHashKey);
        }
    }
}
