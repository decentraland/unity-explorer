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
    }
}
