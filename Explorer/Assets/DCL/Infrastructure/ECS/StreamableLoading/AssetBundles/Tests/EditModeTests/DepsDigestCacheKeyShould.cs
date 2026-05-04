using ECS.StreamableLoading.Cache.Disk;
using NUnit.Framework;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class DepsDigestCacheKeyShould
    {
        private const string DIGEST_A = "dda1af30bdf4a19ce03e663a9a288afe";
        private const string DIGEST_B = "243f68977939e1f526b4c1a05a40b43a";

        [Test]
        public void ExtractDepsDigestsFromThreePartFilenames()
        {
            string[] files =
            {
                "bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru_mac",
                $"bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4_{DIGEST_A}_mac",
                $"bafkreice3qpeyeb4ni7fnlt6bijs57zrbuw7cwmbymzcndyllzho3hbgaa_{DIGEST_B}_mac",
            };

            var map = SceneAssetBundleManifest.ExtractDepsDigests(files);

            Assert.That(map, Is.Not.Null);
            Assert.That(map!.Count, Is.EqualTo(2));

            var manifest = new SceneAssetBundleManifest("v49", "2026-05-01", map);

            Assert.That(manifest.TryGetDepsDigest("bafybeih4xx65yycsf2vx6sari7myjho6rugqox4ocd2tzjhfam73g2trru", out _), Is.False, "Legacy 2-part filenames must not contribute a digest");
            Assert.That(manifest.TryGetDepsDigest("bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4", out string digestA), Is.True);
            Assert.That(digestA, Is.EqualTo(DIGEST_A));
            Assert.That(manifest.TryGetDepsDigest("bafkreice3qpeyeb4ni7fnlt6bijs57zrbuw7cwmbymzcndyllzho3hbgaa", out string digestB), Is.True);
            Assert.That(digestB, Is.EqualTo(DIGEST_B));
        }

        [Test]
        public void RejectInvalidDigestTokens()
        {
            string[] files =
            {
                // Middle token is not 32 hex chars
                "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4_notahex_mac",
                // Right length but contains non-hex character
                "bafkreif5xmg4un7cm4ouyqfoluc6ifcdouiatassnv5pykell4e4mw5xc4_zda1af30bdf4a19ce03e663a9a288afe_mac",
            };

            var map = SceneAssetBundleManifest.ExtractDepsDigests(files);

            Assert.That(map, Is.Null);
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
    }
}
