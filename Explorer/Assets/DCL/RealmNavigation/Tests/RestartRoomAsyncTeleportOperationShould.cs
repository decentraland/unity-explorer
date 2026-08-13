using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility.Types;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using CommunicationData.URLHelpers;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations.Tests
{
    // Regression (UNITY-EXPLORER-NRB): a stalled livekit room restart threw TimeoutException and failed the whole teleport.
    [TestFixture]
    public class RestartRoomAsyncTeleportOperationShould
    {
        private static readonly TimeSpan LIVEKIT_TIMEOUT = TimeSpan.FromMilliseconds(50);
        private const float FINALIZATION_PROGRESS = 0.99f;

                private IRoomHub roomHub = null!;
                private ILoadingStatus loadingStatus = null!;
                private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            roomHub = Substitute.For<IRoomHub>();

            loadingStatus = Substitute.For<ILoadingStatus>();
            loadingStatus.SetCurrentStage(Arg.Any<LoadingStatus.LoadingStage>()).Returns(FINALIZATION_PROGRESS);

            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            cts.Dispose();
        }

        [Test]
        public void FinalizeTeleportWhenLivekitRestartTimesOut()
        {
            // Modeled as an already-faulted StartAsync so .Timeout() propagates the TimeoutException
            // synchronously - deterministic, no player-loop wait.
            roomHub.StartAsync().Returns(UniTask.FromException<bool>(new TimeoutException("Exceed Timeout:00:00:00.0500000")));

            var report = AsyncLoadProcessReport.Create(cts.Token);
            var operation = new RestartRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT);

            EnumResult<TaskError> result =
                operation.ExecuteAsync(MakeParams(report), cts.Token).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success,
                "A livekit restart timeout must not fail the teleport; it should be swallowed and the teleport finalized.");
            Assert.AreEqual(FINALIZATION_PROGRESS, report.ProgressCounter.Value,
                "Report.SetProgress(finalizationProgress) must run so the teleport completes.");
        }

        private TeleportParams MakeParams(AsyncLoadProcessReport report) =>
            new (
                URLDomain.EMPTY,
                Vector2Int.zero,
                report,
                loadingStatus,
                allowsWorldPositionOverride: false);
    }
}
