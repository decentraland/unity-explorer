using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine.Pool;
using Utility.Arch;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Sole owner of <see cref="SocialEmoteInteractionComponent" />: applies the server-ordered social emote events to
    ///     the participants, pairs a receiver with its initiator, and ends interactions on timeout or participant removal.
    ///     Events that arrive before their prerequisites (participant entity, initiator start) are kept for retry until the
    ///     remote timeline passes their timestamp.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RemoteEmotesSystem))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class SocialEmoteInteractionSystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     Seconds an initiator loops the start animation before the interaction is dropped.
        /// </summary>
        internal const float REACTION_TIMEOUT = 3f;

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IEmotesMessageBus emotesMessageBus;

        internal SocialEmoteInteractionSystem(World world, IReadOnlyEntityParticipantTable entityParticipantTable, IEmotesMessageBus emotesMessageBus) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.emotesMessageBus = emotesMessageBus;
        }

        protected override void Update(float t)
        {
            ProcessRemoteIntentions();
            TickPhaseQuery(World, t);
            FinishOnDeletionQuery(World);
            RemoveFinishedQuery(World);
        }

        private void ProcessRemoteIntentions()
        {
            using var scope = HashSetPool<RemoteSocialEmoteIntention>.Get(out var savedIntentions);

            using (OwnedBunch<RemoteSocialEmoteIntention> intentions = emotesMessageBus.SocialEmoteIntentions())
            {
                if (!intentions.Available())
                    return;

                foreach (RemoteSocialEmoteIntention intention in intentions.Collection())
                {
                    if (!TryApply(in intention))
                        savedIntentions.Add(intention);
                }
            }

            foreach (RemoteSocialEmoteIntention savedIntention in savedIntentions)
                emotesMessageBus.SaveForRetry(savedIntention);
        }

        /// <returns>False when the event must be retried on a later update.</returns>
        private bool TryApply(in RemoteSocialEmoteIntention intention)
        {
            // The entity was not created yet, so we wait until it is created to be able to consume the event
            if (!entityParticipantTable.TryGet(intention.WalletId, out IReadOnlyEntityParticipantTable.Entry entry))
                return false;

            Entity entity = entry.Entity;

            if (World.Has<DeleteEntityIntention>(entity))
                return true;

            if (intention.IsStart)
            {
                ApplyStart(entity, in intention);
                return true;
            }

            return intention.IsFromInitiator ? TryApplyInitiatorOutcome(entity, in intention) : TryApplyReceiverOutcome(entity, in intention);
        }

        private void ApplyStart(Entity entity, in RemoteSocialEmoteIntention intention)
        {
            ref SocialEmoteInteractionComponent current = ref World.TryGetRef<SocialEmoteInteractionComponent>(entity, out bool exists);

            // Re-delivery of the same start changes nothing
            if (exists && current.InteractionId == intention.InteractionId)
                return;

            // A new start replaces whatever interaction the initiator was in
            if (exists && current.PartnerWalletId.Length > 0)
                FinishPartner(current.PartnerWalletId, current.InteractionId);

            World.AddOrSet(entity, new SocialEmoteInteractionComponent
            {
                InteractionId = intention.InteractionId,
                EmoteId = intention.EmoteId,
                Role = SocialEmoteRole.Initiator,
                Phase = SocialEmotePhase.Started,
                PartnerWalletId = string.Empty,
                TargetWalletId = intention.TargetWalletId,
                OutcomeIndex = RemoteSocialEmoteIntention.NO_OUTCOME,
            });
        }

        private bool TryApplyInitiatorOutcome(Entity entity, in RemoteSocialEmoteIntention intention)
        {
            ref SocialEmoteInteractionComponent interaction = ref World.TryGetRef<SocialEmoteInteractionComponent>(entity, out bool exists);

            // The start of this interaction was not applied yet
            if (!exists || interaction.InteractionId != intention.InteractionId)
                return IsStale(entity, intention.Timestamp);

            if (interaction.Phase != SocialEmotePhase.Started)
                return true;

            interaction.Phase = SocialEmotePhase.Outcome;
            interaction.OutcomeIndex = intention.OutcomeIndex;
            interaction.ElapsedInPhase = 0f;
            return true;
        }

        private bool TryApplyReceiverOutcome(Entity entity, in RemoteSocialEmoteIntention intention)
        {
            if (!entityParticipantTable.TryGet(intention.InitiatorWalletId, out IReadOnlyEntityParticipantTable.Entry initiatorEntry))
                return IsStale(entity, intention.Timestamp);

            ref SocialEmoteInteractionComponent initiator = ref World.TryGetRef<SocialEmoteInteractionComponent>(initiatorEntry.Entity, out bool initiatorExists);

            // The start of this interaction was not applied yet
            if (!initiatorExists || initiator.InteractionId != intention.InteractionId)
                return IsStale(entity, intention.Timestamp);

            if (initiator.Phase == SocialEmotePhase.Finished)
                return true;

            // The first reaction wins; later ones are dropped
            if (initiator.PartnerWalletId.Length > 0 && initiator.PartnerWalletId != intention.WalletId)
                return true;

            initiator.PartnerWalletId = intention.WalletId;

            // Structural change only after the last write through the initiator reference
            World.AddOrSet(entity, new SocialEmoteInteractionComponent
            {
                InteractionId = intention.InteractionId,
                EmoteId = intention.EmoteId,
                Role = SocialEmoteRole.Receiver,
                Phase = SocialEmotePhase.Outcome,
                PartnerWalletId = intention.InitiatorWalletId,
                TargetWalletId = string.Empty,
                OutcomeIndex = intention.OutcomeIndex,
            });

            return true;
        }

        /// <summary>
        ///     An event whose prerequisites have not arrived by the time the remote timeline is a reaction timeout past
        ///     it will never be applicable, so it is dropped instead of retried forever.
        /// </summary>
        private bool IsStale(Entity entity, double timestamp)
        {
            ref readonly InterpolationComponent interpolation = ref World.TryGetRef<InterpolationComponent>(entity, out bool exists);

            // Without a remote timeline (the local player) the prerequisites cannot arrive later
            return !exists || interpolation.Present > timestamp + REACTION_TIMEOUT;
        }

        private void FinishPartner(string partnerWalletId, uint interactionId)
        {
            if (!entityParticipantTable.TryGet(partnerWalletId, out IReadOnlyEntityParticipantTable.Entry entry))
                return;

            ref SocialEmoteInteractionComponent partner = ref World.TryGetRef<SocialEmoteInteractionComponent>(entry.Entity, out bool exists);

            if (exists && partner.InteractionId == interactionId)
                partner.Phase = SocialEmotePhase.Finished;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void TickPhase([Data] float dt, ref SocialEmoteInteractionComponent interaction)
        {
            interaction.ElapsedInPhase += dt;

            if (interaction.Phase == SocialEmotePhase.Started && interaction.ElapsedInPhase >= REACTION_TIMEOUT)
                interaction.Phase = SocialEmotePhase.Finished;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void FinishOnDeletion(ref SocialEmoteInteractionComponent interaction)
        {
            interaction.Phase = SocialEmotePhase.Finished;
        }

        [Query]
        private void RemoveFinished(Entity entity, in SocialEmoteInteractionComponent interaction)
        {
            if (interaction.Phase != SocialEmotePhase.Finished)
                return;

            if (interaction.PartnerWalletId.Length > 0)
                FinishPartner(interaction.PartnerWalletId, interaction.InteractionId);

            World.Remove<SocialEmoteInteractionComponent>(entity);
        }
    }
}
