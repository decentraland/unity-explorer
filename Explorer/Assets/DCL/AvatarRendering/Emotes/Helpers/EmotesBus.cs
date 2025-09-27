using System;

namespace DCL.AvatarRendering.Emotes
{
	public class EmotesBus
	{
        public delegate void SocialEmoteReactionPlayingRequestedDelegate(string initiatorWalletAddress, IEmote emote, int outcomeIndex);

		public event Action QuickActionEmotePlayed;
        public event SocialEmoteReactionPlayingRequestedDelegate SocialEmoteReactionPlayingRequested;

		public void OnQuickActionEmotePlayed()
		{
			QuickActionEmotePlayed?.Invoke();
		}

        public void PlaySocialEmoteReaction(string initiatorWalletAddress, IEmote emote, int outcomeIndex)
        {
            SocialEmoteReactionPlayingRequested?.Invoke(initiatorWalletAddress, emote, outcomeIndex);
        }
	}
}
