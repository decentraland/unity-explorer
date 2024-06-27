using JetBrains.Annotations;
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

        public void Dispose() { }

        [UsedImplicitly]
        public bool OpenExternalUrl(string url) =>
            api.TryOpenExternalUrl(url);

        [UsedImplicitly]
        public void MovePlayerTo(
            double newRelativePositionX, double newRelativePositionY, double newRelativePositionZ)
        {
            api.TryMovePlayerTo(
                new Vector3((float)newRelativePositionX, (float)newRelativePositionY, (float)newRelativePositionZ),
                null);
        }

        [UsedImplicitly]
        public void MovePlayerTo(
            double newRelativePositionX, double newRelativePositionY, double newRelativePositionZ,
            double cameraTargetX, double cameraTargetY, double cameraTargetZ)
        {
            api.TryMovePlayerTo(
                new Vector3((float)newRelativePositionX, (float)newRelativePositionY, (float)newRelativePositionZ),
                new Vector3((float)cameraTargetX, (float)cameraTargetY, (float)cameraTargetZ));
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
