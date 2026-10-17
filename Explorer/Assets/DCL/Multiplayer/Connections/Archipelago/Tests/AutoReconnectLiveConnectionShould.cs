using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Pools;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using LiveKit.Internal.FFIClients.Pools.Memory;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    public class AutoReconnectLiveConnectionShould
    {
        private IArchipelagoLiveConnection origin;
        private ArchipelagoSignedConnection autoReconnect;

        [SetUp]
        public void Setup()
        {
            origin = Substitute.For<IArchipelagoLiveConnection>();
            autoReconnect = new ArchipelagoSignedConnection(origin, TimeSpan.Zero, new DCLMultiPool(), new ArrayMemoryPool(), new IWeb3IdentityCache.Fake()); // UniTask.Delay is unpredictable in tests
        }

        [Test]
        [TestCaseSource(nameof(TIMEOUT_VALUES))]
        public async Task SpinConnectAsync(int ms)
        {
            await SpinFunctionUntilCancelled(ms, async token => await autoReconnect.ConnectAsync("test", token));
        }

        [Test]
        [TestCaseSource(nameof(TIMEOUT_VALUES))]
        public async Task SpinSendAsync(int ms)
        {
            async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendError()
            {
                await UniTask.Delay(50);
                return EnumResult<IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "Test Error");
            }

            origin.SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>())
                  .Returns(args => SendError());

            await SpinFunctionUntilCancelled(ms, async token => await autoReconnect.SendAsync(new MemoryWrap(), token));
        }

        [Test]
        [TestCaseSource(nameof(TIMEOUT_VALUES))]
        public async Task SpinReceiveAsync(int ms)
        {
            async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveError()
            {
                await UniTask.Delay(50);
                return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "Test Error");
            }

            origin.ReceiveAsync(Arg.Any<CancellationToken>())
                  .Returns(args => ReceiveError());

            await SpinFunctionUntilCancelled(ms, async token => await autoReconnect.ReceiveAsync(token));
        }

        private async Task SpinFunctionUntilCancelled(int ms, Func<CancellationToken, UniTask> func)
        {
            MockConnectAsyncError();

            var cts = new CancellationTokenSource();

            async UniTask WaitAndCancel()
            {
                await UniTask.Delay(ms);
                cts.Cancel();
            }

            var funcFinished = false;

            func(cts.Token).ContinueWith(() => funcFinished = true).Forget();

            int winIndex = await UniTask.WhenAny(WaitAndCancel(), UniTask.WaitUntil(() => funcFinished));
            Assert.That(winIndex, Is.EqualTo(0));

            await Task.Delay(ms + 500);

            Assert.That(funcFinished, Is.True);
        }

        private void MockConnectAsyncError()
        {
            origin.IsConnected.Returns(false);

            async UniTask<Result> ConnectAsync(CancellationToken token)
            {
                await UniTask.Delay(50, cancellationToken: token).SuppressCancellationThrow();
                return Result.ErrorResult("Test Error");
            }

            origin.ConnectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(args => ConnectAsync(args.Arg<CancellationToken>()));
        }

        private static readonly int[] TIMEOUT_VALUES = { 2, 200 };
    }
}
