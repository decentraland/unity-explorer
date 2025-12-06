using CommunicationData.URLHelpers;
using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Emotes
{
    public interface IEmotesMessageBus
    {
        OwnedBunch<RemoteEmoteIntention> EmoteIntentions();
        OwnedBunch<LookAtPositionIntention> LookAtPositionIntentions();

        void Send(URN urn, bool loopCyclePassed, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress, bool isStopping, int interactionId);

        void OnPlayerRemoved(string walletId);

        void SaveForRetry(RemoteEmoteIntention intention);
        void SaveForRetry(LookAtPositionIntention intention);

        void SendLookAtPositionMessage(string walletAddress, float worldPositionX, float worldPositionY, float worldPositionZ);
    }
}
