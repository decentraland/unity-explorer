using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
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

                var localBuffer = ListPool<IEmote>.Get();

                await UniTask.WhenAll(GetByPointersAsync(pointers, BodyShape.MALE, ct, localBuffer),
                    GetByPointersAsync(pointers, BodyShape.FEMALE, ct, localBuffer));

                foreach (var emote in localBuffer)
                    results.Add(emote);

                ListPool<IEmote>.Release(localBuffer);

                return (results, results.Count);
            }

            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS, out string? collectionsCsv))
            {
                string[] collections = collectionsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries);

                results ??= new List<ITrimmedEmote>();
                var localBuffer = ListPool<ITrimmedEmote>.Get();
                for (var i = 0; i < collections.Length; i++)
                {
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
                ListPool<ITrimmedEmote>.Release(localBuffer);

                return (results, count);
            }

            // Regular path without any "self-preview" element
            return await source.GetTrimmedByParamsAsync(requestOptions, ct, results);
        }

        public async UniTask<IReadOnlyCollection<IEmote>?> GetByPointersAsync(IReadOnlyCollection<URN> emoteIds, BodyShape bodyShape, CancellationToken ct, List<IEmote>? results = null) =>
            await source.GetByPointersAsync(emoteIds, bodyShape, ct, results);
    }
}
