using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Emotes
{
    public interface IEmotesMessageBus
    {
        OwnedBunch<RemoteEmoteIntention> EmoteIntentions();
        OwnedBunch<RemoteEmoteStopIntention> EmoteStopIntentions();

        void Send(URN urn, bool loopCyclePassed, AvatarEmoteMask mask);

        void SendStop();

        void OnPlayerRemoved(string walletId);

        void SaveForRetry(RemoteEmoteIntention intention);
    }
}
