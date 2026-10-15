using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.Tests
{
    public class EmoteMessageBusProxyShould
    {
        public enum RetrySource
        {
            Pulse,
            LiveKit,
            Untagged,
        }

        private const string WALLET = "0x0000000000000000000000000000000000000001";
        private const string EMOTE = "urn:decentraland:off-chain:base-emotes:wave";

        private IEmotesMessageBus pulse = null!;
        private IEmotesMessageBus liveKit = null!;
        private MultiplayerContainer.EmoteMessageBusProxy proxy = null!;
        private MutexSync pulseSync = null!;
        private MutexSync liveKitSync = null!;

        [SetUp]
        public void SetUp()
        {
            pulse = Substitute.For<IEmotesMessageBus>();
            liveKit = Substitute.For<IEmotesMessageBus>();
            proxy = new MultiplayerContainer.EmoteMessageBusProxy(pulse, liveKit);
            pulseSync = new MutexSync();
            liveKitSync = new MutexSync();
        }

        [TearDown]
        public void TearDown()
        {
            pulseSync.Dispose();
            liveKitSync.Dispose();
        }

        [TestCase(RetrySource.Pulse)]
        [TestCase(RetrySource.LiveKit)]
        [TestCase(RetrySource.Untagged)]
        public void ReturnStartRetriesOnlyToTheirSource(RetrySource retrySource)
        {
            // Arrange
            IEmotesMessageBus? source = GetSource(retrySource);
            var intention = new RemoteEmoteIntention(new URN(EMOTE), WALLET, 1, AvatarEmoteMask.AemFullBody, source, 17);
            IEmotesMessageBus expected = retrySource == RetrySource.Pulse ? pulse : liveKit;
            IEmotesMessageBus other = retrySource == RetrySource.Pulse ? liveKit : pulse;

            // Act
            proxy.SaveForRetry(intention);

            // Assert
            expected.Received(1).SaveForRetry(Arg.Is<RemoteEmoteIntention>(value =>
                value.Equals(intention) && value.Source == source && value.RetryGeneration == 17));
            other.DidNotReceive().SaveForRetry(Arg.Any<RemoteEmoteIntention>());
        }

        [TestCase(RetrySource.Pulse)]
        [TestCase(RetrySource.LiveKit)]
        [TestCase(RetrySource.Untagged)]
        public void ReturnStopRetriesOnlyToTheirSource(RetrySource retrySource)
        {
            // Arrange
            IEmotesMessageBus? source = GetSource(retrySource);
            var intention = new RemoteEmoteStopIntention(WALLET, 1, source, 17);
            IEmotesMessageBus expected = retrySource == RetrySource.Pulse ? pulse : liveKit;
            IEmotesMessageBus other = retrySource == RetrySource.Pulse ? liveKit : pulse;

            // Act
            proxy.SaveForRetry(intention);

            // Assert
            expected.Received(1).SaveForRetry(Arg.Is<RemoteEmoteStopIntention>(value =>
                value.Equals(intention) && value.Source == source && value.RetryGeneration == 17));
            other.DidNotReceive().SaveForRetry(Arg.Any<RemoteEmoteStopIntention>());
        }

        [Test]
        public void KeepPulseProvenanceWhenEqualStartsAreCombinedAndRetried()
        {
            // Arrange
            var pulseIntention = new RemoteEmoteIntention(new URN(EMOTE), WALLET, 1, AvatarEmoteMask.AemFullBody, pulse, 17);
            var liveKitIntention = new RemoteEmoteIntention(new URN(EMOTE), WALLET, 1, AvatarEmoteMask.AemFullBody, liveKit);
            var pulseSet = new HashSet<RemoteEmoteIntention> { pulseIntention };
            var liveKitSet = new HashSet<RemoteEmoteIntention> { liveKitIntention };
            pulse.EmoteIntentions().Returns(_ => new OwnedBunch<RemoteEmoteIntention>(pulseSync, pulseSet));
            liveKit.EmoteIntentions().Returns(_ => new OwnedBunch<RemoteEmoteIntention>(liveKitSync, liveKitSet));
            RemoteEmoteIntention captured = default;
            Assert.That(pulseIntention, Is.EqualTo(liveKitIntention));

            // Act
            using (OwnedBunch<RemoteEmoteIntention> bunch = proxy.EmoteIntentions())
            {
                Assert.That(bunch.Collection(), Has.Count.EqualTo(1));
                foreach (RemoteEmoteIntention intention in bunch.Collection())
                    captured = intention;
            }
            proxy.SaveForRetry(captured);

            // Assert
            Assert.That(captured.Source, Is.SameAs(pulse));
            Assert.That(captured.RetryGeneration, Is.EqualTo(17));
            pulse.Received(1).SaveForRetry(captured);
            liveKit.DidNotReceive().SaveForRetry(Arg.Any<RemoteEmoteIntention>());
            using (OwnedBunch<RemoteEmoteIntention> next = proxy.EmoteIntentions())
                Assert.That(next.Available(), Is.False);
        }

        [Test]
        public void KeepPulseProvenanceWhenEqualStopsAreCombinedAndRetried()
        {
            // Arrange
            var pulseIntention = new RemoteEmoteStopIntention(WALLET, 1, pulse, 17);
            var liveKitIntention = new RemoteEmoteStopIntention(WALLET, 1, liveKit);
            var pulseSet = new HashSet<RemoteEmoteStopIntention> { pulseIntention };
            var liveKitSet = new HashSet<RemoteEmoteStopIntention> { liveKitIntention };
            pulse.EmoteStopIntentions().Returns(_ => new OwnedBunch<RemoteEmoteStopIntention>(pulseSync, pulseSet));
            liveKit.EmoteStopIntentions().Returns(_ => new OwnedBunch<RemoteEmoteStopIntention>(liveKitSync, liveKitSet));
            RemoteEmoteStopIntention captured = default;
            Assert.That(pulseIntention, Is.EqualTo(liveKitIntention));

            // Act
            using (OwnedBunch<RemoteEmoteStopIntention> bunch = proxy.EmoteStopIntentions())
            {
                Assert.That(bunch.Collection(), Has.Count.EqualTo(1));
                foreach (RemoteEmoteStopIntention intention in bunch.Collection())
                    captured = intention;
            }
            proxy.SaveForRetry(captured);

            // Assert
            Assert.That(captured.Source, Is.SameAs(pulse));
            Assert.That(captured.RetryGeneration, Is.EqualTo(17));
            pulse.Received(1).SaveForRetry(captured);
            liveKit.DidNotReceive().SaveForRetry(Arg.Any<RemoteEmoteStopIntention>());
            using (OwnedBunch<RemoteEmoteStopIntention> next = proxy.EmoteStopIntentions())
                Assert.That(next.Available(), Is.False);
        }

        private IEmotesMessageBus? GetSource(RetrySource source) =>
            source switch
            {
                RetrySource.Pulse => pulse,
                RetrySource.LiveKit => liveKit,
                _ => null,
            };
    }
}
