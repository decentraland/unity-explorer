using JetBrains.Annotations;
using System;
using UnityEngine;

namespace SceneRuntime.Apis.Modules
{
    public class RestrictedActionsAPIWrapper : IDisposable
    {
        private readonly IRestrictedActionsAPI api;

        public RestrictedActionsAPIWrapper(IRestrictedActionsAPI api)
        {
            this.api = api;
        }

        public void Dispose()
        {
            api.Dispose();
        }

        [UsedImplicitly]
        public bool OpenExternalUrl(string url) =>
            api.OpenExternalUrl(url);

        [UsedImplicitly]
        public void MovePlayerTo(
            int newRelativePositionX, int newRelativePositionY, int newRelativePositionZ)
        {
            api.MovePlayerTo(
                new Vector3(newRelativePositionX, newRelativePositionY, newRelativePositionZ),
                null);
        }

        [UsedImplicitly]
        public void MovePlayerTo(
            int newRelativePositionX, int newRelativePositionY, int newRelativePositionZ,
            int cameraTargetX, int cameraTargetY, int cameraTargetZ)
        {
            api.MovePlayerTo(
                new Vector3(newRelativePositionX, newRelativePositionY, newRelativePositionZ),
                new Vector3(cameraTargetX, cameraTargetY, cameraTargetZ));
        }

        [UsedImplicitly]
        public void TeleportTo(int x, int y) =>
            api.TeleportTo(new Vector2Int(x, y));

        [UsedImplicitly]
        public bool ChangeRealm(string message, string realm) =>
            api.ChangeRealm(message, realm);

        [UsedImplicitly]
        public void TriggerEmote(string predefinedEmote) =>
            api.TriggerEmote(predefinedEmote);

        [UsedImplicitly]
        public bool TriggerSceneEmote(string src, bool loop) =>
            api.TriggerSceneEmote(src, loop);

        [UsedImplicitly]
        public bool OpenNftDialog(string urn) =>
            api.OpenNftDialog(urn);
    }
}
