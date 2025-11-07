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
                    SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(remoteEmoteIntention.SocialEmoteInitiatorWalletAddress);

                    // Ignores reaction messages when the initiator started the same social emote again, while they were interacting (which cancels the emote for both)
                    if (remoteEmoteIntention.IsReactingToSocialEmote &&
                        interaction != null &&
                        interaction.Id != remoteEmoteIntention.SocialEmoteInteractionId)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>IGNORED LOOP DIFFERENT ID " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " id1: " + interaction.Id + " id2: " + remoteEmoteIntention.SocialEmoteInteractionId + "</color>");
                        return;
                    }

                    // Ignores start interaction message when, for the same interaction, avatars are already using outcome animations
                    if (interaction != null &&
                        !remoteEmoteIntention.IsUsingSocialOutcomeAnimation &&
                        interaction.AreInteracting &&
                        interaction.Id == remoteEmoteIntention.SocialEmoteInteractionId)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>IGNORED LOOP " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + "</color>");
                        return;
                    }

                    // Ignores messages of reaction to social emote of the interaction for incoming message, which does not exist currently (may occur when a third player connects while 2 avatars are already interacting)
                    if (!string.IsNullOrEmpty(remoteEmoteIntention.SocialEmoteInitiatorWalletAddress) &&
                        !SocialEmoteInteractionsManager.Instance.InteractionExists(remoteEmoteIntention.SocialEmoteInitiatorWalletAddress) &&
                        remoteEmoteIntention.IsReactingToSocialEmote)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>DISCARDED " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation: " + remoteEmoteIntention.IsUsingSocialOutcomeAnimation + " Initiator: " + remoteEmoteIntention.SocialEmoteInitiatorWalletAddress + " interaction? " + (interaction != null) + "</color>");
                        continue;
                    }

                    // Ignores repeated social emote messages for the same interaction
                    if (interaction != null &&
                        interaction.AreInteracting == remoteEmoteIntention.IsUsingSocialOutcomeAnimation &&
                        interaction.Id == remoteEmoteIntention.SocialEmoteInteractionId)
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>IGNORED REPEATED " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + "</color>");
                        return;
                    }

                    // Ignores reaction messages to non-existing interactions, unless it's the first time they are processed (when a third player connects while other 2 are interacting)
                    if (interaction == null &&
                        remoteEmoteIntention.IsUsingSocialOutcomeAnimation &&
                        SocialEmoteInteractionsManager.Instance.HasInteractionExisted(remoteEmoteIntention.SocialEmoteInteractionId))
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>DISCARDED " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation: " + remoteEmoteIntention.IsUsingSocialOutcomeAnimation + " Initiator: " + remoteEmoteIntention.SocialEmoteInitiatorWalletAddress + " interaction? " + (interaction != null) + "</color>");
                        continue;
                    }

                    // TODO: Should this be moved to the top?
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
                        return;
                    }

                    ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "interpolation " + interpolationExists + " " + EmoteIsInPresentOrPast(replicaMovement, remoteEmoteIntention, intComp));

                    // If interpolation passed the time of emote, then we can play it (otherwise emote is still in the interpolation future)
                    if (interpolationExists && EmoteIsInPresentOrPast(replicaMovement, remoteEmoteIntention, intComp))
                    {
                        ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "REMOTE PLAY: " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation " + remoteEmoteIntention.IsUsingSocialOutcomeAnimation + " IsReactingToSocialEmote " + remoteEmoteIntention.IsReactingToSocialEmote + " repeating? " + remoteEmoteIntention.IsRepeating + " id: " + remoteEmoteIntention.SocialEmoteInteractionId);

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
                        intention.SocialEmoteInteractionId = remoteEmoteIntention.SocialEmoteInteractionId;
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
