using JetBrains.Annotations;
using System;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.RestrictedActionsApi
{
    public class RestrictedActionsAPIWrapper : IJsApiWrapper
    {
        private readonly IRestrictedActionsAPI api;

        public RestrictedActionsAPIWrapper(IRestrictedActionsAPI api)
        {
            this.api = api;
        }

        public void Dispose()
        {
        }

        [UsedImplicitly]
        public bool OpenExternalUrl(string url) =>
            api.TryOpenExternalUrl(url);

        [UsedImplicitly]
        public void MovePlayerTo(
            int newRelativePositionX, int newRelativePositionY, int newRelativePositionZ)
        {
            api.TryMovePlayerTo(
                new Vector3(newRelativePositionX, newRelativePositionY, newRelativePositionZ),
                null);
        }

        [UsedImplicitly]
        public void MovePlayerTo(
            int newRelativePositionX, int newRelativePositionY, int newRelativePositionZ,
            int cameraTargetX, int cameraTargetY, int cameraTargetZ)
        {
            api.TryMovePlayerTo(
                new Vector3(newRelativePositionX, newRelativePositionY, newRelativePositionZ),
                new Vector3(cameraTargetX, cameraTargetY, cameraTargetZ));
        }

        [UsedImplicitly]
        public void TeleportTo(int x, int y) =>
            api.TryTeleportTo(new Vector2Int(x, y));

        [UsedImplicitly]
        public bool ChangeRealm(string message, string realm) =>
            api.TryChangeRealm(message, realm);

        [UsedImplicitly]
        public void TriggerEmote(string predefinedEmote) =>
            api.TryTriggerEmote(predefinedEmote);

        [UsedImplicitly]
        public bool TriggerSceneEmote(string src, bool loop) =>
            api.TryTriggerSceneEmote(src, loop);

        [UsedImplicitly]
        public bool OpenNftDialog(string urn) =>
            api.TryOpenNftDialog(urn);
    }
}
