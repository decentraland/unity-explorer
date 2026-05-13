using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents <see cref="NearbyVoiceBannedPlayerWatcher"/>:
    /// when the gatekeeper returns a forbidden-access response for the current scene,
    /// the watcher suppresses Nearby voice chat with <see cref="SuppressionReason.SCENE_BAN"/>
    /// and pushes a NEARBY_VOICE_CHAT_BLOCKED:APPLIED restriction. Any subsequent successful
    /// scene-room connect or clean disconnect releases the suppression.
    /// </summary>
    public class NearbyVoiceBannedPlayerWatcherShould
    {
        private IRoomHub roomHub;
        private IGateKeeperSceneRoom sceneRoom;
        private ISceneRestrictionBusController restrictionBus;
        private NearbyVoiceChatStateModel stateModel;
        private NearbyVoiceBannedPlayerWatcher watcher;

        [SetUp]
        public void SetUp()
        {
            roomHub = Substitute.For<IRoomHub>();
            sceneRoom = Substitute.For<IGateKeeperSceneRoom>();
            roomHub.SceneRoom().Returns(sceneRoom);

            restrictionBus = Substitute.For<ISceneRestrictionBusController>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            watcher = new NearbyVoiceBannedPlayerWatcher(roomHub, restrictionBus, stateModel);
        }

        [TearDown]
        public void TearDown()
        {
            watcher.Dispose();
            stateModel.Dispose();
        }

        [Test]
        public void SuppressOnForbiddenAccess()
        {
            sceneRoom.CurrentSceneRoomForbiddenAccess += Raise.Event<Action>();

            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));
            Assert.That(stateModel.ActiveSuppression.Value, Is.EqualTo(SuppressionReason.SCENE_BAN));
            restrictionBus.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r =>
                r.Type == SceneRestrictions.NEARBY_VOICE_CHAT_BLOCKED && r.Action == SceneRestrictionsAction.APPLIED));
        }

        [Test]
        public void ResumeOnSceneRoomConnected()
        {
            sceneRoom.CurrentSceneRoomForbiddenAccess += Raise.Event<Action>();
            restrictionBus.ClearReceivedCalls();

            sceneRoom.CurrentSceneRoomConnected += Raise.Event<Action>();

            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
            Assert.That(stateModel.ActiveSuppression.Value, Is.Null);
            restrictionBus.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r =>
                r.Type == SceneRestrictions.NEARBY_VOICE_CHAT_BLOCKED && r.Action == SceneRestrictionsAction.REMOVED));
        }

        [Test]
        public void ResumeOnSceneRoomDisconnected()
        {
            sceneRoom.CurrentSceneRoomForbiddenAccess += Raise.Event<Action>();
            restrictionBus.ClearReceivedCalls();

            sceneRoom.CurrentSceneRoomDisconnected += Raise.Event<Action>();

            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
            restrictionBus.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r =>
                r.Action == SceneRestrictionsAction.REMOVED));
        }

        [Test]
        public void IgnoreDuplicateForbiddenWhileAlreadyBanned()
        {
            sceneRoom.CurrentSceneRoomForbiddenAccess += Raise.Event<Action>();
            restrictionBus.ClearReceivedCalls();

            sceneRoom.CurrentSceneRoomForbiddenAccess += Raise.Event<Action>();

            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.SUPPRESSED));
            restrictionBus.DidNotReceive().PushSceneRestriction(Arg.Any<SceneRestriction>());
        }

        [Test]
        public void IgnoreReleaseWhenNotBanned()
        {
            sceneRoom.CurrentSceneRoomConnected += Raise.Event<Action>();

            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
            restrictionBus.DidNotReceive().PushSceneRestriction(Arg.Any<SceneRestriction>());
        }

        [Test]
        public void ReleaseSuppressionOnDisposeWhileBanned()
        {
            sceneRoom.CurrentSceneRoomForbiddenAccess += Raise.Event<Action>();
            restrictionBus.ClearReceivedCalls();

            watcher.Dispose();
            watcher = null!;

            Assert.That(stateModel.State.Value, Is.EqualTo(NearbyVoiceChatState.IDLE));
            restrictionBus.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r =>
                r.Action == SceneRestrictionsAction.REMOVED));
        }
    }
}
