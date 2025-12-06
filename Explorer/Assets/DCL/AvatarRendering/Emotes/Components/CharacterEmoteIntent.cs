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
        public struct SocialEmoteData
        {
            public bool UseOutcomeAnimation;
            public int OutcomeIndex;
            public bool UseOutcomeReactionAnimation;
            public string InitiatorWalletAddress;
            public string TargetAvatarWalletAddress;
            public int InteractionId;
        }

        public string WalletAddress;
        public URN EmoteId;
        public bool Spatial;
        public TriggerSource TriggerSource;
        public bool IsRepeating;
        public SocialEmoteData SocialEmote;

        /// <summary>
        /// The emote that could be loaded and is ready to be played.
        /// </summary>
        public IEmote? EmoteAsset;

        /// <summary>
        /// Whether the emote was finally played.
        /// </summary>
        public bool HasPlayedEmote;

        public void UpdateRemoteId(URN emoteId)
        {
            this.WalletAddress = string.Empty;
            this.EmoteId = emoteId;
            this.Spatial = true;
            this.TriggerSource = TriggerSource.REMOTE;
            this.SocialEmote.UseOutcomeAnimation = false;
            this.SocialEmote.OutcomeIndex = -1;
            this.SocialEmote.UseOutcomeReactionAnimation = false;
            this.SocialEmote.InitiatorWalletAddress = string.Empty;
            this.SocialEmote.TargetAvatarWalletAddress = string.Empty;
            this.IsRepeating = false;
            this.SocialEmote.InteractionId = 0;
            this.EmoteAsset = null;
            this.HasPlayedEmote = false;
        }
    }
}
