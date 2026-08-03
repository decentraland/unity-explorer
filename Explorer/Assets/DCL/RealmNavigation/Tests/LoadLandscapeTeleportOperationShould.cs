using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using CommunicationData.URLHelpers;
using UnityEngine;

namespace DCL.RealmNavigation.TeleportOperations.Tests
{
    /// <summary>
    ///     Regression coverage for https://github.com/decentraland/unity-explorer/issues/9517.
    ///     A loading-screen timeout cancels the shared teleport-chain token while it is inside
    ///     <c>Landscape.LoadTerrainAsync</c>; that method short-circuits on cancellation by returning
    ///     a failed <see cref="EnumResult{TErrorEnum}" /> instead of throwing. If the teleport operation
    ///     doesn't inspect that result, the chain reports a successful teleport with no terrain/ground
    ///     ever generated - exactly the reported "ground/LODs missing after teleport timeout" state.
    /// </summary>
    [TestFixture]
    public class LoadLandscapeTeleportOperationShould
    {
        private ILoadingStatus loadingStatus;
        private ILandscape landscape;
        private CancellationTokenSource cts;

        [SetUp]
        public void SetUp()
        {
            loadingStatus = Substitute.For<ILoadingStatus>();
            loadingStatus.SetCurrentStage(Arg.Any<LoadingStatus.LoadingStage>()).Returns(0.6f);

            landscape = Substitute.For<ILandscape>();

            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            cts.Dispose();
        }

        [Test]
        public void NotReportSuccessWhenTerrainLoadWasCancelled()
        {
            // Simulates Landscape.LoadTerrainAsync's own cancellation short-circuit (Landscape.cs):
            // it returns a cancelled result rather than throwing, once the shared token has been
            // cancelled (e.g. by the 2-minute loading-screen timeout landing mid-chain).
            cts.Cancel();

            landscape
               .LoadTerrainAsync(Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>())
               .Returns(UniTask.FromResult(EnumResult<LandscapeError>.CancelledResult(LandscapeError.MessageError)));

            var operation = new LoadLandscapeTeleportOperation(landscape);

            EnumResult<TaskError> result = operation.ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            Assert.IsFalse(result.Success, "A cancelled terrain load must fail the teleport, not report success with no ground generated");
        }

        [Test]
        public void ReportSuccessWhenTerrainLoadSucceeded()
        {
            landscape
               .LoadTerrainAsync(Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>())
               .Returns(UniTask.FromResult(EnumResult<LandscapeError>.SuccessResult()));

            var operation = new LoadLandscapeTeleportOperation(landscape);

            EnumResult<TaskError> result = operation.ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
        }

        private TeleportParams MakeParams() =>
            new (
                URLDomain.EMPTY,
                Vector2Int.zero,
                AsyncLoadProcessReport.Create(cts.Token),
                loadingStatus,
                allowsWorldPositionOverride: false);
    }
}
