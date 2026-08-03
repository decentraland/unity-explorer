using DCL.Ipfs;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace DCL.Browser.DecentralandUrls.Tests
{
    public class AssetBundleManifestVersionCasingShould
    {
        private const string MIXED_CASE_QM_HASH = "QmeHqrQh3DVMB7wGwvf7cR9TLGk3kNnVKJEEVReekEzHzz";

        private static AssetBundleManifestVersion ManifestAt(string version) =>
            AssetBundleManifestVersion.CreateManualManifest(version, "2026-07-08T14:42:27.091Z", version, "2026-07-08T14:40:13.233Z");

        [TestCase("v8", false)]
        [TestCase("v35", false)]
        [TestCase("v48", false)]
        [TestCase("v49", true)]
        [TestCase("v50", true)]
        public void ReportCasePreservationFromTheConverterVersion(string version, bool expected)
        {
            Assert.That(ManifestAt(version).PreservesOriginalCasing(), Is.EqualTo(expected));
        }

        [Test]
        public void KeepPublishedCasingForCasePreservingManifestsOnEveryPlatform()
        {
            // On Mac the pre-v49 branch lowercases; from v49 the CDN objects keep the published casing,
            // so the sanitized hash must be byte-identical to the published one regardless of platform.
            Assert.That(AssetBundleManifestHelper.SanitizeEntityHash(MIXED_CASE_QM_HASH, ManifestAt("v49")), Is.EqualTo(MIXED_CASE_QM_HASH));
        }

        [Test]
        public void LeaveNonQmHashesUntouched()
        {
            const string BAFK_HASH = "bafkreigdlyk6azmzy3rr2mfrs3pgb6j7wp2sbxdrzs6uyzpe25nwaso3ta";
            Assert.That(AssetBundleManifestHelper.SanitizeEntityHash(BAFK_HASH, ManifestAt("v35")), Is.EqualTo(BAFK_HASH));
        }
    }
}
