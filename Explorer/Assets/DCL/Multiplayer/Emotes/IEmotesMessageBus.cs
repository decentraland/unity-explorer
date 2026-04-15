using CommunicationData.URLHelpers;
using DCL.Multiplayer.Movement;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.Bunches;

namespace DCL.Multiplayer.Emotes
{
    public interface IEmotesMessageBus
    {
        OwnedBunch<RemoteEmoteIntention> EmoteIntentions();
        OwnedBunch<RemoteEmoteStopIntention> EmoteStopIntentions();

        void Send(URN urn, bool loopCyclePassed, AvatarEmoteMask mask, uint durationMs = 0, NetworkMovementMessage? playerState = null);

        void SendStop();

        void OnPlayerRemoved(string walletId);

        void SaveForRetry(RemoteEmoteIntention intention);
        void SaveForRetry(RemoteEmoteStopIntention intention);
    }
}
