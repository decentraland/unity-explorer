using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Pools;
using DCL.Utility.Types;
using Decentraland.Kernel.Comms.V3;
using Global.AppArgs;
using LiveKit.Internal.FFIClients.Pools.Memory;
using NSubstitute;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Multiplayer.Connections.Archipelago.Tests
{
    /// <summary>
    ///     The <c>archipelago-heartbeats</c> kill switch has exactly one guarantee: with the flag served as
    ///     <c>false</c> no <c>Heartbeat</c> reaches the archipelago socket. These tests assert it where it is
    ///     observable — at <see cref="IArchipelagoLiveConnection" />, the socket abstraction — with the real
    ///     <see cref="LiveConnectionArchipelagoSignFlow" /> in between, so deleting the guard, hoisting its flag
    ///     read into a constructor or reinstating a second guard that reports success without sending all fail
    ///     here rather than only showing up as traffic on a service the platform is retiring.
    /// </summary>
    [TestFixture]
    public class ArchipelagoHeartbeatKillSwitchShould
    {
        private const float TOLERANCE = 0.0001f;

        private static readonly Vector3 PLAYER_POSITION = new (8f, 1.7f, 16f);

        private IArchipelagoLiveConnection connection = null!;
        private List<ClientPacket> sentPackets = null!;

        [SetUp]
        public void SetUp()
        {
            // Both are process-wide singletons: reset before seeding so the fixture is order-independent.
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();

            sentPackets = new List<ClientPacket>();
            connection = Substitute.For<IArchipelagoLiveConnection>();

            connection.SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>())
                      .Returns(call =>
                       {
                           // Parsed inside the call: the sign flow returns the pooled buffer as soon as it returns.
                           sentPackets.Add(ClientPacket.Parser.ParseFrom(call.Arg<MemoryWrap>().Span()));
                           return UniTask.FromResult(EnumResult<IArchipelagoLiveConnection.ResponseError>.SuccessResult());
                       });
        }

        [TearDown]
        public void TearDown()
        {
            FeaturesRegistry.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [UnityTest]
        public IEnumerator SendNothingAtAllWhenTheFlagIsServedAsFalse() =>
            UniTask.ToCoroutine(async () =>
            {
                ArchipelagoIslandRoom room = NewRoom(NewFlags(archipelagoHeartbeats: false));

                await room.SendHeartbeatIfEnabledAsync(CancellationToken.None);

                AssertNothingReachedTheSocket();
                CollectionAssert.IsEmpty(sentPackets);
            });

        [UnityTest]
        public IEnumerator KeepSendingHeartbeatsWhenTheFlagIsServedAsTrue() =>
            UniTask.ToCoroutine(async () =>
            {
                ArchipelagoIslandRoom room = NewRoom(NewFlags(archipelagoHeartbeats: true));

                await room.SendHeartbeatIfEnabledAsync(CancellationToken.None);

                AssertOneHeartbeatReachedTheSocketAt(PLAYER_POSITION);
            });

        /// <summary>
        ///     CONTEXT rule: a client that resolved no feature flags at all (the offline / flag-fetch-failed
        ///     path installs an empty configuration) must behave exactly as it does today.
        /// </summary>
        [UnityTest]
        public IEnumerator KeepSendingHeartbeatsWhenNoFlagWasResolved() =>
            UniTask.ToCoroutine(async () =>
            {
                ArchipelagoIslandRoom room = NewRoom(FeatureFlagsResultDto.Empty);

                await room.SendHeartbeatIfEnabledAsync(CancellationToken.None);

                AssertOneHeartbeatReachedTheSocketAt(PLAYER_POSITION);
            });

        private void AssertNothingReachedTheSocket() =>
            connection.DidNotReceive().SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>());

        private void AssertOneHeartbeatReachedTheSocketAt(Vector3 expectedPosition)
        {
            connection.Received(1).SendAsync(Arg.Any<MemoryWrap>(), Arg.Any<CancellationToken>());

            Assert.AreEqual(1, sentPackets.Count);
            Assert.AreEqual(ClientPacket.MessageOneofCase.Heartbeat, sentPackets[0].MessageCase);

            Decentraland.Common.Position position = sentPackets[0].Heartbeat.Position;
            Assert.AreEqual(expectedPosition.x, position.X, TOLERANCE);
            Assert.AreEqual(expectedPosition.y, position.Y, TOLERANCE);
            Assert.AreEqual(expectedPosition.z, position.Z, TOLERANCE);
        }

        /// <summary>
        ///     The room is built the way production builds it, over the real sign flow, so the assertion sits on
        ///     the transport and not on a stubbed sign flow that could hide a second guard.
        /// </summary>
        private ArchipelagoIslandRoom NewRoom(FeatureFlagsResultDto flags)
        {
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(flags));

            // ConnectiveRoom's constructor reads the registry, so it must exist before the room does.
            FeaturesRegistry.Initialize(new FeaturesRegistry(Substitute.For<IAppArgs>(), localSceneDevelopment: false));

            ICharacterObject characterObject = Substitute.For<ICharacterObject>();
            characterObject.Position.Returns(PLAYER_POSITION);

            var signFlow = new LiveConnectionArchipelagoSignFlow(connection, new ArrayMemoryPool(), new DCLMultiPool());

            return new ArchipelagoIslandRoom(signFlow, characterObject, Substitute.For<ICurrentAdapterAddress>());
        }

        private static FeatureFlagsResultDto NewFlags(bool archipelagoHeartbeats)
        {
            FeatureFlagsResultDto dto = FeatureFlagsResultDto.Empty;
            dto.flags[FeatureFlagsStrings.ARCHIPELAGO_HEARTBEATS] = archipelagoHeartbeats;
            return dto;
        }
    }
}
