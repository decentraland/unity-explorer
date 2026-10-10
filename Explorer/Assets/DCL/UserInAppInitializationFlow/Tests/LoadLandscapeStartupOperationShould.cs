using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using NSubstitute;
using NUnit.Framework;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class LoadLandscapeStartupOperationShould
    {
        private World world;
        private ILoadingStatus loadingStatus;
        private ILandscape landscape;
        private CancellationTokenSource cts;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            loadingStatus = Substitute.For<ILoadingStatus>();
            loadingStatus.SetCurrentStage(Arg.Any<LoadingStatus.LoadingStage>()).Returns(0.5f);

            landscape = Substitute.For<ILandscape>();
            landscape
                .LoadTerrainAsync(Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>())
                .Returns(UniTask.FromResult(EnumResult<LandscapeError>.SuccessResult()));

            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            cts.Dispose();
        }

        [Test]
        public void CallsLoadTerrainAsync()
        {
            var operation = new LoadLandscapeStartupOperation(loadingStatus, landscape);
            operation.ExecuteAsync(MakeParams(), cts.Token).GetAwaiter().GetResult();

            landscape.Received(1)
                .LoadTerrainAsync(Arg.Any<AsyncLoadProcessReport>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void ReportsProgressAfterLoad()
        {
            var operation = new LoadLandscapeStartupOperation(loadingStatus, landscape);
            var report = AsyncLoadProcessReport.Create(cts.Token);

            var flowParams = new UserInAppInitializationFlowParameters(
                showAuthentication: false,
                showLoading: false,
                loadSource: IUserInAppInitializationFlow.LoadSource.StartUp,
                world: world,
                playerEntity: default);

            operation.ExecuteAsync(new IStartupOperation.Params(report, flowParams), cts.Token).GetAwaiter().GetResult();

            Assert.AreEqual(0.5f, report.ProgressCounter.Value);
        }

        private IStartupOperation.Params MakeParams()
        {
            var flowParams = new UserInAppInitializationFlowParameters(
                showAuthentication: false,
                showLoading: false,
                loadSource: IUserInAppInitializationFlow.LoadSource.StartUp,
                world: world,
                playerEntity: default);

            return new IStartupOperation.Params(AsyncLoadProcessReport.Create(cts.Token), flowParams);
        }
    }
}
