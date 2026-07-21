using DCL.Ipfs;
using DCL.Utility;
using ECS.StreamableLoading.Cache.Disk;
using ECS.Unity.GLTFContainer.Asset.Components;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class DepsDigestCacheKeyShould
    {
        private const string DIGEST_A = "dda1af30bdf4a19ce03e663a9a288afe";
        private const string DIGEST_B = "243f68977939e1f526b4c1a05a40b43a";

        private const string HASH_A = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
        private const string HASH_B = "bafkreice3qpeyeb4ni7fnlt6bijs57zrbuw7cwmbymzcndyllzho3hbgaa";
        private const string HASH_LEGACY = "bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru";

        private static readonly AssetBundleManifestVersion ANY_MANIFEST = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");

        private static AssetBundleManifestVersion CreateV49Manifest(params string[] files)
        {
            var manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            manifest.InjectDepsDigests(files);
            return manifest;
        }

        [Test]
        public void TranslateBareHashToCdnRequestHash()
        {
            string platform = PlatformUtils.GetCurrentPlatform();

            AssetBundleManifestVersion manifest = CreateV49Manifest(
                $"{HASH_LEGACY}{platform}",
                $"{HASH_A}_{DIGEST_A}{platform}");

            Assert.That(manifest.GetCdnRequestHash(HASH_A), Is.EqualTo($"{HASH_A}_{DIGEST_A}{platform}"));

            Assert.That(manifest.GetCdnRequestHash(HASH_LEGACY), Is.EqualTo($"{HASH_LEGACY}{platform}"),
                "Files listed without a digest resolve to their verbatim 2-part manifest name");

            Assert.That(manifest.GetCdnRequestHash(HASH_B), Is.EqualTo($"{HASH_B}{platform}"),
                "Hashes absent from the manifest must fall back to the platform-suffixed bare hash");
        }

        [Test]
        public void TranslateBareHashCaseInsensitivelyToManifestCasing()
        {
            string platform = PlatformUtils.GetCurrentPlatform();

            AssetBundleManifestVersion manifest = CreateV49Manifest($"qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv_{DIGEST_A}{platform}");

            // The lookup is case-insensitive and returns the manifest's casing — the name that exists on the case-sensitive CDN.
            Assert.That(manifest.GetCdnRequestHash("QmaBrb8WisG9b4Szzt6ACHgaJdyULTEjpzmTwDi4RCEtZV"),
                Is.EqualTo($"qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv_{DIGEST_A}{platform}"));
        }

        [Test]
        public void ReportCanonicalAssetsOnlyWhenFilesWereInjected()
        {
            // The URL shape and cache-key dispatch hinge on this: only scenes fetch files[], and only scene bundles
            // are stored under the canonical assets/ prefix; wearables/emotes must keep entity-path URLs and buildDate keying.
            var withoutFiles = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            Assert.That(withoutFiles.HasCanonicalAssets(), Is.False);

            AssetBundleManifestVersion withDigestFiles = CreateV49Manifest($"{HASH_A}_{DIGEST_A}_mac");
            Assert.That(withDigestFiles.HasCanonicalAssets(), Is.True);

            // Any injected files[] counts — a reuse-converted scene can legitimately list only 2-part names.
            AssetBundleManifestVersion onlyLegacyFiles = CreateV49Manifest($"{HASH_LEGACY}_mac");
            Assert.That(onlyLegacyFiles.HasCanonicalAssets(), Is.True);
        }

        [Test]
        public void ProvideFailedManifestWhenDefinitionHasNone()
        {
            // AB intentions require a manifest: definitions without one hand out the failed sentinel,
            // which the loading pipeline already treats as a dead end.
            var definition = new SceneEntityDefinition();

            Assert.That(definition.assetBundleManifestVersion, Is.Null);
            Assert.That(definition.AssetBundleManifestVersionOrFailed.assetBundleManifestRequestFailed, Is.True);
        }

        [Test]
        public void ComposeCacheKey_FallsBackToBareHashWhenNoDigest()
        {
            AssetBundleManifestVersion manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            Assert.That(manifest.ComposeCacheKey(HASH_A), Is.EqualTo(HASH_A));
        }

        [Test]
        public void ComposeCacheKey_ReturnsVerbatimFileNameWhenPresent()
        {
            string platform = PlatformUtils.GetCurrentPlatform();
            AssetBundleManifestVersion manifest = CreateV49Manifest($"{HASH_A}_{DIGEST_A}{platform}");

            Assert.That(manifest.ComposeCacheKey(HASH_A), Is.EqualTo($"{HASH_A}_{DIGEST_A}{platform}"));
        }

        [Test]
        public void ResolveQmContentCasingThroughTheSameMap()
        {
            string platform = PlatformUtils.GetCurrentPlatform();
            var manifest = AssetBundleManifestVersion.CreateFromFallback("v35", "2026-05-01");

            manifest.InjectContent("Qmf7DaJZRygoayfNn5Jq6QAykrhFpQUr2us2VFvjREiajk",
                new[] { new ContentDefinition { file = "model.glb", hash = "QmaBrb8WisG9b4Szzt6ACHgaJdyULTEjpzmTwDi4RCEtZV" } });

            Assert.That(manifest.HasCanonicalAssets(), Is.False, "Content casing entries must not switch the URL shape or cache keying to the canonical scheme");

            Assert.That(manifest.GetCdnRequestHash("QmaBrb8WisG9b4Szzt6ACHgaJdyULTEjpzmTwDi4RCEtZV"),
                Is.EqualTo($"qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv{platform}").IgnoreCase);
        }

        [Test]
        public void PreferDigestEntriesOverContentCasingEntries()
        {
            string platform = PlatformUtils.GetCurrentPlatform();
            const string QM_ENTITY_ID = "Qmf7DaJZRygoayfNn5Jq6QAykrhFpQUr2us2VFvjREiajk";
            const string QM_HASH = "qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv";

            // Regardless of injection order, the digest-bearing manifest entry wins over the casing entry.
            var contentFirst = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            contentFirst.InjectContent(QM_ENTITY_ID, new[] { new ContentDefinition { file = "model.glb", hash = QM_HASH } });
            contentFirst.InjectDepsDigests(new[] { $"{QM_HASH}_{DIGEST_A}{platform}" });
            Assert.That(contentFirst.GetCdnRequestHash(QM_HASH), Is.EqualTo($"{QM_HASH}_{DIGEST_A}{platform}"));

            var digestsFirst = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            digestsFirst.InjectDepsDigests(new[] { $"{QM_HASH}_{DIGEST_A}{platform}" });
            digestsFirst.InjectContent(QM_ENTITY_ID, new[] { new ContentDefinition { file = "model.glb", hash = QM_HASH } });
            Assert.That(digestsFirst.GetCdnRequestHash(QM_HASH), Is.EqualTo($"{QM_HASH}_{DIGEST_A}{platform}"));
        }

        [Test]
        public void TreatIntentionsWithDifferentDigestBearingHashesAsDistinct()
        {
            // The digest is part of the Hash itself, so two dependency closures of the same bare hash never collide.
            var a = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac", ANY_MANIFEST);
            var b = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_B}_mac", ANY_MANIFEST);

            Assert.That(a.Equals(b), Is.False);
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void TreatIntentionsWithSameHashAsEqual()
        {
            var a = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac", ANY_MANIFEST);
            var b = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac", ANY_MANIFEST);

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ProduceDistinctDiskFilenamesForDifferentDigestBearingHashes()
        {
            var a = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac", ANY_MANIFEST);
            var b = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_B}_mac", ANY_MANIFEST);

            using var keyA = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in a);
            using var keyB = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in b);

            Assert.That(HashNamings.HashNameFrom(keyA, ".ab"), Is.Not.EqualTo(HashNamings.HashNameFrom(keyB, ".ab")));
        }

        [Test]
        public void PreserveLegacyDiskFilenameForDigestLessHashes()
        {
            // A digest-less hash must keep its pre-scheme on-disk file name so existing cached entries keep hitting.
            var legacy = GetAssetBundleIntention.FromHash(HASH_LEGACY, ANY_MANIFEST);
            var alsoLegacy = GetAssetBundleIntention.FromHash(HASH_LEGACY, ANY_MANIFEST);

            using var keyA = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in legacy);
            using var keyB = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in alsoLegacy);

            Assert.That(HashNamings.HashNameFrom(keyA, ".ab"), Is.EqualTo(HashNamings.HashNameFrom(keyB, ".ab")));
        }

        [Test]
        public void GltfIntentionDefaultsCacheKeyToHash()
        {
            var intention = new GetGltfContainerAssetIntention("model.glb", HASH_A, new CancellationTokenSource());

            Assert.That(intention.CacheKey, Is.EqualTo(HASH_A), "Legacy callers that don't supply a cache key must default to the bare hash");
        }

        [Test]
        public void GltfIntentionStoresPassedCacheKeyVerbatim()
        {
            string customKey = $"{HASH_A}_{DIGEST_A}_mac";
            var intention = new GetGltfContainerAssetIntention("model.glb", HASH_A, new CancellationTokenSource(), customKey);

            Assert.That(intention.CacheKey, Is.EqualTo(customKey));
        }

        [Test]
        public void GltfIntentionsWithDifferentCacheKeysAreDistinct()
        {
            var a = new GetGltfContainerAssetIntention("model.glb", HASH_A, new CancellationTokenSource(), $"{HASH_A}_{DIGEST_A}_mac");
            var b = new GetGltfContainerAssetIntention("model.glb", HASH_A, new CancellationTokenSource(), $"{HASH_A}_{DIGEST_B}_mac");

            Assert.That(a.CacheKey, Is.Not.EqualTo(b.CacheKey));
            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void ComputeStableCacheHashForSameInputs()
        {
            AssetBundleManifestVersion sceneManifest = CreateV49Manifest($"{HASH_A}_{DIGEST_A}_mac");
            Assert.That(sceneManifest.ComputeCacheHash($"{HASH_A}_{DIGEST_A}_mac"), Is.EqualTo(sceneManifest.ComputeCacheHash($"{HASH_A}_{DIGEST_A}_mac")));

            AssetBundleManifestVersion legacyManifest = AssetBundleManifestVersion.CreateFromFallback("v48", "2026-05-01");
            Assert.That(legacyManifest.ComputeCacheHash(HASH_LEGACY), Is.EqualTo(legacyManifest.ComputeCacheHash(HASH_LEGACY)));
        }

        [Test]
        public void ComputeDifferentCacheHashWhenDigestBearingHashDiffers()
        {
            // The digest travels inside the hash, so two dependency closures produce different Unity-cache keys.
            AssetBundleManifestVersion manifest = CreateV49Manifest($"{HASH_A}_{DIGEST_A}_mac", $"{HASH_A}_{DIGEST_B}_mac");

            Assert.That(manifest.ComputeCacheHash($"{HASH_A}_{DIGEST_A}_mac"), Is.Not.EqualTo(manifest.ComputeCacheHash($"{HASH_A}_{DIGEST_B}_mac")));
        }

        [Test]
        public void ComputeDifferentCacheHashWhenVersionDiffers()
        {
            AssetBundleManifestVersion v49 = CreateV49Manifest($"{HASH_A}_{DIGEST_A}_mac");

            var v50 = AssetBundleManifestVersion.CreateFromFallback("v50", "2026-05-01");
            v50.InjectDepsDigests(new[] { $"{HASH_A}_{DIGEST_A}_mac" });

            Assert.That(v49.ComputeCacheHash($"{HASH_A}_{DIGEST_A}_mac"), Is.Not.EqualTo(v50.ComputeCacheHash($"{HASH_A}_{DIGEST_A}_mac")));
        }

        [Test]
        public void ComputeDifferentCacheHashAcrossVersionHashBoundary()
        {
            // Without the delimiter, (version="v49", hash="0foo") and (version="v490", hash="foo") would produce the same byte stream.
            AssetBundleManifestVersion v49 = CreateV49Manifest($"0foo_{DIGEST_A}_mac");

            var v490 = AssetBundleManifestVersion.CreateFromFallback("v490", "2026-05-01");
            v490.InjectDepsDigests(new[] { $"foo_{DIGEST_A}_mac" });

            Assert.That(v49.ComputeCacheHash("0foo"), Is.Not.EqualTo(v490.ComputeCacheHash("foo")));
        }

        [Test]
        public void ComputeCacheHashFromBuildDateWithoutDepsMap()
        {
            // Manifests without a deps map (wearables/emotes, pre-v49 scenes) key on buildDate — a republish must flush their cache.
            var a = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            var b = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-02");

            Assert.That(a.ComputeCacheHash(HASH_LEGACY), Is.Not.EqualTo(b.ComputeCacheHash(HASH_LEGACY)));
        }

        [Test]
        public void ComputeDistinctCacheHashesAcrossKeyingSchemes()
        {
            // A mapped and an unmapped manifest must not produce the same key for the same hash, even when the
            // legacy buildDate happens to equal the v49 version string.
            AssetBundleManifestVersion mapped = CreateV49Manifest($"{HASH_A}_{DIGEST_A}_mac");
            var unmapped = AssetBundleManifestVersion.CreateFromFallback("v49", "v49");

            Assert.That(mapped.ComputeCacheHash(HASH_A), Is.Not.EqualTo(unmapped.ComputeCacheHash(HASH_A)));
        }

        [Test]
        public void VersionPredicates_DoNotThrowOnNonVNVersions()
        {
            //Check that when handling a LOD it doesn't throw
            var lodManifest = AssetBundleManifestVersion.CreateForLOD("LOD/0", "dummyDate");

            Assert.That(lodManifest.HasHashInPath(), Is.False);
            Assert.That(lodManifest.SupportsDepsDigests(), Is.False);

            var manualManifest = AssetBundleManifestVersion.CreateManualManifest();

            Assert.That(manualManifest.HasHashInPath(), Is.False);
            Assert.That(manualManifest.SupportsDepsDigests(), Is.False);

            var wrongString = AssetBundleManifestVersion.CreateFromFallback("v", "dummyDate");
            Assert.That(wrongString.SupportsDepsDigests(), Is.False);

            var nonNumeric = AssetBundleManifestVersion.CreateFromFallback("vfoo", "dummyDate");
            Assert.That(nonNumeric.SupportsDepsDigests(), Is.False);
        }
    }
}
