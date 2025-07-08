﻿using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Web3.Identities;
using ECS.Abstract;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RemoteEmotesSystem : BaseUnityLoopSystem
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IEmotesMessageBus emotesMessageBus;
        private readonly Entity playerEntity;

        internal RemoteEmotesSystem(World world, IWeb3IdentityCache identityCache, IReadOnlyEntityParticipantTable entityParticipantTable, IEmotesMessageBus emotesMessageBus, Entity playerEntity) : base(world)
        {
            this.identityCache = identityCache;
            this.entityParticipantTable = entityParticipantTable;
            this.playerEntity = playerEntity;
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

                    ref CharacterEmoteIntent intention = ref World!.AddOrGet<CharacterEmoteIntent>(entry.Entity);
                    ref RemotePlayerMovementComponent replicaMovement = ref World.TryGetRef<RemotePlayerMovementComponent>(entry.Entity, out bool interpolationExists);

                    if (interpolationExists)
                    {
                        if (replicaMovement.PastMessage.timestamp >= remoteEmoteIntention.Timestamp)
                            intention.UpdateRemoteId(remoteEmoteIntention.EmoteId);
                        else
                            savedIntentions.Add(remoteEmoteIntention);
                    }
                    else
                        savedIntentions.Add(remoteEmoteIntention);
                }
            }

            foreach (RemoteEmoteIntention savedIntention in savedIntentions!)
                emotesMessageBus.SaveForRetry(savedIntention);
        }
    }
}
