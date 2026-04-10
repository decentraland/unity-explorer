using CommunicationData.URLHelpers;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Emotes
{
    public interface IEmotesMessageBus
    {
        OwnedBunch<RemoteEmoteIntention> EmoteIntentions();

        void Send(URN urn, bool loopCyclePassed, uint durationMs = 0, NetworkMovementMessage? playerState = null);

        void SendStop();

        void OnPlayerRemoved(string walletId);

        void SaveForRetry(RemoteEmoteIntention intention);
    }
}
