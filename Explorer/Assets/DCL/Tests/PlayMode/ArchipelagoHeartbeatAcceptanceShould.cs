using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Web3.Identities;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;
using UnityEngine.Pool;
using DCL.Utility.Types;
using Decentraland.Kernel.Comms.V3;
using Global.AppArgs;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools.Memory;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Tests.PlayMode
{
    [TestFixture]
    public class ArchipelagoHeartbeatAcceptanceShould
    {
        private const string ADAPTER_URL = "wss://archipelago.example.com/ws";
        private const string CONNECTION_STRING = "wss://livekit.example.com?access_token=test";
        private const string ISLAND_ID = "island-test";

        private static readonly Vector3 PLAYER_POSITION = new (8f, 1.7f, 16f);

        private IArchipelagoLiveConnection connection = null!;
        private IMemoryPool memoryPool = null!;
        private DCLMultiPool multiPool = null!;
        private List<ClientPacket> sentPackets = null!;

        [SetUp]
        public void SetUp()
        {
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
            LogAssert.ignoreFailingMessages = false;

            connection = Substitute.For<IArchipelagoLiveConnection>();
            memoryPool = new ArrayMemoryPool();
            multiPool = new DCLMultiPool();
            sentPackets = new List<ClientPacket>();

            connection.SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>())
                      .Returns(call =>
                       {
                           sentPackets.Add(ClientPacket.Parser.ParseFrom(call.Arg<MemoryWrap>().Span()));
                           return UniTask.FromResult(EnumResult<IArchipelagoLiveConnection.ResponseError>.SuccessResult());
                       });
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [UnityTest]
        public IEnumerator SendNoHeartbeatWhenTheFlagIsOffInPlayMode() =>
            UniTask.ToCoroutine(async () =>
            {
                ArchipelagoIslandRoom room = NewRoom(NewFlags(false), NewSignFlow());

                await room.SendHeartbeatIfEnabledAsync(CancellationToken.None);

                AssertNoHeartbeatWasSent();
            });

        [UnityTest]
        public IEnumerator KeepTheDefaultHeartbeatWhenTheFlagIsAbsentInPlayMode() =>
            UniTask.ToCoroutine(async () =>
            {
                ArchipelagoIslandRoom room = NewRoom(FeatureFlagsResultDto.Empty, NewSignFlow());

                await room.SendHeartbeatIfEnabledAsync(CancellationToken.None);

                AssertOneHeartbeatWasSent();
            });

        [UnityTest]
        public IEnumerator KeepTheHeartbeatWhenTheFlagIsOnInPlayMode() =>
            UniTask.ToCoroutine(async () =>
            {
                ArchipelagoIslandRoom room = NewRoom(NewFlags(true), NewSignFlow());

                await room.SendHeartbeatIfEnabledAsync(CancellationToken.None);

                AssertOneHeartbeatWasSent();
            });

        [UnityTest]
        public IEnumerator ReceiveAnIslandAssignmentAfterSocketRecoveryWithoutAHeartbeat() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var assignment = new UniTaskCompletionSource<(string islandId, string connectionString)>();
                var receiveCalls = 0;

                connection.ReceiveAsync(Arg.Any<CancellationToken>())
                          .Returns(call =>
                           {
                               int callNumber = Interlocked.Increment(ref receiveCalls);

                               return callNumber switch
                                      {
                                          1 => UniTask.FromResult(EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(
                                              IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "simulated socket drop")),
                                          2 => UniTask.FromResult(IslandAssignment(ISLAND_ID, CONNECTION_STRING)),
                                          _ => WaitForCancellationAsync(call.Arg<CancellationToken>()),
                                      };
                           });

                LiveConnectionArchipelagoSignFlow signFlow = NewSignFlow();
                ArchipelagoIslandRoom room = NewRoom(NewFlags(false), signFlow);
                LogAssert.ignoreFailingMessages = true;

                // Act
                await room.SendHeartbeatIfEnabledAsync(cts.Token);
                signFlow.StartListeningForConnectionStringAsync((islandId, connectionString) => assignment.TrySetResult((islandId, connectionString)), cts.Token).Forget();
                (string islandId, string connectionString) received = await assignment.Task.Timeout(TimeSpan.FromSeconds(4));
                cts.Cancel();

                // Assert
                Assert.AreEqual(ISLAND_ID, received.islandId);
                Assert.AreEqual(CONNECTION_STRING, received.connectionString);
                Assert.GreaterOrEqual(receiveCalls, 2);
                AssertNoHeartbeatWasSent();
            });

        [UnityTest]
        public IEnumerator ReceiveAnIslandAssignmentAfterAForcedFreshHandshakeWithoutAHeartbeat() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var assignment = new UniTaskCompletionSource<(string islandId, string connectionString)>();
                var response = new UniTaskCompletionSource<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>>();
                var receiveCalls = 0;

                connection.ConnectAsync(ADAPTER_URL, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(Result.SuccessResult()));
                connection.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(Result.SuccessResult()));
                connection.ReceiveAsync(Arg.Any<CancellationToken>())
                          .Returns(call => Interlocked.Increment(ref receiveCalls) == 1
                               ? response.Task
                               : WaitForCancellationAsync(call.Arg<CancellationToken>()));

                LiveConnectionArchipelagoSignFlow signFlow = NewSignFlow();
                ICurrentAdapterAddress adapterAddress = Substitute.For<ICurrentAdapterAddress>();
                adapterAddress.AdapterUrl().Returns(ADAPTER_URL);
                ArchipelagoIslandRoom room = NewRoom(NewFlags(false), signFlow, adapterAddress);

                // Act
                signFlow.StartListeningForConnectionStringAsync((islandId, connectionString) => assignment.TrySetResult((islandId, connectionString)), cts.Token).Forget();
                await room.SendHeartbeatIfEnabledAsync(cts.Token);
                await room.ForceFreshIslandAssignmentAsync(cts.Token);
                response.TrySetResult(IslandAssignment(ISLAND_ID, CONNECTION_STRING));
                (string islandId, string connectionString) received = await assignment.Task.Timeout(TimeSpan.FromSeconds(4));
                cts.Cancel();

                // Assert
                Assert.AreEqual(ISLAND_ID, received.islandId);
                Assert.AreEqual(CONNECTION_STRING, received.connectionString);
                _ = connection.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
                _ = connection.Received(1).ConnectAsync(ADAPTER_URL, Arg.Any<CancellationToken>());
                AssertNoHeartbeatWasSent();
            });

        [TestCase("DuplicateIdentity")]
        [TestCase("ParticipantRemoved")]
        public void TreatTakeoverDisconnectsAsDuplicateIdentity(string disconnectReason)
        {
            // Arrange
            InitializeFeatureRegistry(NewFlags(heartbeatEnabled: true, stopOnDuplicateIdentity: true));
            var room = new ProbeConnectiveRoom();

            // Act
            InvokeConnectionUpdated(room, "Disconnected", disconnectReason);

            // Assert
            Assert.IsTrue(DuplicateIdentityDetected(room));
        }

        [UnityTest]
        public IEnumerator RejectLateIslandAfterAuthoritativeKickWithLegacyFlagDisabled() =>
            UniTask.ToCoroutine(async () =>
            {
                using var cache = new MemoryWeb3IdentityCache();
                var session = SessionControl.For(cache);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var packets = new Queue<ServerPacket>();
                packets.Enqueue(new ServerPacket { SessionStatus = new SessionStatusMessage { State = SessionStatus.TakeoverPending, RetryAfterMs = 1000 } });
                packets.Enqueue(new ServerPacket { Kicked = new KickedMessage { Reason = KickedReason.KrNewSession } });
                packets.Enqueue(new ServerPacket { IslandChanged = new IslandChangedMessage { IslandId = "late", ConnStr = CONNECTION_STRING } });
                connection.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(call => packets.Count > 0
                    ? UniTask.FromResult(Packet(packets.Dequeue())) : WaitForCancellationAsync(call.Arg<CancellationToken>()));
                var signFlow = new LiveConnectionArchipelagoSignFlow(connection, memoryPool, multiPool, session);
                var assignments = 0;
                InitializeFeatureRegistry(NewFlags(false));
                signFlow.StartListeningForConnectionStringAsync((_, _) => Interlocked.Increment(ref assignments), cts.Token).Forget();
                await UniTask.WaitUntil(() => session.Current == SessionControl.Status.Superseded, cancellationToken: cts.Token);
                Assert.AreEqual(0, assignments);
                var room = new ArchipelagoIslandRoom(signFlow, Substitute.For<ICharacterObject>(), Substitute.For<ICurrentAdapterAddress>(), session);
                Assert.IsFalse(await room.StartAsync());
                Assert.IsFalse(session.AcceptAssignment(session.Generation));
                cts.Cancel();
                room.Dispose();
            });

        [UnityTest]
        public IEnumerator ResolvePendingWithNormalAssignmentWithoutHeartbeat() =>
            UniTask.ToCoroutine(async () =>
            {
                using var cache = new MemoryWeb3IdentityCache();
                var session = SessionControl.For(cache);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var pending = new ServerPacket { SessionStatus = new SessionStatusMessage { State = SessionStatus.TakeoverPending, RetryAfterMs = 1000 } };
                var calls = 0;
                connection.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(call => Interlocked.Increment(ref calls) switch
                {
                    1 => UniTask.FromResult(Packet(pending)),
                    2 => UniTask.FromResult(IslandAssignment(ISLAND_ID, CONNECTION_STRING)),
                    _ => WaitForCancellationAsync(call.Arg<CancellationToken>()),
                });
                var signFlow = new LiveConnectionArchipelagoSignFlow(connection, memoryPool, multiPool, session);
                var assigned = new UniTaskCompletionSource<string>();
                signFlow.StartListeningForConnectionStringAsync((id, _) => assigned.TrySetResult(id), cts.Token).Forget();
                Assert.AreEqual(ISLAND_ID, await assigned.Task.AttachExternalCancellation(cts.Token));
                Assert.AreEqual(SessionControl.Status.Active, session.Current);
                AssertNoHeartbeatWasSent();
                cts.Cancel();
            });

        private EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> Packet(ServerPacket packet)
        {
            MemoryWrap memory = memoryPool.Memory(packet);
            packet.WriteTo(memory.Span());
            return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.SuccessResult(memory);
        }

        [UnityTest]
        public IEnumerator DistinguishBanAndUnknownFromSupersession() =>
            UniTask.ToCoroutine(async () =>
            {
                foreach (KickedReason reason in new[] { KickedReason.KrBanned, (KickedReason)99 })
                {
                    using var cache = new MemoryWeb3IdentityCache();
                    var session = SessionControl.For(cache);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    connection.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(Packet(new ServerPacket { Kicked = new KickedMessage { Reason = reason } })));
                    var flow = new LiveConnectionArchipelagoSignFlow(connection, memoryPool, multiPool, session);
                    flow.StartListeningForConnectionStringAsync((_, _) => Assert.Fail("Kick cannot assign an island"), cts.Token).Forget();
                    await UniTask.WaitUntil(() => session.Current != SessionControl.Status.Active, cancellationToken: cts.Token);
                    Assert.AreEqual(reason == KickedReason.KrBanned ? SessionControl.Status.Banned : SessionControl.Status.Unknown, session.Current);
                    Assert.IsFalse(session.CanRecover(session.Generation));
                    if (reason == KickedReason.KrBanned) Assert.IsFalse(session.BeginReauthentication());
                    cts.Cancel();
                }
            });

        [UnityTest]
        public IEnumerator IgnoreKickFromReplacedListenerGeneration() =>
            UniTask.ToCoroutine(async () =>
            {
                using var cache = new MemoryWeb3IdentityCache();
                var session = SessionControl.For(cache);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var oldResponse = new UniTaskCompletionSource<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>>();
                var received = new UniTaskCompletionSource<string>();
                var calls = 0;
                connection.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(call => Interlocked.Increment(ref calls) switch
                {
                    1 => oldResponse.Task,
                    2 => UniTask.FromResult(IslandAssignment(ISLAND_ID, CONNECTION_STRING)),
                    _ => WaitForCancellationAsync(call.Arg<CancellationToken>()),
                });
                var flow = new LiveConnectionArchipelagoSignFlow(connection, memoryPool, multiPool, session);
                flow.StartListeningForConnectionStringAsync((_, _) => Assert.Fail("Old listener callback"), cts.Token).Forget();
                await UniTask.WaitUntil(() => Volatile.Read(ref calls) == 1, cancellationToken: cts.Token);
                flow.StartListeningForConnectionStringAsync((id, _) => received.TrySetResult(id), cts.Token).Forget();
                Assert.AreEqual(ISLAND_ID, await received.Task.AttachExternalCancellation(cts.Token));
                oldResponse.TrySetResult(Packet(new ServerPacket { Kicked = new KickedMessage { Reason = KickedReason.KrNewSession } }));
                await UniTask.Delay(100, cancellationToken: cts.Token);
                Assert.AreEqual(SessionControl.Status.Active, session.Current);
                cts.Cancel();
            });

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator UpgradeLegacyPopupAndReconnectThroughActualButton() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                using var oldIdentity = new IWeb3Identity.Random();
                using var newIdentity = new IWeb3Identity.Random();
                using var cache = new MemoryWeb3IdentityCache();
                cache.Identity = oldIdentity;
                var session = SessionControl.For(cache);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/DCL/UI/DuplicateIdentityPopup/DuplicateIdentityWindow.prefab");
                var instance = UnityEngine.Object.Instantiate(prefab);
                var view = instance.GetComponent<UI.DuplicateIdentityPopup.DuplicateIdentityWindowView>();
                var invoked = false;
                var controller = new UI.DuplicateIdentityPopup.DuplicateIdentityWindowController(() => view, session,
                    token => session.ReauthenticateAsync(_ =>
                    {
                        invoked = true;
                        Assert.IsNull(cache.Identity);
                        cache.Identity = newIdentity;
                        return UniTask.CompletedTask;
                    }, token));
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    UniTask lifecycle = controller.LaunchViewLifeCycleAsync(new MVC.CanvasOrdering(MVC.CanvasOrdering.SortingLayer.Overlay, 0), default, cts.Token);
                    Assert.AreEqual("Session Ended", view.Title.text);

                    // Act
                    session.Stop(session.Generation, SessionControl.Status.Superseded);
                    await UniTask.Delay(300, cancellationToken: cts.Token);
                    Assert.AreEqual("Connected elsewhere", view.Title.text);
                    Assert.AreEqual("Reconnect here", view.ActionLabel.text);
                    view.ExitButton.onClick.Invoke();
                    await lifecycle;

                    // Assert
                    Assert.IsTrue(invoked);
                    Assert.AreSame(newIdentity, cache.Identity);
                    Assert.IsTrue(session.CanRecover(session.Generation));
                }
                finally
                {
                    cts.Cancel();
                    await controller.HideViewAsync(CancellationToken.None);
                    controller.Dispose();
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });
#endif

        [UnityTest]
        public IEnumerator RejectRoomSwapWhenSupersededWhilePreviousRoomDisconnects() =>
            UniTask.ToCoroutine(async () =>
            {
                using var cache = new MemoryWeb3IdentityCache();
                var session = SessionControl.For(cache);
                int generation = session.Generation;
                int revision = session.Revision;
                var previous = Substitute.For<IRoom>();
                var replacement = Substitute.For<IRoom>();
                var pool = Substitute.For<IObjectPool<IRoom>>();
                var disconnect = new UniTaskCompletionSource();
                previous.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(disconnect.Task);
                var interior = new InteriorRoom();
                UniTask swapping = interior.SwapRoomsAsync(RoomSelection.New, previous, replacement, pool, CancellationToken.None,
                    () => session.CanCompleteOperation(generation, revision));
                session.Stop(generation, SessionControl.Status.Superseded);
                disconnect.TrySetResult();
                await swapping;
                Assert.AreSame(NullRoom.INSTANCE.Info, interior.Info);
                pool.Received(1).Release(replacement);
                _ = replacement.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
            });

        [Test]
        public void IgnoreUnrelatedDisconnectsForDuplicateIdentityHandling()
        {
            // Arrange
            InitializeFeatureRegistry(NewFlags(heartbeatEnabled: true, stopOnDuplicateIdentity: true));
            var room = new ProbeConnectiveRoom();

            // Act
            InvokeConnectionUpdated(room, "Connected", "DuplicateIdentity");
            InvokeConnectionUpdated(room, "Disconnected", "ClientInitiated");

            // Assert
            Assert.IsFalse(DuplicateIdentityDetected(room));
        }

        private LiveConnectionArchipelagoSignFlow NewSignFlow() =>
            new (connection, memoryPool, multiPool);

        private ArchipelagoIslandRoom NewRoom(FeatureFlagsResultDto flags, IArchipelagoSignFlow signFlow, ICurrentAdapterAddress? adapterAddress = null)
        {
            InitializeFeatureRegistry(flags);

            ICharacterObject characterObject = Substitute.For<ICharacterObject>();
            characterObject.Position.Returns(PLAYER_POSITION);

            return new ArchipelagoIslandRoom(signFlow, characterObject, adapterAddress ?? Substitute.For<ICurrentAdapterAddress>());
        }

        private EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> IslandAssignment(string islandId, string connectionString)
        {
            var packet = new ServerPacket
            {
                IslandChanged = new IslandChangedMessage
                {
                    IslandId = islandId,
                    ConnStr = connectionString,
                },
            };

            MemoryWrap memory = memoryPool.Memory(packet);
            packet.WriteTo(memory.Span());
            return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.SuccessResult(memory);
        }

        private static async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> WaitForCancellationAsync(CancellationToken token)
        {
            await UniTask.WaitUntil(() => token.IsCancellationRequested);
            return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(
                IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "test completed");
        }

        private void AssertNoHeartbeatWasSent()
        {
            connection.DidNotReceive().SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>());
            CollectionAssert.IsEmpty(sentPackets);
        }

        private void AssertOneHeartbeatWasSent()
        {
            connection.Received(1).SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>());
            Assert.AreEqual(1, sentPackets.Count);
            Assert.AreEqual(ClientPacket.MessageOneofCase.Heartbeat, sentPackets[0].MessageCase);
        }

        private static FeatureFlagsResultDto NewFlags(bool heartbeatEnabled, bool stopOnDuplicateIdentity = false)
        {
            FeatureFlagsResultDto dto = FeatureFlagsResultDto.Empty;
            dto.flags[FeatureFlagsStrings.ARCHIPELAGO_HEARTBEATS] = heartbeatEnabled;
            dto.flags[FeatureFlagsStrings.STOP_ON_DUPLICATE_IDENTITY] = stopOnDuplicateIdentity;
            return dto;
        }

        private static void InitializeFeatureRegistry(FeatureFlagsResultDto flags)
        {
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(flags));
            FeaturesRegistry.Initialize(new FeaturesRegistry(Substitute.For<IAppArgs>(), localSceneDevelopment: false));
        }

        private static void InvokeConnectionUpdated(ConnectiveRoom room, string connectionUpdate, string disconnectReason)
        {
            MethodInfo method = typeof(ConnectiveRoom).GetMethod("OnConnectionUpdated", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ParameterInfo[] parameters = method.GetParameters();
            object update = Enum.Parse(parameters[1].ParameterType, connectionUpdate);
            Type reasonType = Nullable.GetUnderlyingType(parameters[2].ParameterType)!;
            object reason = Enum.Parse(reasonType, disconnectReason);

            method.Invoke(room, new[] { null, update, reason });
        }

        private static bool DuplicateIdentityDetected(ConnectiveRoom room)
        {
            FieldInfo field = typeof(ConnectiveRoom).GetField("isDuplicateIdentityDetected", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (bool)field.GetValue(room)!;
        }

        private sealed class ProbeConnectiveRoom : ConnectiveRoom
        {
            protected override UniTask PrewarmAsync(CancellationToken token) =>
                UniTask.CompletedTask;

            protected override UniTask CycleStepAsync(CancellationToken token) =>
                UniTask.CompletedTask;
        }
    }
}
