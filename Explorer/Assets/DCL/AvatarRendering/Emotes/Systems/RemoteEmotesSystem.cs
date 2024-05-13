using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Web3.Identities;
using ECS.Abstract;
using System.Collections.Generic;
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
            HashSet<RemoteEmoteIntention> savedIntentions = HashSetPool<RemoteEmoteIntention>.Get();

            // this using cleans up the intention list
            using (OwnedBunch<RemoteEmoteIntention> emoteIntentions = emotesMessageBus.EmoteIntentions())
            {
                if (!emoteIntentions.Available())
                {
                    HashSetPool<RemoteEmoteIntention>.Release(savedIntentions);
                    return;
                }

                foreach (RemoteEmoteIntention remoteEmoteIntention in emoteIntentions.Collection())
                {
                    Entity entity = EntityOrNull(remoteEmoteIntention.WalletId);

                    // The entity was not created yet, so we wait until its created to be able to consume the intent
                    if (entity == Entity.Null)
                    {
                        savedIntentions.Add(remoteEmoteIntention);
                        continue;
                    }

                    ref CharacterEmoteIntent intention = ref World.AddOrGet<CharacterEmoteIntent>(entity);
                    intention.EmoteId = remoteEmoteIntention.EmoteId;
                    intention.Spatial = true;
                }
            }

            foreach (RemoteEmoteIntention savedIntention in savedIntentions)
                emotesMessageBus.SaveForRetry(savedIntention);

            HashSetPool<RemoteEmoteIntention>.Release(savedIntentions);
        }

        private Entity EntityOrNull(string walletId)
        {
            if (identityCache.Identity!.Address.Equals(walletId))
                return playerEntity;

            if (entityParticipantTable.Has(walletId))
                return entityParticipantTable.Entity(walletId);

            return Entity.Null;
        }
    }
}
