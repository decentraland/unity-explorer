using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using ECS.StreamableLoading;

namespace DCL.AvatarRendering.Emotes
{
    public enum TriggerSource
    {
        PREVIEW,
        SELF,
        REMOTE,
        SCENE,
    }

    public struct CharacterEmoteIntent
    {
        public URN EmoteId;
        public bool Spatial;
        public TriggerSource TriggerSource;
        public AvatarEmoteMask Mask;

        private LoadTimeout? playTimeout;

        // TODO (Maurizio) what to do with mask here?

        public void UpdateRemoteId(URN emoteId)
        {
            this.EmoteId = emoteId;
            this.Spatial = true;
            this.TriggerSource = TriggerSource.REMOTE;
        }

        public bool UpdatePlayTimeout(float dt)
        {
            // Timeout access returns a temporary value. We need to reassign the field or we lose the changes
            playTimeout = new LoadTimeout(playTimeout?.Timeout ?? StreamableLoadingDefaults.TIMEOUT, playTimeout?.ElapsedTime ?? 0 + dt);
            bool result = playTimeout.Value.IsTimeout;
            return result;
        }
    }
}
