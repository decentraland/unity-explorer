using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Emotes
{
    public class ApplicationParamsEmoteProvider : IEmoteProvider
    {
        private readonly IAppArgs appArgs;
        private readonly IEmoteProvider source;
        private readonly string builderDTOsUrl;

        public ApplicationParamsEmoteProvider(IAppArgs appArgs, IEmoteProvider source, string builderDTOsUrl)
        {
            this.appArgs = appArgs;
            this.source = source;
            this.builderDTOsUrl = builderDTOsUrl;
        }

        public async UniTask<(IReadOnlyList<ITrimmedEmote> results, int totalAmount)> GetTrimmedByParamsAsync(
            IEmoteProvider.OwnedEmotesRequestOptions requestOptions,
            CancellationToken ct,
            List<ITrimmedEmote>? results = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false)
        {
            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_EMOTES, out string? emotesCsv))
            {
                URN[] pointers = emotesCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(s => new URN(s))
                                           .ToArray();

                results ??= new List<ITrimmedEmote>();

                using var bufferScope = ListPool<IEmote>.Get(out var localBuffer);
                await source.GetMetadataByPointersAsync(pointers, ct, localBuffer);
                if (requestOptions is { PageNum: not null, PageSize: not null })
                    results.AddRange(localBuffer.Skip((requestOptions.PageNum.Value - 1) * requestOptions.PageSize.Value).Take(requestOptions.PageSize.Value));
                else
                    results.AddRange(localBuffer);

                return (results, localBuffer.Count);
            }

            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS, out string? collectionsCsv))
            {
                string[] collections = collectionsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries);

                results ??= new List<ITrimmedEmote>();
                using var bufferScope = ListPool<ITrimmedEmote>.Get(out var localBuffer);
                for (var i = 0; i < collections.Length; i++)
                {
                    if (!BuilderCollectionIds.IsValid(collections[i], ReportCategory.EMOTE))
                        continue;

                    // localBuffer accumulates the loaded emotes
                    await source.GetTrimmedByParamsAsync(requestOptions, ct, localBuffer,
                        loadingArguments: new CommonLoadingArguments(
                            builderDTOsUrl.Replace(LoadingConstants.BUILDER_DTO_URL_COL_ID_PLACEHOLDER, collections[i]),
                            cancellationTokenSource: new CancellationTokenSource()
                        ),
                        needsBuilderAPISigning: true);
                }

                if (requestOptions is { PageNum: not null, PageSize: not null })
                {
                    int pageIndex = requestOptions.PageNum.Value - 1;
                    results.AddRange(localBuffer.Skip(pageIndex * requestOptions.PageSize.Value).Take(requestOptions.PageSize.Value));
                }
                else
                {
                    results.AddRange(localBuffer);
                }

                int count = localBuffer.Count;

                return (results, count);
            }

            // Regular path without any "self-preview" element
            return await source.GetTrimmedByParamsAsync(requestOptions, ct, results);
        }

        public UniTask GetMetadataByPointersAsync(IReadOnlyCollection<URN> pointers, CancellationToken ct, List<IEmote> results) =>
            source.GetMetadataByPointersAsync(pointers, ct, results);

        public async UniTask<IReadOnlyCollection<IEmote>?> GetByPointersAsync(IReadOnlyCollection<URN> emoteIds, BodyShape bodyShape, CancellationToken ct, List<IEmote>? results = null) =>
            await source.GetByPointersAsync(emoteIds, bodyShape, ct, results);
    }
}
