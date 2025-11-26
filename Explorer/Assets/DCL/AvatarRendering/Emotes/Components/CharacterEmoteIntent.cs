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
        public string WalletAddress;
        public URN EmoteId;
        public bool Spatial;
        public TriggerSource TriggerSource;
        // TODO: Put all the fields of the social emote into an inner class SocialEmote
        public bool UseSocialEmoteOutcomeAnimation;
        public int SocialEmoteOutcomeIndex;
        public bool UseOutcomeReactionAnimation;
        public string SocialEmoteInitiatorWalletAddress;
        public string TargetAvatarWalletAddress;
        public bool IsRepeating;
        public int SocialEmoteInteractionId;

        /// <summary>
        ///
        /// </summary>
        public IEmote? EmoteAsset;

        public void UpdateRemoteId(URN emoteId)
        {
            this.WalletAddress = string.Empty;
            this.EmoteId = emoteId;
            this.Spatial = true;
            this.TriggerSource = TriggerSource.REMOTE;
            this.UseSocialEmoteOutcomeAnimation = false;
            this.SocialEmoteOutcomeIndex = -1;
            this.UseOutcomeReactionAnimation = false;
            this.SocialEmoteInitiatorWalletAddress = string.Empty;
            this.TargetAvatarWalletAddress = string.Empty;
            this.IsRepeating = false;
            this.SocialEmoteInteractionId = 0;
            this.EmoteAsset = null;
        }
    }
}
