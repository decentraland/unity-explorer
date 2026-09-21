using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Time;
using DCL.WebRequests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.PluginSystem.Global.Tests
{
    /// <summary>
    ///     Regression coverage for https://github.com/decentraland/unity-explorer/issues/10075.
    ///     <see cref="RealmClock" />'s monotonic anchor stops while the OS sleeps, so a sample recorded before
    ///     sleep reads as a large desync after wake even though the user's clock is fine. The check must discard
    ///     that sample and re-probe before blaming the user, and every Retry must re-probe as well.
    /// </summary>
    [TestFixture]
    public class EnsureClockSyncShould
    {
        // Well above EnsureClockSync's 60s desync threshold
        private static readonly TimeSpan STALE_OFFSET = TimeSpan.FromMinutes(10);

        private RealmClock realmClock = null!;
        private EnsureClockSync ensureClockSync = null!;
        private Queue<EnsureClockSync.Result> userResponses = null!;
        private Queue<Action> serverResponses = null!;
        private int probeCount;
        private int promptCount;

        [SetUp]
        public void SetUp()
        {
            realmClock = new RealmClock();
            userResponses = new Queue<EnsureClockSync.Result>();
            serverResponses = new Queue<Action>();
            probeCount = 0;
            promptCount = 0;

            // IsHeadReachableAsync bottoms out in this generic SendAsync call; the production controller records
            // the response's Date header into the RealmClock, which the queued server responses stand in for.
            IWebRequestController webRequestController = Substitute.For<IWebRequestController>();

            webRequestController
               .SendAsync<GenericHeadRequest, GenericHeadArguments, WebRequestUtils.NoOp<GenericHeadRequest>, WebRequestUtils.NoResult>(
                    Arg.Any<RequestEnvelope<GenericHeadRequest, GenericHeadArguments>>(),
                    Arg.Any<WebRequestUtils.NoOp<GenericHeadRequest>>(),
                    Arg.Any<long>(),
                    Arg.Any<IProgress<float>?>())
               .Returns(_ =>
                {
                    probeCount++;
                    serverResponses.Dequeue().Invoke();
                    return UniTask.FromResult(default(WebRequestUtils.NoResult));
                });

            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(Arg.Any<DecentralandUrl>()).Returns("https://example.com");

            ensureClockSync = new EnsureClockSync(realmClock, webRequestController, RequestUserActionAsync, urlsSource);
        }

        [Test]
        public async Task SelfHealStaleSampleWithoutPromptingUser()
        {
            RecordStaleSample();
            serverResponses.Enqueue(RecordFreshSample);

            await ensureClockSync.ExecuteAsync(CancellationToken.None);

            Assert.AreEqual(0, promptCount, "A sample the anchor left behind is not a user clock problem");
            Assert.AreEqual(1, probeCount, "The stale sample must be discarded and re-probed exactly once");
            Assert.IsTrue(realmClock.HasSample);
            Assert.Less(Math.Abs((realmClock.UtcNow!.Value - DateTime.UtcNow).TotalSeconds), 5d);
        }

        [Test]
        public async Task PromptUserWhenDesyncPersistsAfterReprobe()
        {
            RecordStaleSample();
            serverResponses.Enqueue(RecordStaleSample);
            userResponses.Enqueue(EnsureClockSync.Result.Continue);

            await ensureClockSync.ExecuteAsync(CancellationToken.None);

            Assert.AreEqual(1, probeCount);
            Assert.AreEqual(1, promptCount, "A desync that survives a fresh sample is a real one and must be surfaced");
        }

        [Test]
        public async Task ReprobeOnEachRetry()
        {
            RecordStaleSample();
            serverResponses.Enqueue(RecordStaleSample);
            serverResponses.Enqueue(RecordFreshSample);
            userResponses.Enqueue(EnsureClockSync.Result.Restart);

            await ensureClockSync.ExecuteAsync(CancellationToken.None);

            Assert.AreEqual(2, probeCount, "Retry must hit the server again instead of re-reading the same sample");
            Assert.AreEqual(1, promptCount, "The retry resolved the desync, so the dialog must not reappear");
        }

        private UniTask<EnsureClockSync.Result> RequestUserActionAsync(CancellationToken ct)
        {
            promptCount++;
            return UniTask.FromResult(userResponses.Dequeue());
        }

        private void RecordStaleSample() =>
            realmClock.RecordServerTime(DateTime.UtcNow - STALE_OFFSET);

        private void RecordFreshSample() =>
            realmClock.RecordServerTime(DateTime.UtcNow);
    }
}
