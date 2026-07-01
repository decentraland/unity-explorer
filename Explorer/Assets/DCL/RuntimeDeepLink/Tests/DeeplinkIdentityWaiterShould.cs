using Cysharp.Threading.Tasks;
using DCL.RuntimeDeepLink;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.RuntimeDeepLink.Tests
{
    public class DeeplinkIdentityWaiterShould
    {
        private DeeplinkSigninDispatcher dispatcher;

        [SetUp]
        public void SetUp()
        {
            dispatcher = new DeeplinkSigninDispatcher();
        }

        [Test]
        public async Task ReturnTheIdentityIdDispatchedAfterSubscribing()
        {
            UniTask<string> waiting = DeeplinkIdentityWaiter.WaitForSigninAsync(dispatcher, TimeSpan.FromSeconds(30), null, CancellationToken.None);

            dispatcher.Dispatch("identity-1");

            string identityId = await waiting;
            Assert.That(identityId, Is.EqualTo("identity-1"));
        }

        [Test]
        public async Task ReturnTheBufferedIdentityIdWhenItArrivedBeforeWaiting()
        {
            // Cold-start: the deep link arrived (via command-line args) before the auth state began waiting.
            dispatcher.Dispatch("identity-cold");

            string identityId = await DeeplinkIdentityWaiter.WaitForSigninAsync(dispatcher, TimeSpan.FromSeconds(30), null, CancellationToken.None);

            Assert.That(identityId, Is.EqualTo("identity-cold"));
        }

        [Test]
        public void ThrowTimeoutWhenNoSigninArrivesInTime()
        {
            Assert.That(async () => await DeeplinkIdentityWaiter.WaitForSigninAsync(dispatcher, TimeSpan.FromMilliseconds(50), null, CancellationToken.None),
                Throws.InstanceOf<TimeoutException>());
        }

        [Test]
        public void ThrowOperationCanceledWhenCancelledBeforeSignin()
        {
            using var cts = new CancellationTokenSource();
            UniTask<string> waiting = DeeplinkIdentityWaiter.WaitForSigninAsync(dispatcher, TimeSpan.FromSeconds(30), null, cts.Token);

            cts.Cancel();

            Assert.That(async () => await waiting, Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task ClearTheBufferWhenCancelledSoItDoesNotBleedIntoTheNextAttempt()
        {
            using var cts = new CancellationTokenSource();
            UniTask<string> waiting = DeeplinkIdentityWaiter.WaitForSigninAsync(dispatcher, TimeSpan.FromSeconds(30), null, cts.Token);
            cts.Cancel();

            try { await waiting; }
            catch (OperationCanceledException)
            { /* expected */
            }

            // A deep link arriving after the cancelled attempt must not be replayed to the next subscriber.
            dispatcher.Dispatch("identity-late");

            string? replayed = null;

            using (dispatcher.Subscribe(id => replayed = id)) { }

            Assert.That(replayed, Is.EqualTo("identity-late"), "buffer should hold only post-cancellation arrivals, not leak the cancelled wait's slot");
        }
    }
}
