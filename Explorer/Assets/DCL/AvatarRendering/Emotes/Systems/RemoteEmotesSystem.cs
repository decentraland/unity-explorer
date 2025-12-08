using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles;
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
            // Shares the list with the MultiplayerEmoteMessageBus using a mutex
            using var scope = HashSetPool<RemoteEmoteIntention>.Get(out var savedIntentions);

            // this using cleans up the intention list
            using (OwnedBunch<RemoteEmoteIntention> emoteIntentions = emotesMessageBus.EmoteIntentions())
            {
                if (emoteIntentions.Available())
                {
                    foreach (RemoteEmoteIntention remoteEmoteIntention in emoteIntentions.Collection())
                    {
                        // The entity was not created yet, so we wait until its created to be able to consume the intent
                        if (!entityParticipantTable.TryGet(remoteEmoteIntention.WalletId, out IReadOnlyEntityParticipantTable.Entry entry))
                        {
                            ReportHub.Log(ReportCategory.EMOTE_DEBUG, "savedIntentions +1   isOutcome? " + remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation);
                            savedIntentions!.Add(remoteEmoteIntention);
                            continue;
                        }

                        SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly? interaction = SocialEmoteInteractionsManager.Instance.GetInteractionState(remoteEmoteIntention.SocialEmote.InitiatorWalletAddress);

                        // Ignores reaction messages when the initiator started the same social emote again, while they were interacting (which cancels the emote for both)
                        if (remoteEmoteIntention.SocialEmote.IsReacting &&
                            interaction != null &&
                            interaction.Id != remoteEmoteIntention.SocialEmote.InteractionId)
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>IGNORED LOOP DIFFERENT ID " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " id1: " + interaction.Id + " id2: " + remoteEmoteIntention.SocialEmote.InteractionId + "</color>");
                            return;
                        }

                        // Ignores start interaction message when, for the same interaction, avatars are already using outcome animations
                        if (interaction != null &&
                            !remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation &&
                            interaction.AreInteracting &&
                            interaction.Id == remoteEmoteIntention.SocialEmote.InteractionId)
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>IGNORED LOOP " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + "</color>");
                            return;
                        }

                        // Ignores messages of reaction to social emote when the interaction does not exist yet (may occur when a third player connects while 2 avatars are already interacting)
                        if (!string.IsNullOrEmpty(remoteEmoteIntention.SocialEmote.InitiatorWalletAddress) &&
                            !SocialEmoteInteractionsManager.Instance.InteractionExists(remoteEmoteIntention.SocialEmote.InitiatorWalletAddress) &&
                            remoteEmoteIntention.SocialEmote.IsReacting)
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>DISCARDED " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation: " + remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation + " Initiator: " + remoteEmoteIntention.SocialEmote.InitiatorWalletAddress + " interaction? " + (interaction != null) + "</color>");
                            continue;
                        }

                        // Ignores repeated social emote messages for the same interaction
                        if (interaction != null &&
                            interaction.AreInteracting == remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation &&
                            interaction.Id == remoteEmoteIntention.SocialEmote.InteractionId)
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>IGNORED REPEATED " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + "</color>");
                            return;
                        }

                        // Ignores reaction messages to non-existing interactions, unless it's the first time they are processed (when a third player connects while other 2 are interacting)
                        if (interaction == null &&
                            remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation &&
                            SocialEmoteInteractionsManager.Instance.HasInteractionExisted(remoteEmoteIntention.SocialEmote.InteractionId))
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>DISCARDED " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation: " + remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation + " Initiator: " + remoteEmoteIntention.SocialEmote.InitiatorWalletAddress + " interaction? " + (interaction != null) + "</color>");
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
                        if (interpolationExists && (EmoteIsInPresentOrPast(replicaMovement, remoteEmoteIntention, intComp) || !intComp.Enabled))
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "REMOTE PLAY: " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + " IsUsingSocialOutcomeAnimation " + remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation + " IsReactingToSocialEmote " + remoteEmoteIntention.SocialEmote.IsReacting + " repeating? " + remoteEmoteIntention.IsRepeating + " id: " + remoteEmoteIntention.SocialEmote.InteractionId);

                            if(interaction != null)
                                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "REMOTE PLAY++: interaction emote " + interaction.Emote.DTO.Metadata.id + " intention shorten: " + remoteEmoteIntention.EmoteId.Shorten());

                            bool isInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop = false;

                            // This is a corner case that happens if 2 avatars are playing a looping social emote and a new avatar joins the scene. The third will receive the messages
                            // of both players when they iterate the animation loop, but those messages arrive in an unknown order. So if the initiator comes first, it has to wait for the receiver's
                            // and this flag is set until it arrives.
                            // If the message of the receiver comes first, it's ignored (the message is discarded in a previous condition). It will be handled when the initiator's is already there waiting.
                            // Once both have arrived, both emotes play at the same time (see SynchronizeRemoteInteractionBeforePlaying).
                            if (!remoteEmoteIntention.SocialEmote.IsReacting &&
                                remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation &&
                                interaction == null &&
                                !string.IsNullOrEmpty(remoteEmoteIntention.SocialEmote.InitiatorWalletAddress) &&
                                !SocialEmoteInteractionsManager.Instance.HasInteractionExisted(remoteEmoteIntention.SocialEmote.InteractionId))
                            {
                                ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=magenta>REBUILD INTERACTION! " + remoteEmoteIntention.EmoteId + " " + remoteEmoteIntention.WalletId + "</color>");
                                isInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop = true;
                            }

                            ref CharacterEmoteIntent intention = ref World!.AddOrGet<CharacterEmoteIntent>(entry.Entity);
                            intention.UpdateRemoteId(remoteEmoteIntention.EmoteId);
                            intention.WalletAddress = remoteEmoteIntention.WalletId;
                            intention.IsRepeating = remoteEmoteIntention.IsRepeating;
                            intention.SocialEmote.OutcomeIndex = remoteEmoteIntention.SocialEmote.OutcomeIndex;
                            intention.SocialEmote.UseOutcomeReactionAnimation = remoteEmoteIntention.SocialEmote.IsReacting;
                            intention.SocialEmote.UseOutcomeAnimation = remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation;
                            intention.SocialEmote.InitiatorWalletAddress = remoteEmoteIntention.SocialEmote.InitiatorWalletAddress;
                            intention.SocialEmote.TargetAvatarWalletAddress = remoteEmoteIntention.SocialEmote.TargetAvatarWalletAddress;
                            intention.SocialEmote.InteractionId = remoteEmoteIntention.SocialEmote.InteractionId;
                            intention.SocialEmote.IsInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop = isInitiatorOutcomeAnimationWaitingForReceiverAnimationLoop;
                        }
                        else
                        {
                            ReportHub.Log(ReportCategory.EMOTE_DEBUG, "savedIntentions ++2 (" + emoteIntentions.Collection().Count + ")   isOutcome? " + remoteEmoteIntention.SocialEmote.IsUsingOutcomeAnimation);
                            savedIntentions.Add(remoteEmoteIntention);
                        }
                    }
                }
            } // The list is cleared when the flow leaves the scope

            foreach (RemoteEmoteIntention savedIntention in savedIntentions!)
                emotesMessageBus.SaveForRetry(savedIntention);

            // TODO: Should the LookAtPositionIntention messages be handled in another system or multiplayer bus?

            // Look at position messages for remote players
            using var scopeLookAtPosition = HashSetPool<LookAtPositionIntention>.Get(out var savedLookAtPositionIntentions);

            using (OwnedBunch<LookAtPositionIntention> lookAtPositionIntentions = emotesMessageBus.LookAtPositionIntentions())
            {
                if (lookAtPositionIntentions.Available())
                {
                    foreach (LookAtPositionIntention lookAtPositionIntention in lookAtPositionIntentions.Collection())
                    {
                        if (entityParticipantTable.TryGet(lookAtPositionIntention.WalletAddress, out IReadOnlyEntityParticipantTable.Entry entry))
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Added look at (remote)." + lookAtPositionIntention.WalletAddress + " pos: " + lookAtPositionIntention.TargetPosition.ToString("F6"));
                            World.Add(entry.Entity, lookAtPositionIntention);
                        }
                        else
                        {
                            savedLookAtPositionIntentions.Add(lookAtPositionIntention);
                        }
                    }
                }
            }

            foreach (LookAtPositionIntention savedIntention in savedLookAtPositionIntentions!)
                emotesMessageBus.SaveForRetry(savedIntention);

            // Look at position messages for local player
            ConsumeLookAtPositionQuery(World);

            return;

            bool EmoteIsInPresentOrPast(RemotePlayerMovementComponent replicaMovement, RemoteEmoteIntention remoteEmoteIntention, InterpolationComponent intComp) =>
                intComp.Present + t >= remoteEmoteIntention.Timestamp || replicaMovement.PastMessage.timestamp >= remoteEmoteIntention.Timestamp;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void ConsumeLookAtPosition(Entity entity, Profile profile)
        {
            using var scopeLookAtPosition = HashSetPool<LookAtPositionIntention>.Get(out var savedLookAtPositionIntentions);

            using (OwnedBunch<LookAtPositionIntention> lookAtPositionIntentions = emotesMessageBus.LookAtPositionIntentions())
            {
                if (lookAtPositionIntentions.Available())
                {
                    foreach (LookAtPositionIntention lookAtPositionIntention in lookAtPositionIntentions.Collection())
                    {
                        if (profile.UserId == lookAtPositionIntention.WalletAddress)
                        {
                            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Added look at (local)." + lookAtPositionIntention.WalletAddress + " pos: " + lookAtPositionIntention.TargetPosition.ToString("F6"));
                            World.Add(entity, lookAtPositionIntention);
                        }
                        else
                        {
                            savedLookAtPositionIntentions.Add(lookAtPositionIntention);
                        }
                    }
                }
            }

            foreach (LookAtPositionIntention savedIntention in savedLookAtPositionIntentions!)
                emotesMessageBus.SaveForRetry(savedIntention);
        }
    }
}
