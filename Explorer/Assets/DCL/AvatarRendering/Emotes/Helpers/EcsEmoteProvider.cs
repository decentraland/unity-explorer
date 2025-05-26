using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.Web3;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using PromiseByPointers = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using OwnedEmotesPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetOwnedEmotesFromRealmIntention>;
using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

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

        public async UniTask<int> GetAsync(
            Web3Address userId,
            CancellationToken ct,
            IEmoteProvider.OwnedEmotesRequestOptions requestOptions,
            List<IEmote>? results = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false
        )
        {
            if (!loadingArguments.HasValue)
            {
                results?.Clear();
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
                loadingArguments = new CommonLoadingArguments(url);
            }

            var intention = new GetOwnedEmotesFromRealmIntention(loadingArguments.Value, needsBuilderAPISigning);
            var promise = await OwnedEmotesPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY).ToUniTaskAsync(world, cancellationToken: ct);

            if (!promise.Result.HasValue)
                return 0;

            if (!promise.Result.Value.Succeeded)
                throw promise.Result.Value.Exception!;

            using var emotes = promise.Result.Value.Asset.ConsumeEmotes();

            results?.AddRange(emotes.Value);

            if (needsBuilderAPISigning && results is { Count: > 0 })
            {
                Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results? {results.Count}");

                // int assetsAmount = 0;
                for (var i = 0; i < results.Count; i++)
                {
                    /*Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.id: {results[i].DTO.id}");
                    Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.ContentDownloadUrl: {results[i].DTO.ContentDownloadUrl}");
                    Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.type: {results[i].DTO.type}");
                    Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - 3 - {results[i].DTO.Metadata.AbstractData}");
                    Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - 4 - {results[i].DTO.Metadata.AbstractData.representations}");

                    var representations = results[i].DTO.Metadata.AbstractData.representations;
                    string mainFile = null;
                    for (var j = 0; j < representations.Length; j++)
                    {
                        Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.Metadata.AbstractData.representations[{j}].mainFile: {representations[j].mainFile}");
                        Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.Metadata.AbstractData.representations[{j}].bodyShapes[0]: {representations[j].bodyShapes[0]}");
                        AvatarAttachmentDTO.Content? glbContent = null;
                        bool sameMainFile = !string.IsNullOrEmpty(mainFile) && mainFile.Equals(representations[j].mainFile);

                        if (sameMainFile)
                            continue;

                        mainFile = representations[j].mainFile;

                        foreach (AvatarAttachmentDTO.Content content in results[i].DTO.content)
                        {
                            Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.content.file: {content.file}");
                            Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.content.hash: {content.hash}");

                            if (content.file.EndsWith(".glb") && content.file == mainFile)
                            {
                                glbContent = content;
                                break;
                            }
                        }

                        if (!glbContent.HasValue) continue;
                        // assetsAmount++;

                        var gltfPromise = GltfPromise.Create(world, GetGLTFIntention.Create(glbContent.Value.file, glbContent.Value.hash), PartitionComponent.TOP_PRIORITY);
                        world.Create(gltfPromise, results[i], BodyShape.MALE, i);
                        // world.Create(gltfPromise, results[i], BodyShape.FEMALE, i);
                    }*/

                    AvatarAttachmentDTO.Content? glbContent = null;
                    foreach (AvatarAttachmentDTO.Content content in results[i].DTO.content)
                    {
                        Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.content.file: {content.file}");
                        Debug.Log($"PRAVS - ECSEmoteProvider.GetAsync() - Results[{i}].DTO.content.hash: {content.hash}");
                        if (content.file.EndsWith(".glb"))
                        {
                            glbContent = content;
                            break;
                        }
                    }
                    if (!glbContent.HasValue) continue;
                    // assetsAmount++;

                    var gltfPromise = GltfPromise.Create(world, GetGLTFIntention.Create(glbContent.Value.file, glbContent.Value.hash), PartitionComponent.TOP_PRIORITY);
                    world.Create(gltfPromise, results[i], BodyShape.MALE, i);
                    // world.Create(gltfPromise, results[i], BodyShape.FEMALE, i);
                }

                // return assetsAmount;
            }

            return promise.Result.Value.Asset.TotalAmount;
        }

        public async UniTask RequestPointersAsync(IReadOnlyCollection<URN> emoteIds, BodyShape bodyShape, CancellationToken ct, List<IEmote> output)
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
