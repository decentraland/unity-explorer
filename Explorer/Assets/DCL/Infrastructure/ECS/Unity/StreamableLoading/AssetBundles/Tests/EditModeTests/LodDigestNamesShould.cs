using DCL.Ipfs;
using DCL.Utility;
using NUnit.Framework;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class LodDigestNamesShould
    {
        private const string SCENE_ID = "bafkreictb7lsedstowe2twuqjk7nn3hvqs3s2jqhc2bduwmein73xxelbu";
        private const string DIGEST = "0123456789abcdef0123456789abcdef";

        private static SceneAbLodsDto Block() =>
            new ()
            {
                digest = DIGEST,
                descriptor = $"{SCENE_ID}_{DIGEST}_InitialSceneState.json",
                levels = new[] { new SceneAbLodLevelDto { level = 1, file = $"{SCENE_ID}_{DIGEST}_1" } },
            };

        [Test]
        public void ExposeTheNamesTheManifestPublished()
        {
            // Arrange
            AssetBundleManifestVersion manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-09-22");

            // Act
            manifest.InjectLods(Block());

            // Assert
            Assert.That(manifest.TryGetLodDescriptorFile(out string descriptor), Is.True);
            Assert.That(descriptor, Is.EqualTo($"{SCENE_ID}_{DIGEST}_InitialSceneState.json"));
            Assert.That(manifest.TryGetLodBundleFile(1, out string bundle), Is.True);
            Assert.That(bundle, Is.EqualTo($"{SCENE_ID}_{DIGEST}_1"));
            Assert.That(manifest.TryGetLodBundleFile(0, out _), Is.False, "Levels the manifest does not name fall back to the scene-id name");
        }

        [Test]
        public void ExposeNothingWithoutABlockOrOnAFailedManifest()
        {
            // Arrange
            AssetBundleManifestVersion plain = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-09-22");
            AssetBundleManifestVersion failed = AssetBundleManifestVersion.FAILED;

            // Act
            plain.InjectLods(null);
            failed.InjectLods(Block());

            // Assert
            Assert.That(plain.TryGetLodDescriptorFile(out _), Is.False);
            Assert.That(plain.TryGetLodBundleFile(1, out _), Is.False);
            Assert.That(failed.TryGetLodDescriptorFile(out _), Is.False, "The shared FAILED sentinel must stay immutable");
            Assert.That(failed.TryGetLodBundleFile(1, out _), Is.False);
        }

        [Test]
        public void RequestTheDigestNameWithThePlatformSuffixFromTheLodCdn()
        {
            // Arrange
            AssetBundleManifestVersion lodManifest = AssetBundleManifestVersion.CreateForLOD("LOD/1", "abgen-lods-source");

            // Act
            string requested = lodManifest.GetCdnRequestHash($"{SCENE_ID}_{DIGEST}_1");

            // Assert
            Assert.That(requested, Is.EqualTo($"{SCENE_ID}_{DIGEST}_1{PlatformUtils.GetCurrentPlatform()}"));
        }
    }
}
