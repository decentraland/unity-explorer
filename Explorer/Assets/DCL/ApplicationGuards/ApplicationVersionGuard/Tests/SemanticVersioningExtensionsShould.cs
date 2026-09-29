using NUnit.Framework;

namespace DCL.ApplicationGuards
{
    public class SemanticVersioningExtensionsShould
    {
        [TestCase("0.180.0-alpha", "v0.181.0-alpha", true, TestName = "Lower minor is older")]
        [TestCase("0.181.0", "0.181.1", true, TestName = "Lower patch is older")]
        [TestCase("0.181.0", "1.0.0", true, TestName = "Lower major is older")]
        [TestCase("0.181.0-alpha", "v0.181.0-alpha", false, TestName = "Equal version is not older")]
        [TestCase("0.181.0-alpha-release/2026-09-23-1001f8f", "v0.181.0-alpha", false, TestName = "Branch and commit suffix is ignored")]
        [TestCase("1.0.0", "0.181.0", false, TestName = "Higher major with lower minor is not older")]
        [TestCase("0.182.0", "0.181.5", false, TestName = "Higher minor with lower patch is not older")]
        [TestCase("1", "0.181.0", false, TestName = "Missing minor and patch parse as zero")]
        public void CompareVersions(string current, string latest, bool expectedOlder)
        {
            Assert.AreEqual(expectedOlder, current.IsOlderThan(latest));
        }
    }
}
