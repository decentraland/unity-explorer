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
            new (RESOLVED, ORIGINAL, isReachable: true, isLiveStream: true, resolvedUrlExpiresAt: 1234.5f, isFromContentServer: true);

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

            // IsReachableConsume compares against the pre-resolution address (what the component stores).
            Assert.IsTrue(promise.IsReachableConsume(ORIGINAL));
            Assert.IsTrue(promise.IsConsumed);
        }

        [Test]
        public void RoundTripTemplateMetadata()
        {
            VideoTemplateData original = Template();
            var promise = new OpenMediaPromise();
            promise.SeedResolved(original);

            VideoTemplateData roundTripped = promise.ToTemplateData(original.IsFromContentServer);

            Assert.AreEqual(original.ResolvedAddress.ToString(), roundTripped.ResolvedAddress.ToString());
            Assert.AreEqual(original.OriginalAddress.ToString(), roundTripped.OriginalAddress.ToString());
            Assert.AreEqual(original.IsReachable, roundTripped.IsReachable);
            Assert.AreEqual(original.IsLiveStream, roundTripped.IsLiveStream);
            Assert.AreEqual(original.ResolvedUrlExpiresAt, roundTripped.ResolvedUrlExpiresAt);
            Assert.AreEqual(original.IsFromContentServer, roundTripped.IsFromContentServer);
        }
    }
}
#endif
