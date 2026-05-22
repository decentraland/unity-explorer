using DCL.Multiplayer.Connections.RoomHubs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.VoiceChat.Nearby;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Suppresses Nearby voice chat for the local player while they are banned from the current scene.
    ///     Listens to scene-room access signals from the gatekeeper:
    ///     a forbidden-access response engages suppression, any subsequent successful connect/disconnect of a new scene room releases it.
    /// </summary>
    public class NearbyVoiceBannedPlayerWatcher : IDisposable
    {
        private readonly IRoomHub roomHub;
        private readonly ISceneRestrictionBusController restrictionBus;
        private readonly NearbyVoiceChatStateModel stateModel;

        private bool localPlayerIsBanned;

        public NearbyVoiceBannedPlayerWatcher(IRoomHub roomHub, ISceneRestrictionBusController restrictionBus, NearbyVoiceChatStateModel stateModel)
        {
            this.roomHub = roomHub;
            this.restrictionBus = restrictionBus;
            this.stateModel = stateModel;

            roomHub.SceneRoom().CurrentSceneRoomForbiddenAccess += OnForbiddenAccess;
            roomHub.SceneRoom().CurrentSceneRoomConnected += OnSceneRoomReleased;
            roomHub.SceneRoom().CurrentSceneRoomDisconnected += OnSceneRoomReleased;
        }

        public void Dispose()
        {
            roomHub.SceneRoom().CurrentSceneRoomForbiddenAccess -= OnForbiddenAccess;
            roomHub.SceneRoom().CurrentSceneRoomConnected -= OnSceneRoomReleased;
            roomHub.SceneRoom().CurrentSceneRoomDisconnected -= OnSceneRoomReleased;

            if (localPlayerIsBanned)
                SetBanned(false);
        }

        private void OnForbiddenAccess() =>
            SetBanned(true);

        private void OnSceneRoomReleased() =>
            SetBanned(false);

        private void SetBanned(bool banned)
        {
            if (banned == localPlayerIsBanned)
                return;

            localPlayerIsBanned = banned;

            if (banned)
            {
                restrictionBus.PushSceneRestriction(SceneRestriction.CreateNearbyVoiceChatBlocked(SceneRestrictionsAction.APPLIED));
                stateModel.Suppress(SuppressionReason.SCENE_BAN);
            }
            else
            {
                restrictionBus.PushSceneRestriction(SceneRestriction.CreateNearbyVoiceChatBlocked(SceneRestrictionsAction.REMOVED));
                stateModel.Resume(SuppressionReason.SCENE_BAN);
            }
        }
    }
}
