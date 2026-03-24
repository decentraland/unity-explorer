using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

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
            ProcessRemoteIntentions(t);
            ProcessRemoteStopIntentions(t);
        }

        private void ProcessRemoteIntentions(float t)
        {
            using var scope = HashSetPool<RemoteEmoteIntention>.Get(out var savedIntentions);

            // this using cleans up the intention list
            using (OwnedBunch<RemoteEmoteIntention> emoteIntentions = emotesMessageBus.EmoteIntentions())
            {
                if (!emoteIntentions.Available())
                    return;

                foreach (RemoteEmoteIntention remoteEmoteIntention in emoteIntentions.Collection())
                {
                    // The entity was not created yet, so we wait until its created to be able to consume the intent
                    if (!entityParticipantTable.TryGet(remoteEmoteIntention.WalletId, out IReadOnlyEntityParticipantTable.Entry entry))
                    {
                        savedIntentions!.Add(remoteEmoteIntention);
                        continue;
                    }

                    ref RemotePlayerMovementComponent replicaMovement = ref World.TryGetRef<RemotePlayerMovementComponent>(entry.Entity, out bool _);
                    ref InterpolationComponent intComp = ref World.TryGetRef<InterpolationComponent>(entry.Entity, out bool interpolationExists);

                    // If interpolation passed the time of emote, then we can play it (otherwise emote is still in the interpolation future)
                    if (interpolationExists && EmoteIsInPresentOrPast(replicaMovement, remoteEmoteIntention, intComp))
                    {
                        ref CharacterEmoteIntent intention = ref World!.AddOrGet<CharacterEmoteIntent>(entry.Entity);
                        intention.UpdateRemoteId(remoteEmoteIntention.EmoteId);
                        intention.Mask = remoteEmoteIntention.Mask;
                    }
                    else
                        savedIntentions.Add(remoteEmoteIntention);
                }
            }

            foreach (RemoteEmoteIntention savedIntention in savedIntentions!)
                emotesMessageBus.SaveForRetry(savedIntention);

            return;

            bool EmoteIsInPresentOrPast(RemotePlayerMovementComponent replicaMovement, RemoteEmoteIntention remoteEmoteIntention, InterpolationComponent intComp) =>
                intComp.Time + t >= remoteEmoteIntention.Timestamp || replicaMovement.PastMessage.timestamp >= remoteEmoteIntention.Timestamp;
        }

        private void ProcessRemoteStopIntentions(float t)
        {
            using var scope = HashSetPool<RemoteEmoteStopIntention>.Get(out var savedStopIntentions);

            // Use a scoped using block so Dispose (which clears the set) runs BEFORE SaveForRetry
            // re-adds items. A declaration-level using would clear the set at method exit, wiping retries.
            using (OwnedBunch<RemoteEmoteStopIntention> stopIntentions = emotesMessageBus.EmoteStopIntentions())
            {
                if (!stopIntentions.Available())
                    return;

                foreach (RemoteEmoteStopIntention stopIntention in stopIntentions.Collection())
                {
                    // The entity was not created yet, so we wait until it is created to be able to consume the intent
                    if (!entityParticipantTable.TryGet(stopIntention.WalletId, out IReadOnlyEntityParticipantTable.Entry entry))
                    {
                        savedStopIntentions!.Add(stopIntention);
                        continue;
                    }

                    // If the entity has an emote intent not already consumed we remove it straight away
                    if (World.Has<CharacterEmoteIntent>(entry.Entity))
                    {
                        World.Remove<CharacterEmoteIntent>(entry.Entity);
                        continue;
                    }

                    // Check if any emote (full-body or masked) is playing on the remote entity and stop it
                    bool isPlayingAny = false;

                    if (World.TryGet(entry.Entity, out CharacterEmoteComponent emoteComponent) && (emoteComponent.IsPlayingEmote || emoteComponent.CurrentEmoteReference != null))
                    {
                        emoteComponent.StopEmote = true;
                        World.Set(entry.Entity, emoteComponent);
                        isPlayingAny = true;
                    }

                    if (World.TryGet(entry.Entity, out CharacterMaskedEmoteComponent masked) && (masked.IsPlaying || masked.CurrentEmoteReference != null))
                    {
                        masked.StopEmote = true;
                        World.Set(entry.Entity, masked);
                        isPlayingAny = true;
                    }

                    if (isPlayingAny)
                        continue;

                    // Entity exists but the play intention hasn't been applied yet (still queued
                    // waiting for interpolation to catch up). Save the stop for retry so it can
                    // cancel the emote once the play is eventually consumed.
                    // However, if interpolation has already passed the stop's timestamp, the
                    // corresponding play was either never received or already consumed — discard.
                    ref InterpolationComponent intComp = ref World.TryGetRef<InterpolationComponent>(entry.Entity, out bool interpolationExists);
                    ref RemotePlayerMovementComponent replicaMovement = ref World.TryGetRef<RemotePlayerMovementComponent>(entry.Entity, out bool _);

                    if (!interpolationExists || StopIsInPresentOrPast(replicaMovement, stopIntention, intComp))
                        continue;

                    savedStopIntentions!.Add(stopIntention);
                }
            }

            foreach (RemoteEmoteStopIntention savedStopIntention in savedStopIntentions!)
                emotesMessageBus.SaveForRetry(savedStopIntention);

            return;

            bool StopIsInPresentOrPast(RemotePlayerMovementComponent replicaMovement, RemoteEmoteStopIntention stopIntention, InterpolationComponent intComp) =>
                intComp.Time + t >= stopIntention.Timestamp || replicaMovement.PastMessage.timestamp >= stopIntention.Timestamp;
        }
    }
}
