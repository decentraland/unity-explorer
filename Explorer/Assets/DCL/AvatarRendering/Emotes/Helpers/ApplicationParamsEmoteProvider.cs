using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Web3;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public class ApplicationParamsEmoteProvider : IEmoteProvider
    {
        private readonly IAppArgs appArgs;
        private readonly IEmoteProvider source;

        public ApplicationParamsEmoteProvider(IAppArgs appArgs,
            IEmoteProvider source)
        {
            this.appArgs = appArgs;
            this.source = source;
        }

        public async UniTask<int> GetOwnedEmotesAsync(Web3Address userId, CancellationToken ct,
            IEmoteProvider.OwnedEmotesRequestOptions requestOptions,
            List<IEmote> output)
        {
            if (!appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_EMOTES, out string? emotesCsv))
                return await source.GetOwnedEmotesAsync(userId, ct, requestOptions, output);

            URN[] pointers = emotesCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => new URN(s))
                                       .ToArray();

            await UniTask.WhenAll(GetEmotesAsync(pointers, BodyShape.MALE, ct, output),
                GetEmotesAsync(pointers, BodyShape.FEMALE, ct, output));

            return output.Count;
        }

        public UniTask GetEmotesAsync(IReadOnlyCollection<URN> emoteIds, BodyShape bodyShape, CancellationToken ct, List<IEmote> output) =>
            source.GetEmotesAsync(emoteIds, bodyShape, ct, output);
    }
}
