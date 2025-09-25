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
        public bool UseSocialEmoteOutcomeAnimation;
        public int SocialEmoteOutcomeIndex;
        public bool UseOutcomeReactionAnimation;

        public void UpdateRemoteId(URN emoteId)
        {
            this.EmoteId = emoteId;
            this.Spatial = true;
            this.TriggerSource = TriggerSource.REMOTE;
            this.UseSocialEmoteOutcomeAnimation = false;
            this.SocialEmoteOutcomeIndex = -1;
            this.UseOutcomeReactionAnimation = false;
        }
    }
}
