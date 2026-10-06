using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Utility.Types;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace DCL.Multiplayer.HealthChecks.Tests
{
    /// <summary>
    ///     The rooms are started together and reported as a single failure, so this check's message is the only
    ///     place that can say which room did not come up - the load flow, the analytics event and the error popup
    ///     all read it. The real hub is wired in rather than substituted because the per-room naming lives there,
    ///     and a substituted hub would only echo the expectation back.
    /// </summary>
    public class StartLiveKitRoomsShould
    {
        private const string NOT_CONNECTED_SCENE_ROOM = "Scene[Starting, Error, CycleFailed]";

        [Test]
        public async Task NameTheRoomThatDidNotStart()
        {
            // Arrange
            IGateKeeperSceneRoom sceneRoom = NotConnectedSceneRoom();
            sceneRoom.StartAsync().Returns(UniTask.FromResult(false));

            // Act
            Result result = await new StartLiveKitRooms(HubWith(sceneRoom)).IsRemoteAvailableAsync(CancellationToken.None);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.That(result.ErrorMessage, Does.Contain(NOT_CONNECTED_SCENE_ROOM));

            // The rooms that did come up are named as well, otherwise the failing one cannot be told apart
            Assert.That(result.ErrorMessage, Does.Contain("Island[Running, Success, Running]"));
            Assert.That(result.ErrorMessage, Does.Contain("Chat[Running, Success, Running]"));
        }

        [Test]
        public async Task NameTheRoomThatThrewWhileStarting()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            IGateKeeperSceneRoom sceneRoom = NotConnectedSceneRoom();
            sceneRoom.StartAsync().Throws(new InvalidOperationException("Room is already running"));

            // Act
            Result result = await new StartLiveKitRooms(HubWith(sceneRoom)).IsRemoteAvailableAsync(CancellationToken.None);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.That(result.ErrorMessage, Does.Contain(NOT_CONNECTED_SCENE_ROOM));
        }

        private static IRoomHub HubWith(IGateKeeperSceneRoom sceneRoom) =>
            new RoomHub(ConnectedRoom(), sceneRoom, ConnectedRoom(), new VoiceChatActivatableConnectiveRoom());

        private static IGateKeeperSceneRoom NotConnectedSceneRoom()
        {
            var room = Substitute.For<IGateKeeperSceneRoom>();

            // Only a stopped room is asked to start, and by the time the failure is reported the room is starting
            room.CurrentState().Returns(IConnectiveRoom.State.Stopped, IConnectiveRoom.State.Starting);
            room.AttemptToConnectState.Returns(AttemptToConnectState.Error);
            room.CurrentConnectionLoopHealth.Returns(IConnectiveRoom.ConnectionLoopHealth.CycleFailed);
            return room;
        }

        private static IConnectiveRoom ConnectedRoom()
        {
            var room = Substitute.For<IConnectiveRoom>();
            room.CurrentState().Returns(IConnectiveRoom.State.Running);
            room.AttemptToConnectState.Returns(AttemptToConnectState.Success);
            room.CurrentConnectionLoopHealth.Returns(IConnectiveRoom.ConnectionLoopHealth.Running);
            return room;
        }
    }
}
