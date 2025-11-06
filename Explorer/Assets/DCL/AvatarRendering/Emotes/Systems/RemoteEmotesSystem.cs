using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using DCL.SocialEmotes;
using ECS.Abstract;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    public partial class RemoteEmotesSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IEmotesMessageBus emotesMessageBus;

        internal RemoteEmotesSystem(World world, IReadOnlyEntityParticipantTable entityParticipantTable, IEmotesMessageBus emotesMessageBus) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.emotesMessageBus = emotesMessageBus;
        }

        protected override void Update(float t)
        {
            using var scope = HashSetPool<RemoteEmoteIntention>.Get(out var savedIntentions);

            // this using cleans up the intention list
            using (OwnedBunch<RemoteEmoteIntention> emoteIntentions = emotesMessageBus.EmoteIntentions())
            {
                if (!emoteIntentions.Available())
                    return;

                foreach (RemoteEmoteIntention remoteEmoteIntention in emoteIntentions.Collection())
                {
                    SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(remoteEmoteIntention.WalletId);

                    // Corner case:
                    // While the start animation is looping, a new message is received and the intention is queued and consumed
                    // When the interaction among both avatars occurs, the local initiator plays the outcome animator, but the remote initiator was still looping, sending a message
                    // The message that arrives after the outcome animation played queued but was not consumed since there is a check below that prevents that from happening while there is another animation interpolation
                    // That message has to be discarded as it does not make sense anymore, the outcome animation is playing, the start animation is not going to play again
                    // If not removed, then it would play the start animation after the interaction finishes, which leads the system in an undesired state
     /*               if (!string.IsNullOrEmpty(remoteEmoteIntention.SocialEmoteInitiatorWalletAddress) &&
                        !remoteEmoteIntention.IsStopping &&
                        interaction != null &&
                        interaction.InitiatorWalletAddress == remoteEmoteIntention.WalletId &&
                        !remoteEmoteIntention.IsUsingSocialOutcomeAnimation &&
                        interaction.AreInteracting)
                    {
                        ReportHub.Log(ReportCategory.EMOTE_DEBUG, "PENDING savedIntention IGNORED " + remoteEmoteIntention.EmoteId);
                        continue;
                    }
*/
                    if (interaction != null &&
                        remoteEmoteIntention.EmoteId.Shorten() == interaction.Emote.DTO.Metadata.id &&
                        remoteEmoteIntention.IsRepeating)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>IGNORED LOOP " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + "</color>");
                        return;
                    }

                    if (//(interaction == null && remoteEmoteIntention.IsUsingSocialOutcomeAnimation) ||
                        !string.IsNullOrEmpty(remoteEmoteIntention.SocialEmoteInitiatorWalletAddress) && !SocialEmoteInteractionsManager.Instance.InteractionExists(remoteEmoteIntention.SocialEmoteInitiatorWalletAddress) && remoteEmoteIntention.IsUsingSocialOutcomeAnimation)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=green>DISCARDED " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation: " + remoteEmoteIntention.IsUsingSocialOutcomeAnimation + " Initiator: " + remoteEmoteIntention.SocialEmoteInitiatorWalletAddress + " interaction? " + (interaction != null) + "</color>");
                        continue;
                    }

                    // The entity was not created yet, so we wait until its created to be able to consume the intent
                    if (!entityParticipantTable.TryGet(remoteEmoteIntention.WalletId, out IReadOnlyEntityParticipantTable.Entry entry))
                    {
                        ReportHub.Log(ReportCategory.EMOTE_DEBUG, "savedIntentions +1   isOutcome? " + remoteEmoteIntention.IsUsingSocialOutcomeAnimation);
                        savedIntentions!.Add(remoteEmoteIntention);
                        continue;
                    }

                    ref RemotePlayerMovementComponent replicaMovement = ref World.TryGetRef<RemotePlayerMovementComponent>(entry.Entity, out bool _);
                    ref InterpolationComponent intComp = ref World.TryGetRef<InterpolationComponent>(entry.Entity, out bool interpolationExists);

                    if (remoteEmoteIntention.IsStopping)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=cyan>STOP SIGNAL " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + "</color>");
                        World.Add(entry.Entity, new StopEmoteIntent(remoteEmoteIntention.EmoteId));

                        //stopIntent.WalletAddress = remoteEmoteIntention.WalletId;
                        return;
                    }

                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "interpolation " + interpolationExists + " " + EmoteIsInPresentOrPast(replicaMovement, remoteEmoteIntention, intComp));

                    // If interpolation passed the time of emote, then we can play it (otherwise emote is still in the interpolation future)
                    if (interpolationExists && EmoteIsInPresentOrPast(replicaMovement, remoteEmoteIntention, intComp))
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "REMOTE PLAY: " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation " + remoteEmoteIntention.IsUsingSocialOutcomeAnimation + " repeating? " + remoteEmoteIntention.IsRepeating);

                        if(interaction != null)
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "REMOTE PLAY++: interaction emote " + interaction.Emote.DTO.Metadata.id + " intention shorten: " + remoteEmoteIntention.EmoteId.Shorten());

                        ref CharacterEmoteIntent intention = ref World!.AddOrGet<CharacterEmoteIntent>(entry.Entity);
                        intention.UpdateRemoteId(remoteEmoteIntention.EmoteId);
                        intention.WalletAddress = remoteEmoteIntention.WalletId;
                        intention.SocialEmoteOutcomeIndex = remoteEmoteIntention.SocialEmoteOutcomeIndex;
                        intention.UseOutcomeReactionAnimation = remoteEmoteIntention.IsReactingToSocialEmote;
                        intention.UseSocialEmoteOutcomeAnimation = remoteEmoteIntention.IsUsingSocialOutcomeAnimation;
                        intention.SocialEmoteInitiatorWalletAddress = remoteEmoteIntention.SocialEmoteInitiatorWalletAddress;
                        intention.IsRepeating = remoteEmoteIntention.IsRepeating;
                    }
                    else
                    {
                        ReportHub.Log(ReportCategory.EMOTE_DEBUG, "savedIntentions ++2 (" + emoteIntentions.Collection().Count + ")   isOutcome? " + remoteEmoteIntention.IsUsingSocialOutcomeAnimation);
                        savedIntentions.Add(remoteEmoteIntention);
                    }
                }
            }

            foreach (RemoteEmoteIntention savedIntention in savedIntentions!)
                emotesMessageBus.SaveForRetry(savedIntention);

            return;

            bool EmoteIsInPresentOrPast(RemotePlayerMovementComponent replicaMovement, RemoteEmoteIntention remoteEmoteIntention, InterpolationComponent intComp) =>
                intComp.Time + t >= remoteEmoteIntention.Timestamp || replicaMovement.PastMessage.timestamp >= remoteEmoteIntention.Timestamp;
        }
    }
}
