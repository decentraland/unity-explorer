using Decentraland.Abgen;
using NUnit.Framework;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    /// <summary>
    ///     Proves the embedded abgen native library actually loads and its ABI matches the C# package
    ///     in the real Unity runtime on this platform — the prerequisite for the CDN-miss fallback.
    /// </summary>
    public class AbgenNativeSmokeShould
    {
        [Test]
        public void LoadNativeLibraryWithMatchingAbi()
        {
            Assert.IsTrue(AbgenConverter.IsAbiCompatible(), $"abgen native lib missing or ABI mismatch; version={AbgenConverter.Version}");
            Assert.AreNotEqual("unknown", AbgenConverter.Version, "abgen native lib did not report a version");
        }
    }
}
