using ECS.StreamableLoading.GLTF;
using NUnit.Framework;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class GetGLTFIntentionShould
    {
        [Test]
        public void EqualsTrueWhenHashAndNameMatch()
        {
            var a = GetGLTFIntention.Create("models/tree.glb", "hash-1");
            var b = GetGLTFIntention.Create("models/tree.glb", "hash-1");

            Assert.That(a.Equals(b), Is.True);
            Assert.That(b.Equals(a), Is.True);
        }

        [Test]
        public void EqualsFalseWhenOnlyNameMatches()
        {
            // Two scenes can legitimately reference the same Src path with different content hashes.
            // OR-equality on (Hash, Name) would treat these as equal and cross-contaminate cache entries.
            var a = GetGLTFIntention.Create("models/tree.glb", "hash-1");
            var b = GetGLTFIntention.Create("models/tree.glb", "hash-2");

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void EqualsFalseWhenOnlyHashMatches()
        {
            var a = GetGLTFIntention.Create("models/tree.glb", "hash-1");
            var b = GetGLTFIntention.Create("models/rock.glb", "hash-1");

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void GetHashCodeStableForEqualIntentions()
        {
            // The original bug: Equals overridden without GetHashCode, so default ValueType.GetHashCode
            // landed equal-by-Equals values in different dictionary buckets and dedup silently missed.
            var a = GetGLTFIntention.Create("models/tree.glb", "hash-1");
            var b = GetGLTFIntention.Create("models/tree.glb", "hash-1");

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void EqualsCaseInsensitiveOnHash()
        {
            // Defensive against a toolchain emitting uppercase hashes; content hashes are lowercase
            // by convention but the comparison must not silently miss on case differences.
            var a = GetGLTFIntention.Create("models/tree.glb", "abcdef");
            var b = GetGLTFIntention.Create("models/tree.glb", "ABCDEF");

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void EqualsCaseSensitiveOnName()
        {
            // Name is the file path — case sensitivity matches OS-style src lookups and the
            // sibling GetGltfContainerAssetIntention's behaviour.
            var a = GetGLTFIntention.Create("models/tree.glb", "hash-1");
            var b = GetGLTFIntention.Create("Models/Tree.glb", "hash-1");

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void DictionaryFindsExistingEntryWhenKeyEqualsByValue()
        {
            // GltfLoadCache and OngoingRequests are keyed by GetGLTFIntention. If Equals/GetHashCode
            // disagree, a follow-up consumer constructs an equal intention but TryGetValue misses,
            // spawning a duplicate load.
            var dict = new Dictionary<GetGLTFIntention, string>
            {
                [GetGLTFIntention.Create("models/tree.glb", "hash-1")] = "load-1",
            };

            var lookupKey = GetGLTFIntention.Create("models/tree.glb", "hash-1");

            Assert.That(dict.TryGetValue(lookupKey, out string value), Is.True);
            Assert.That(value, Is.EqualTo("load-1"));
        }
    }
}
