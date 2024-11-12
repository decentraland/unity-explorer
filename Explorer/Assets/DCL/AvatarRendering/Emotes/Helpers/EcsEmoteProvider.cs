using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Web3;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Threading;
using PromiseByPointers = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
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

        public async UniTask<int> GetOwnedEmotesAsync(
            Web3Address userId,
            CancellationToken ct,
            IEmoteProvider.OwnedEmotesRequestOptions requestOptions,
            List<IEmote> output
        )
        {
            output.Clear();

            urlBuilder.Clear();

            urlBuilder.AppendDomain(realmData.Ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"/users/{userId}/emotes"))
                      .AppendParameter(new URLParameter("includeEntities", "true"));

            int? pageNum = requestOptions.pageNum;
            int? pageSize = requestOptions.pageSize;
            URN? collectionId = requestOptions.collectionId;
            IEmoteProvider.OrderOperation? orderOperation = requestOptions.orderOperation;
            string? name = requestOptions.name;

            if (pageNum != null)
                urlBuilder.AppendParameter(new URLParameter("pageNum", pageNum.ToString()));

            if (pageSize != null)
                urlBuilder.AppendParameter(new URLParameter("pageSize", pageSize.ToString()));

            if (collectionId != null)
                urlBuilder.AppendParameter(new URLParameter("collectionId", collectionId));

            if (orderOperation.HasValue)
            {
                urlBuilder.AppendParameter(new URLParameter("orderBy", orderOperation.Value.By));
                urlBuilder.AppendParameter(new URLParameter("direction", orderOperation.Value.IsAscendent ? "asc" : "desc"));
            }

            if (name != null)
                urlBuilder.AppendParameter(new URLParameter("name", name));

            URLAddress url = urlBuilder.Build();

            var intention = new GetOwnedEmotesFromRealmIntention(new CommonLoadingArguments(url));

            OwnedEmotesPromise promise = await OwnedEmotesPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY)
                                                                 .ToUniTaskAsync(world, cancellationToken: ct);

            if (!promise.Result.HasValue)
                return 0;

            if (!promise.Result.Value.Succeeded)
                throw promise.Result.Value.Exception!;

            using var emotes = promise.Result.Value.Asset.ConsumeEmotes();
            output.AddRange(emotes.Value);
            return promise.Result.Value.Asset.TotalAmount;
        }

        public async UniTask GetEmotesAsync(IReadOnlyCollection<URN> emoteIds, BodyShape bodyShape, CancellationToken ct, List<IEmote> output)
        {
            output.Clear();

            GetEmotesByPointersIntention intention = EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, emoteIds);
            var promise = PromiseByPointers.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            if (!promise.Result.HasValue)
                return;

            if (!promise.Result.Value.Succeeded)
                throw promise.Result.Value.Exception!;

            using var emotes = promise.Result.Value.Asset.ConsumeEmotes();
            output.AddRange(emotes.Value);
        }
    }
}
