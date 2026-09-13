using NUnit.Framework;

namespace DCL.ApplicationGuards
{
    public class SemanticVersioningExtensionsShould
    {
        [Test]
        [TestCase("0.1.1-preview", "0.1.1-preview", false, TestName = "Same channel version is current")]
        [TestCase("0.1.1-linux-preview", "0.1.1-preview", false, TestName = "Pre-release label carries no ordering")]
        [TestCase("0.1.1-preview", "v0.1.1", false, TestName = "Leading v carries no ordering")]
        [TestCase("0.1.1-preview", "0.1.2-preview", true, TestName = "Newer patch is an update")]
        [TestCase("0.1.1-preview", "0.2.0-preview", true, TestName = "Newer minor is an update")]
        [TestCase("0.1.1-preview", "1.0.0", true, TestName = "Newer major is an update")]
        [TestCase("0.0.0-dev", "0.1.1-preview", true, TestName = "Unstamped dev build is older than the channel")]
        [TestCase("0.1.2-preview", "0.1.1-preview", false, TestName = "Older patch is not an update")]
        [TestCase("1.0.0", "0.9.9", false, TestName = "Higher major wins over higher minor and patch")]
        [TestCase("0.2.0", "0.1.9", false, TestName = "Higher minor wins over higher patch")]
        [TestCase("0.80.0", "0.79.10", false, TestName = "Components compare numerically, not lexically")]
        [TestCase("Mock", "0.1.1-preview", true, TestName = "Unparseable version is older than any channel")]
        [TestCase("0.1.1-preview", "not-a-version", false, TestName = "Unparseable channel never demands an update")]
        public void CompareByNumericCore(string current, string latest, bool expectedOlder)
        {
            // Act
            bool isOlder = current.IsOlderThan(latest);

            // Assert
            Assert.AreEqual(expectedOlder, isOlder);
        }
    }
}
