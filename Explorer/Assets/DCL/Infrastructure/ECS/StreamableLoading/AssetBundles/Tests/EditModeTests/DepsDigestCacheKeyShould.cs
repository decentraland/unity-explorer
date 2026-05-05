using DCL.Ipfs;
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

        [Test]
        public void InjectDepsDigestsFromThreePartFilenames()
        {
            string[] files =
            {
                "bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru_mac",
                $"bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4_{DIGEST_A}_mac",
                $"bafkreice3qpeyeb4ni7fnlt6bijs57zrbuw7cwmbymzcndyllzho3hbgaa_{DIGEST_B}_mac",
            };

            var manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            manifest.InjectDepsDigests(files);

            Assert.That(manifest.TryGetDepsDigest("bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru", out _), Is.False, "Legacy 2-part filenames must not contribute a digest");
            Assert.That(manifest.TryGetDepsDigest("bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4", out string digestA), Is.True);
            Assert.That(digestA, Is.EqualTo(DIGEST_A));
            Assert.That(manifest.TryGetDepsDigest("bafkreice3qpeyeb4ni7fnlt6bijs57zrbuw7cwmbymzcndyllzho3hbgaa", out string digestB), Is.True);
            Assert.That(digestB, Is.EqualTo(DIGEST_B));
        }

        [Test]
        public void TreatIntentionsWithDifferentDigestsAsDistinct()
        {
            var sameHash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            var a = GetAssetBundleIntention.FromHash(sameHash);
            a.DepsDigest = DIGEST_A;
            var b = GetAssetBundleIntention.FromHash(sameHash);
            b.DepsDigest = DIGEST_B;

            Assert.That(a.Equals(b), Is.False);
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void TreatIntentionsWithSameHashAndDigestAsEqual()
        {
            var sameHash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            var a = GetAssetBundleIntention.FromHash(sameHash);
            a.DepsDigest = DIGEST_A;
            var b = GetAssetBundleIntention.FromHash(sameHash);
            b.DepsDigest = DIGEST_A;

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void PreserveLegacyEqualityWhenNoDigestPresent()
        {
            // Two intentions with the same hash and no digest must still match — preserves cache hits for pre-v49 entries.
            var sameHash = "bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru";
            var a = GetAssetBundleIntention.FromHash(sameHash);
            var b = GetAssetBundleIntention.FromHash(sameHash);

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ProduceDistinctDiskFilenamesForDifferentDigests()
        {
            var sameHash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            var a = GetAssetBundleIntention.FromHash(sameHash);
            a.DepsDigest = DIGEST_A;
            var b = GetAssetBundleIntention.FromHash(sameHash);
            b.DepsDigest = DIGEST_B;

            using var keyA = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in a);
            using var keyB = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in b);

            string nameA = HashNamings.HashNameFrom(keyA, ".ab");
            string nameB = HashNamings.HashNameFrom(keyB, ".ab");

            Assert.That(nameA, Is.Not.EqualTo(nameB));
        }

        [Test]
        public void PreserveLegacyDiskFilenameWhenNoDigest()
        {
            // An intention without a digest must produce the same on-disk file name as before this change so existing
            // cached entries keep hitting after upgrade.
            var sameHash = "bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru";
            var legacy = GetAssetBundleIntention.FromHash(sameHash);
            var alsoLegacy = GetAssetBundleIntention.FromHash(sameHash);

            using var keyA = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in legacy);
            using var keyB = GetAssetBundleIntention.DiskHashCompute.INSTANCE.ComputeHash(in alsoLegacy);

            Assert.That(HashNamings.HashNameFrom(keyA, ".ab"), Is.EqualTo(HashNamings.HashNameFrom(keyB, ".ab")));
        }

        [Test]
        public void GltfIntentionDefaultsCacheKeyToHash()
        {
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            var intention = new GetGltfContainerAssetIntention("model.glb", hash, new CancellationTokenSource());

            Assert.That(intention.CacheKey, Is.EqualTo(hash), "Legacy callers that don't supply a cache key must default to the bare hash");
        }

        [Test]
        public void GltfIntentionStoresPassedCacheKeyVerbatim()
        {
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            string customKey = $"{hash}@{DIGEST_A}";
            var intention = new GetGltfContainerAssetIntention("model.glb", hash, new CancellationTokenSource(), customKey);

            Assert.That(intention.CacheKey, Is.EqualTo(customKey));
        }

        [Test]
        public void GltfIntentionsWithDifferentCacheKeysAreDistinct()
        {
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            var a = new GetGltfContainerAssetIntention("model.glb", hash, new CancellationTokenSource(), $"{hash}@{DIGEST_A}");
            var b = new GetGltfContainerAssetIntention("model.glb", hash, new CancellationTokenSource(), $"{hash}@{DIGEST_B}");

            Assert.That(a.CacheKey, Is.Not.EqualTo(b.CacheKey));
            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void ComposeCacheKey_FallsBackToBareHashWhenManifestIsNull()
        {
            AssetBundleManifestVersion? manifest = null;
            Assert.That(manifest.ComposeCacheKey("X"), Is.EqualTo("X"));
        }

        [Test]
        public void ComposeCacheKey_FallsBackToBareHashWhenNoDigest()
        {
            var manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            Assert.That(manifest.ComposeCacheKey("X"), Is.EqualTo("X"));
        }

        [Test]
        public void ComposeCacheKey_AppendsDigestWhenPresent()
        {
            var manifest = AssetBundleManifestVersion.CreateFromFallback("v49", "2026-05-01");
            manifest.InjectDepsDigests(new[] { $"X_{DIGEST_A}_mac" });

            Assert.That(manifest.ComposeCacheKey("X"), Is.EqualTo($"X@{DIGEST_A}"));
        }

        [Test]
        public void V49HashIsStableForSameInputs()
        {
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", DIGEST_A);
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", DIGEST_A);

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void V49HashAcceptsEmptyDigestForLeafBundles()
        {
            // v49+ leaf ABs that aren't listed in the manifest's deps map carry an empty digest. They must still
            // produce a deterministic key — and crucially, one that doesn't depend on buildDate.
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", string.Empty);
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", string.Empty);

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void V49HashDiffersWhenDigestDiffers()
        {
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", DIGEST_A);
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", DIGEST_B);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void V49HashDiffersBetweenEmptyAndNonEmptyDigest()
        {
            // A v49+ leaf AB (no digest) and a v49+ AB with a digest must not share a cache key, even though
            // both go through the v49 path — the digest is a real discriminator and absence is meaningful.
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            Hash128 leaf = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", string.Empty);
            Hash128 withDigest = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", DIGEST_A);

            Assert.That(leaf, Is.Not.EqualTo(withDigest));
        }

        [Test]
        public void V49HashDiffersWhenHashDiffers()
        {
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49("hashA", "v49", DIGEST_A);
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49("hashB", "v49", DIGEST_A);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void V49HashDiffersWhenVersionDiffers()
        {
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", DIGEST_A);
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v50", DIGEST_A);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void V49DelimiterPreventsBoundaryCollisions()
        {
            // Without delimiters, (version="v4", hash="9foo") and (version="v49", hash="foo") would produce the
            // same byte stream.
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49("9foo", "v4", string.Empty);
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49("foo", "v49", string.Empty);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void LegacyHashIsStableForSameInputs()
        {
            const string hash = "bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru";
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(hash, "2026-05-01");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(hash, "2026-05-01");

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void LegacyHashChangesWithBuildDate()
        {
            // Pre-v49 ABs have no per-file freshness signal, so buildDate is the only thing that flushes the cache
            // when dependencies might have changed — verify it is actually contributing to the key.
            const string hash = "bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru";
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(hash, "2026-05-01");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(hash, "2026-05-02");

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void LegacyDelimiterPreventsBoundaryCollisions()
        {
            // Without a delimiter, (buildDate="2026-05-01", hash="Xhash") and (buildDate="2026-05-01X", hash="hash")
            // would concatenate to the same byte stream.
            Hash128 a = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy("Xhash", "2026-05-01");
            Hash128 b = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy("hash", "2026-05-01X");

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void V49AndLegacyDoNotCollideForSameHash()
        {
            // A v49+ leaf AB with no digest must not accidentally produce the same Hash128 as the legacy path for
            // the same bare hash, even if the legacy buildDate happens to equal the v49 version string.
            const string hash = "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4";
            Hash128 legacy = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashLegacy(hash, "v49");
            Hash128 v49 = PrepareAssetBundleLoadingParametersSystemBase.ComputeHashV49(hash, "v49", string.Empty);

            Assert.That(legacy, Is.Not.EqualTo(v49));
        }
    }
}
