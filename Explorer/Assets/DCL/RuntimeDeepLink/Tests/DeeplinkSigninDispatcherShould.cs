using DCL.RuntimeDeepLink;
using Global.AppArgs;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.RuntimeDeepLink.Tests
{
    public class DeeplinkSigninDispatcherShould
    {
        private DeeplinkSigninDispatcher dispatcher;
        private List<string> received;

        [SetUp]
        public void SetUp()
        {
            dispatcher = new DeeplinkSigninDispatcher();
            received = new List<string>();
        }

        [Test]
        public void DeliverToALiveSubscriber()
        {
            dispatcher.Subscribe(received.Add);

            dispatcher.Dispatch("identity-1");

            Assert.That(received, Is.EqualTo(new[] { "identity-1" }));
        }

        [Test]
        public void ReplayTheBufferedSigninToALateSubscriber()
        {
            // Cold-start: the deep link arrives before the state subscribes.
            dispatcher.Dispatch("identity-1");

            dispatcher.Subscribe(received.Add);

            Assert.That(received, Is.EqualTo(new[] { "identity-1" }));
        }

        [Test]
        public void ReplaceTheBufferWithTheMostRecentSignin()
        {
            dispatcher.Dispatch("identity-old");
            dispatcher.Dispatch("identity-new");

            dispatcher.Subscribe(received.Add);

            Assert.That(received, Is.EqualTo(new[] { "identity-new" }));
        }

        [Test]
        public void ClearTheBufferWhenTheSubscriptionIsDisposed()
        {
            // A deep link consumed by a cancelled attempt must not bleed into the next attempt.
            dispatcher.Dispatch("identity-1");
            IDisposable subscription = dispatcher.Subscribe(received.Add);

            subscription.Dispose();

            var nextAttempt = new List<string>();
            dispatcher.Subscribe(nextAttempt.Add);

            Assert.That(nextAttempt, Is.Empty);
        }

        [Test]
        public void BufferTheSigninAppArgOnColdStartConstruction()
        {
            var appArgs = Substitute.For<IAppArgs>();

            appArgs.TryGetValue(AppArgsFlags.SIGNIN, out Arg.Any<string?>())
                   .Returns(call =>
                    {
                        call[1] = "identity-cold";
                        return true;
                    });

            var coldStarted = new DeeplinkSigninDispatcher(appArgs);

            var replayed = new List<string>();
            coldStarted.Subscribe(replayed.Add);

            Assert.That(replayed, Is.EqualTo(new[] { "identity-cold" }));
        }

        [Test]
        public void NotBufferWhenNoSigninAppArgIsPresent()
        {
            var appArgs = Substitute.For<IAppArgs>();
            appArgs.TryGetValue(AppArgsFlags.SIGNIN, out Arg.Any<string?>()).Returns(false);

            var coldStarted = new DeeplinkSigninDispatcher(appArgs);

            var replayed = new List<string>();
            coldStarted.Subscribe(replayed.Add);

            Assert.That(replayed, Is.Empty);
        }

        [Test]
        public void IgnoreRequestIdCorrelationInStageOne()
        {
            // Stage 1 ships the expectedRequestId / sourceRequestId params unused: a mismatch is still delivered.
            dispatcher.Subscribe(received.Add, expectedRequestId: "request-A");

            dispatcher.Dispatch("identity-1", sourceRequestId: "request-B");

            Assert.That(received, Is.EqualTo(new[] { "identity-1" }));
        }
    }
}
