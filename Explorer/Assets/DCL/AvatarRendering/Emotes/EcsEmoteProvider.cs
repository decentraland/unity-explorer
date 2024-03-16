using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using PromiseByPointers = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersFromRealmIntention>;
using OwnedEmotesPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetOwnedEmotesFromRealmIntention>;

namespace DCL.AvatarRendering.Emotes
{
    public class EcsEmoteProvider : IEmoteProvider
    {
        private readonly World world;
        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder = new ();

        public EcsEmoteProvider(World world,
            IRealmData realmData)
        {
            this.world = world;
            this.realmData = realmData;
        }

        public async UniTask<IReadOnlyList<IEmote>> GetOwnedEmotesAsync(Web3Address userId, CancellationToken ct, int? pageNum = null, int? pageSize = null, URN? collectionId = null)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(realmData.Ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"/users/{userId}/emotes"))
                      .AppendParameter(new URLParameter("includeEntities", "true"));

            if (pageNum != null)
                urlBuilder.AppendParameter(new URLParameter("pageNum", pageNum.ToString()));

            if (pageSize != null)
                urlBuilder.AppendParameter(new URLParameter("pageSize", pageSize.ToString()));

            if (collectionId != null)
                urlBuilder.AppendParameter(new URLParameter("collectionId", collectionId));

            URLAddress url = urlBuilder.Build();

            var intention = new GetOwnedEmotesFromRealmIntention(new CommonLoadingArguments(url));

            OwnedEmotesPromise promise = await OwnedEmotesPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY)
                                                                 .ToUniTaskAsync(world, cancellationToken: ct);

            if (!promise.Result.HasValue)
                return ArraySegment<IEmote>.Empty;

            if (!promise.Result.Value.Succeeded)
                throw promise.Result.Value.Exception;

            return promise.Result.Value.Asset.Emotes;
        }

        public async UniTask<IReadOnlyList<IEmote>> GetEmotesAsync(IEnumerable<URN> emoteIds, CancellationToken ct)
        {
            List<URN> pointers = ListPool<URN>.Get();

            try
            {
                pointers.AddRange(emoteIds);

                var intention = new GetEmotesByPointersFromRealmIntention(pointers,
                    new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint));

                var promise = PromiseByPointers.Create(world, intention, PartitionComponent.TOP_PRIORITY);
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
