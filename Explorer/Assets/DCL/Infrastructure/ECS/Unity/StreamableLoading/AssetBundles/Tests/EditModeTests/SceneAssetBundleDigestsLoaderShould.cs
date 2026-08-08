using Arch.Core;
using DCL.Ipfs;
using DCL.Utility;
using ECS.Prioritization.Components;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using System.Threading.Tasks;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class SceneAssetBundleDigestsLoaderShould
    {
        private const string HASH = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
        private const string DIGEST = "dda1af30bdf4a19ce03e663a9a288afe";

        [Test]
        public async Task InjectDigestsFromPrefetchedManifestWithoutFetching()
        {
            //Arrange
            using World world = World.Create();
            string platform = PlatformUtils.GetCurrentPlatform();

            var definition = new SceneEntityDefinition("test-scene", new SceneMetadata(),
                AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01"));

            var prefetched = new SceneAssetBundleManifest("v49", "2026-05-01", new[] { $"{HASH}_{DIGEST}{platform}" });

            //Act
            await SceneAssetBundleDigestsLoader.EnsureDepsDigestsAsync(world, definition, PartitionComponent.TOP_PRIORITY, CancellationToken.None, prefetched);

            //Assert
            Assert.That(definition.AssetBundleManifestVersionOrFailed.GetCdnRequestHash(HASH), Is.EqualTo($"{HASH}_{DIGEST}{platform}"));
            Assert.That(world.Size, Is.Zero, "the prefetched manifest must be reused — no manifest promise entity may be created");
        }
    }
}
