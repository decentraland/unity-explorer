#if AV_PRO_PRESENT
using NUnit.Framework;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers the pre-load-effect-preserving path: a freshly built player is seeded from cached
    ///     <see cref="VideoTemplateData" /> (see MediaFactory.CreateMediaPlayerComponent) so it skips the
    ///     reachability/resolution round-trip but still opens its own stream.
    /// </summary>
    public class OpenMediaPromiseSeedShould
    {
        private static readonly MediaAddress ORIGINAL = MediaAddress.New("https://peer.example/content/contents/QmHash");
        private static readonly MediaAddress RESOLVED = MediaAddress.New("https://cdn.example/direct.mp4");

        private static VideoTemplateData Template() =>
            new (RESOLVED, ORIGINAL, new ResolvedMediaUrl("https://cdn.example/direct.mp4", isReachable: true, isLiveStream: true, expiresAtRealtimeSinceStartup: 1234.5f));

        [Test]
        public void LeaveResolvedNotConsumed_SoConsumerStillOpensItsOwnStream()
        {
            var promise = new OpenMediaPromise();

            promise.SeedResolved(Template());

            Assert.IsTrue(promise.IsResolved);
            Assert.IsFalse(promise.IsConsumed);
        }

        [Test]
        public void ConsumeAgainstTheOriginalAddress()
        {
            var promise = new OpenMediaPromise();
            promise.SeedResolved(Template());

            // consume matches against the pre-resolution address, not the resolved one
            Assert.IsTrue(promise.IsReachableConsume(ORIGINAL));
            Assert.IsTrue(promise.IsConsumed);
        }

        [Test]
        public void RoundTripTemplateMetadata()
        {
            VideoTemplateData original = Template();
            var promise = new OpenMediaPromise();
            promise.SeedResolved(original);

            VideoTemplateData roundTripped = promise.ToTemplateData();

            Assert.AreEqual(original.ResolvedAddress.ToString(), roundTripped.ResolvedAddress.ToString());
            Assert.AreEqual(original.OriginalAddress.ToString(), roundTripped.OriginalAddress.ToString());
            Assert.AreEqual(original.Resolved.IsReachable, roundTripped.Resolved.IsReachable);
            Assert.AreEqual(original.Resolved.IsLiveStream, roundTripped.Resolved.IsLiveStream);
            Assert.AreEqual(original.Resolved.ExpiresAtRealtimeSinceStartup, roundTripped.Resolved.ExpiresAtRealtimeSinceStartup);
        }
    }
}
#endif
