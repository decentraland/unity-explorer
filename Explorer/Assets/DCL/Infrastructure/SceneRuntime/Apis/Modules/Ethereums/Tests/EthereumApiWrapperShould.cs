using Cysharp.Threading.Tasks;
using DCL.Web3;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.Ethereums.Tests
{
    public class EthereumApiWrapperShould
    {
        private IEthereumApi ethereumApi = null!;
        private ISceneExceptionsHandler sceneExceptionsHandler = null!;
        private CancellationTokenSource disposeCts = null!;
        private EthereumApiWrapper wrapper = null!;

        [SetUp]
        public void SetUp()
        {
            ethereumApi = Substitute.For<IEthereumApi>();
            sceneExceptionsHandler = Substitute.For<ISceneExceptionsHandler>();
            disposeCts = new CancellationTokenSource();

            wrapper = new EthereumApiWrapper(ethereumApi, sceneExceptionsHandler, Substitute.For<IWeb3IdentityCache>(), disposeCts);
        }

        [TearDown]
        public void TearDown()
        {
            wrapper.Dispose();
            disposeCts.Dispose();
        }

        [Test]
        public void NotReportRequestCancelledByDispose()
        {
            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(call => UniTask.Never<EthApiResponse>(call.ArgAt<CancellationToken>(2)));

            Send();
            wrapper.Dispose();

            sceneExceptionsHandler.DidNotReceive().OnEngineException(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void ReportCancellationNotRequestedByTheScene()
        {
            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(UniTask.FromException<EthApiResponse>(new OperationCanceledException()));

            Send();

            sceneExceptionsHandler.Received(1).OnEngineException(Arg.Any<OperationCanceledException>(), Arg.Any<string>());
        }

        private void Send()
        {
            try { wrapper.SendAsync(1, "eth_blockNumber", "[]"); }
            catch (ArgumentNullException)
            {
                // No script engine in EditMode to build the returned promise; the request is already sent by then
            }
        }
    }
}
