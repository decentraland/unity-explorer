using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Notifications.Tests
{
    public class NotificationsRequestControllerShould
    {
        private static readonly TimeSpan TEST_POLL_INTERVAL = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan POLL_OBSERVATION_TIMEOUT = TimeSpan.FromSeconds(10);

        private NotificationsRequestController controller = null!;
        private IWebRequestController webRequestController = null!;
        private IWeb3IdentityCache identityCache = null!;
        private IDecentralandUrlsSource urlsSource = null!;

        private readonly List<List<INotification>> capturedTargets = new ();
        private readonly List<int> callTimeCounts = new ();

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            NotificationsRequestController.PollIntervalOverrideForTest = TEST_POLL_INTERVAL;

            NotificationsBusController.Initialize(new NotificationsBusController());

            capturedTargets.Clear();
            callTimeCounts.Clear();

            urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.Notifications).Returns("https://notifications.test");
            urlsSource.GetOriginalUrl(Arg.Any<string>()).Returns("https://notifications.test/notifications");

            identityCache = Substitute.For<IWeb3IdentityCache>();
            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.IsExpired.Returns(false);
            identityCache.Identity.Returns(identity);

            INotification cannedNotification = Substitute.For<INotification>();
            cannedNotification.Id.Returns("notification-1");

            webRequestController = Substitute.For<IWebRequestController>();

            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<List<INotification>, GenericGetRequest>, List<INotification>>(
                                     Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                                     Arg.Any<GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<List<INotification>, GenericGetRequest>>())!
                                .Returns(callInfo =>
                                 {
                                     GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<List<INotification>, GenericGetRequest> op =
                                         callInfo.ArgAt<GenericDownloadHandlerUtils.OverwriteFromJsonAsyncOp<List<INotification>, GenericGetRequest>>(1);

                                     capturedTargets.Add(op.Target);
                                     callTimeCounts.Add(op.Target.Count);
                                     op.Target.Add(cannedNotification);
                                     return UniTask.FromResult(op.Target);
                                 });

            controller = new NotificationsRequestController(webRequestController, urlsSource, identityCache);
        }

        [TearDown]
        public void TearDown() =>
            NotificationsRequestController.PollIntervalOverrideForTest = null;

        [Test]
        public async Task ReuseSingleListInstanceAcrossPollIterations()
        {
            var cts = new CancellationTokenSource();
            UniTask loopTask = controller.StartGettingNewNotificationsOverTimeAsync(cts.Token);

            // Two poll iterations at the injected test cadence
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (capturedTargets.Count < 2 && stopwatch.Elapsed < POLL_OBSERVATION_TIMEOUT)
                await UniTask.Yield();

            cts.Cancel();
            await loopTask;

            Assert.That(capturedTargets.Count, Is.GreaterThanOrEqualTo(2),
                "the poll loop must parse into a reusable buffer via the Overwrite op instead of allocating a fresh List per poll");

            Assert.That(capturedTargets[1], Is.SameAs(capturedTargets[0]),
                "the same buffer instance must be reused across poll iterations");

            Assert.That(callTimeCounts, Is.All.EqualTo(0),
                "the buffer must be cleared before each poll so already-dispatched notifications are not delivered again");
        }
    }
}
