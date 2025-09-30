using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Pools;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.V3;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools.Memory;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    public class SignFlowShould
    {
        private IArchipelagoLiveConnection connectionMock;
        private LiveConnectionArchipelagoSignFlow signFlow;
        private IMemoryPool memoryPool;

        [SetUp]
        public void Setup()
        {
            connectionMock = Substitute.For<IArchipelagoLiveConnection>();
            memoryPool = new ArrayMemoryPool();
            var dclMultiPool = new DCLMultiPool();
            signFlow = new LiveConnectionArchipelagoSignFlow(new ArchipelagoSignedConnection(connectionMock, dclMultiPool, memoryPool, new IWeb3IdentityCache.Fake()), memoryPool, dclMultiPool);
        }

        [Test]
        public async Task StartListeningForConnectionStringAsync_ShouldContinueOnErrorAsync([Values(IArchipelagoLiveConnection.ResponseError.MessageError)]
            IArchipelagoLiveConnection.ResponseError error)
        {
            var onNewConnection = Substitute.For<Action<string>>();

            var cts = new CancellationTokenSource();

            try
            {

                LogAssert.ignoreFailingMessages = true;

                async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveErrorAsync(CancellationToken ct)
                {
                    await UniTask.Delay(50, cancellationToken: ct).SuppressCancellationThrow();
                    return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(error, "TEST ERROR");
                }

                connectionMock.ReceiveAsync(Arg.Any<CancellationToken>())
                              .Returns(arg => ReceiveErrorAsync(arg.Arg<CancellationToken>()));

                signFlow.StartListeningForConnectionStringAsync(onNewConnection, cts.Token).Forget();

                onNewConnection.DidNotReceive().Invoke(Arg.Any<string>());

                var successPacket = new ServerPacket
                {
                    IslandChanged = new IslandChangedMessage
                    {
                        IslandId = "test",
                        ConnStr = "test_connection_string"
                    }
                };

                async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveSuccessAsync(CancellationToken ct)
                {
                    var memory = memoryPool.Memory(successPacket);
                    successPacket.WriteTo(memory.Span());

                    await UniTask.Delay(5, cancellationToken: ct).SuppressCancellationThrow();
                    return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.SuccessResult(memory);
                }

                connectionMock.ReceiveAsync(Arg.Any<CancellationToken>())
                              .Returns(arg => ReceiveSuccessAsync(arg.Arg<CancellationToken>()));

                await Task.Delay(500);

                onNewConnection.Received().Invoke(Arg.Is<string>(s => s == successPacket.IslandChanged.ConnStr));
            }
            finally
            {
                cts.Cancel();
                cts.Dispose();
            }

            await Task.Yield();
        }
    }
}
