using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;

namespace DCL.AvatarRendering.Emotes
{
    public class EcsEmoteProvider : IEmoteProvider
    {
        private readonly World world;
        private readonly IRealmData realmData;

        public EcsEmoteProvider(World world,
            IRealmData realmData)
        {
            this.world = world;
            this.realmData = realmData;
        }

        public UniTask<IReadOnlyList<IEmote>> GetOwnedEmotesAsync(string userId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<IReadOnlyList<IEmote>> GetEmotesAsync(IEnumerable<URN> emoteIds, CancellationToken ct)
        {
            List<URN> pointers = ListPool<URN>.Get();

            try
            {
                pointers.AddRange(emoteIds);

                var intention = new GetEmotesByPointersFromRealmIntention(pointers,
                    new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint));
                var promise = Promise.Create(world, intention, new PartitionComponent());
                promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

                if (!promise.Result.HasValue)
                    return Array.Empty<IEmote>();

                if (!promise.Result.Value.Succeeded)
                    throw promise.Result.Value.Exception;

                return promise.Result.Value.Asset.Emotes;
            }
            finally { ListPool<URN>.Release(pointers); }
        }
    }
}
