using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Web3.Identities;
using ECS.Abstract;

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
            using OwnedBunch<RemoteEmoteIntention> emoteIntentions = emotesMessageBus.EmoteIntentions();

            if (!emoteIntentions.Available()) return;

            foreach (RemoteEmoteIntention remoteEmoteIntention in emoteIntentions.Collection())
            {
                var entity = EntityOrNull(remoteEmoteIntention.WalletId);

                if (entity == Entity.Null)
                {
                    ReportHub.LogWarning(ReportCategory.EMOTE, $"Cannot find entity for walletId: {remoteEmoteIntention.WalletId}");
                    continue;
                }

                ref var intention = ref World.AddOrGet<CharacterEmoteIntent>(entity);
                intention.EmoteId = remoteEmoteIntention.EmoteId;
                intention.Spatial = true;
            }
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
