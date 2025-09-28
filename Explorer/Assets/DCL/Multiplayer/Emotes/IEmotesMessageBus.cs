using CommunicationData.URLHelpers;
using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Emotes
{
    public interface IEmotesMessageBus
    {
        OwnedBunch<RemoteEmoteIntention> EmoteIntentions();

        void Send(URN urn, bool loopCyclePassed, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress);

        void OnPlayerRemoved(string walletId);

        void SaveForRetry(RemoteEmoteIntention intention);
    }
}
