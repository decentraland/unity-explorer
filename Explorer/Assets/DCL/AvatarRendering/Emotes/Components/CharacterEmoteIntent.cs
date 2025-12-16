#if !ENABLE_SOCIAL_EMOTES

using CommunicationData.URLHelpers;

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

        public CharacterEmoteIntent(URN emoteId,
            TriggerSource triggerSource = default,
            bool spatial = false)
        {
            this.EmoteId = emoteId;
            this.Spatial = spatial;
            this.TriggerSource = triggerSource;
        }

        public void UpdateRemoteId(URN emoteId)
        {
            this.EmoteId = emoteId;
            this.Spatial = true;
            this.TriggerSource = TriggerSource.REMOTE;
        }
    }
}

#endif
