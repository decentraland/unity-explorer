using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Pools;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        private readonly HashSet<RemoteSocialEmoteIntention> socialEmoteIntentions = new (PoolConstants.AVATARS_COUNT);

        public OwnedBunch<RemoteSocialEmoteIntention> SocialEmoteIntentions() =>
            new (emoteSync, socialEmoteIntentions);

        public void SaveForRetry(RemoteSocialEmoteIntention intention)
        {
            using (emoteSync.GetScope())
                socialEmoteIntentions.Add(intention);
        }

        // The Pulse protocol in use carries no social emote payload: nothing is sent until the protocol gains it
        public void SendSocialEmoteStart(URN urn, string targetWalletId, uint durationMs, NetworkMovementMessage playerState) =>
            ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Social emote start {urn} not sent: the Pulse protocol has no social emote payload");

        public void SendSocialEmoteOutcome(uint interactionId, int outcomeIndex, uint durationMs, NetworkMovementMessage playerState) =>
            ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Social emote outcome {outcomeIndex} of interaction {interactionId} not sent: the Pulse protocol has no social emote payload");
    }
}
