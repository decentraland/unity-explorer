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

        private static AssetBundleManifestVersion CreateV49Manifest(params string[] files)
        {
            var manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            manifest.InjectDepsDigests(files);
            return manifest;
        }

        [Test]
        public void ResolveCdnRequestHashToVerbatimManifestEntry()
        {
            // ResolveCdnRequestHash strips the *current* platform suffix off the input, so the manifest entries must be
            // built with it for the test to be platform-agnostic.
            string platform = PlatformUtils.GetCurrentPlatform();

            AssetBundleManifestVersion manifest = CreateV49Manifest(
                $"{HASH_LEGACY}{platform}",
                $"{HASH_A}_{DIGEST_A}{platform}");

            Assert.That(manifest.ResolveCdnRequestHash($"{HASH_A}{platform}"), Is.EqualTo($"{HASH_A}_{DIGEST_A}{platform}"));

            Assert.That(manifest.ResolveCdnRequestHash($"{HASH_LEGACY}{platform}"), Is.EqualTo($"{HASH_LEGACY}{platform}"),
                "Files listed without a digest must fall back to the input hash");

            Assert.That(manifest.ResolveCdnRequestHash($"{HASH_B}{platform}"), Is.EqualTo($"{HASH_B}{platform}"),
                "Hashes absent from the manifest must fall back to the input hash");
        }

        [Test]
        public void ResolveCdnRequestHashCaseInsensitivelyToManifestCasing()
        {
            string platform = PlatformUtils.GetCurrentPlatform();

            AssetBundleManifestVersion manifest = CreateV49Manifest($"qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv_{DIGEST_A}{platform}");

            // The lookup is case-insensitive and returns the manifest's casing — the CDN is case-sensitive,
            // so the verbatim entry is the one that actually exists.
            Assert.That(manifest.ResolveCdnRequestHash($"QmaBrb8WisG9b4Szzt6ACHgaJdyULTEjpzmTwDi4RCEtZV{platform}"),
                Is.EqualTo($"qmabrb8wisg9b4szzt6achgajdyultejpzmtwdi4rcetzv_{DIGEST_A}{platform}"));
        }

        [Test]
        public void ComposeCdnRequestHashFromBareHash()
        {
            string platform = PlatformUtils.GetCurrentPlatform();

            AssetBundleManifestVersion manifest = CreateV49Manifest($"{HASH_A}_{DIGEST_A}{platform}");

            Assert.That(manifest.GetCdnRequestHash(HASH_A), Is.EqualTo($"{HASH_A}_{DIGEST_A}{platform}"));

            Assert.That(manifest.GetCdnRequestHash(HASH_B), Is.EqualTo($"{HASH_B}{platform}"),
                "Hashes absent from the manifest must fall back to the platform-suffixed bare hash");

            AssetBundleManifestVersion? nullManifest = null;
            Assert.That(nullManifest.GetCdnRequestHash(HASH_A), Is.EqualTo($"{HASH_A}{platform}"));
        }

        [Test]
        public void ReportDepsDigestsOnlyWhenMapWasInjected()
        {
            // The cache-key dispatch (v49 version+hash vs legacy buildDate+hash) hinges on this: only manifests
            // that received files[] (scenes) report true. Wearable/emote manifests never fetch files[] and must
            // keep buildDate keying — their bundles are republished in place and cannot be reused across builds.
            var withoutMap = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            Assert.That(withoutMap.HasDepsDigests(), Is.False);

            AssetBundleManifestVersion withMap = CreateV49Manifest($"{HASH_A}_{DIGEST_A}_mac");
            Assert.That(withMap.HasDepsDigests(), Is.True);

            // A manifest whose files[] contained only legacy 2-part names carries no map either.
            AssetBundleManifestVersion onlyLegacyFiles = CreateV49Manifest($"{HASH_LEGACY}_mac");
            Assert.That(onlyLegacyFiles.HasDepsDigests(), Is.False);
        }

        [Test]
        public void ComposeCacheKey_FallsBackToBareHashWhenManifestIsNull()
        {
            AssetBundleManifestVersion? manifest = null;
            Assert.That(manifest.ComposeCacheKey(HASH_A), Is.EqualTo(HASH_A));
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
            AssetBundleManifestVersion manifest = CreateV49Manifest($"{HASH_A}_{DIGEST_A}_mac");

            Assert.That(manifest.ComposeCacheKey(HASH_A), Is.EqualTo($"{HASH_A}_{DIGEST_A}_mac"));
        }

        [Test]
        public void TreatIntentionsWithDifferentDigestBearingHashesAsDistinct()
        {
            // The digest is part of the Hash itself, so two dependency closures of the same bare hash never
            // collide in the in-memory AB cache.
            var a = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac");
            var b = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_B}_mac");

            Assert.That(a.Equals(b), Is.False);
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void TreatIntentionsWithSameHashAsEqual()
        {
            var a = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac");
            var b = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac");

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ProduceDistinctDiskFilenamesForDifferentDigestBearingHashes()
        {
            var a = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_A}_mac");
            var b = GetAssetBundleIntention.FromHash($"{HASH_A}_{DIGEST_B}_mac");

            using var keyA = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in a);
            using var keyB = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in b);

            Assert.That(HashNamings.HashNameFrom(keyA, ".ab"), Is.Not.EqualTo(HashNamings.HashNameFrom(keyB, ".ab")));
        }

        [Test]
        public void PreserveLegacyDiskFilenameForDigestLessHashes()
        {
            // A digest-less hash must produce the same on-disk file name as before this scheme so existing
            // cached entries keep hitting after upgrade.
            var legacy = GetAssetBundleIntention.FromHash(HASH_LEGACY);
            var alsoLegacy = GetAssetBundleIntention.FromHash(HASH_LEGACY);

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
        public void V49HashIsStableForSameInputs()
        {
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49($"{HASH_A}_{DIGEST_A}_mac", "v49");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49($"{HASH_A}_{DIGEST_A}_mac", "v49");

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void V49HashDiffersWhenDigestBearingHashDiffers()
        {
            // The digest travels inside the hash, so two dependency closures of the same bare hash produce
            // different Unity-cache keys.
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49($"{HASH_A}_{DIGEST_A}_mac", "v49");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49($"{HASH_A}_{DIGEST_B}_mac", "v49");

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void V49HashDiffersWhenVersionDiffers()
        {
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49($"{HASH_A}_{DIGEST_A}_mac", "v49");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49($"{HASH_A}_{DIGEST_A}_mac", "v50");

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void V49DelimiterPreventsBoundaryCollisions()
        {
            // Without the delimiter, (version="v4", hash="9foo") and (version="v49", hash="foo") would produce
            // the same byte stream.
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49("9foo", "v4");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49("foo", "v49");

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void LegacyHashIsStableForSameInputs()
        {
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(HASH_LEGACY, "2026-05-01");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(HASH_LEGACY, "2026-05-01");

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void LegacyHashChangesWithBuildDate()
        {
            // Pre-v49 ABs have no per-file freshness signal, so buildDate is the only thing that flushes the cache
            // when dependencies might have changed — verify it is actually contributing to the key.
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(HASH_LEGACY, "2026-05-01");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(HASH_LEGACY, "2026-05-02");

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void V49AndLegacyDoNotCollideForSameHash()
        {
            // A v49+ AB with a digest-less name must not accidentally produce the same Hash128 as the legacy path
            // for the same hash, even if the legacy buildDate happens to equal the v49 version string.
            Hash128 legacy = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(HASH_A, "v49");
            Hash128 v49 = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(HASH_A, "v49");

            Assert.That(legacy, Is.Not.EqualTo(v49));
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
