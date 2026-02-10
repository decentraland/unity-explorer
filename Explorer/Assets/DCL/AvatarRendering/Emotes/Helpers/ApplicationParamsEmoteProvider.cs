using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.Web3;
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

        public async UniTask<(IReadOnlyList<ITrimmedEmote> results, int totalAmount)> GetOwnedEmotesAsync(
            CancellationToken ct,
            IEmoteProvider.OwnedEmotesRequestOptions requestOptions,
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

                await UniTask.WhenAll(GetEmotesAsync(pointers, BodyShape.MALE, ct, localBuffer),
                    GetEmotesAsync(pointers, BodyShape.FEMALE, ct, localBuffer));

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
                    await source.GetOwnedEmotesAsync(ct, requestOptions, localBuffer,
                        loadingArguments: new CommonLoadingArguments(
                            builderDTOsUrl.Replace(LoadingConstants.BUILDER_DTO_URL_COL_ID_PLACEHOLDER, collections[i]),
                            cancellationTokenSource: new CancellationTokenSource()
                        ),
                        needsBuilderAPISigning: true);
                }

                if (requestOptions is { pageNum: not null, pageSize: not null })
                {
                    int pageIndex = requestOptions.pageNum.Value - 1;
                    results.AddRange(localBuffer.Skip(pageIndex * requestOptions.pageSize.Value).Take(requestOptions.pageSize.Value));
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
            return await source.GetOwnedEmotesAsync(ct, requestOptions, results);
        }

        public UniTask GetEmotesAsync(IReadOnlyCollection<URN> emoteIds, BodyShape bodyShape, CancellationToken ct, List<IEmote> results) =>
            source.GetEmotesAsync(emoteIds, bodyShape, ct, results);
    }
}
