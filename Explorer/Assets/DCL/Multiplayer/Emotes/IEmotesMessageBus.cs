using CommunicationData.URLHelpers;
using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Emotes
{
    public interface IEmotesMessageBus
    {
        OwnedBunch<RemoteEmoteIntention> EmoteIntentions();

        void Send(URN urn, bool loopCyclePassed, bool sendToSelfReplica);

        void OnPlayerRemoved(string walletId);

        void SaveForRetry(RemoteEmoteIntention intention);
    }
}
