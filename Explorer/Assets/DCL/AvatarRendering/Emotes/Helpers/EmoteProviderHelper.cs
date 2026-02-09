using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Diagnostics;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Emotes
{
    public static class EmoteProviderHelper
    {
        public static async UniTaskVoid FetchEmoteByPointerAndExecuteAsync(string pointer,
            IEmoteProvider emoteProvider,
            IEmoteStorage emoteStorage,
            IReadOnlyEquippedWearables equippedWearables,
            Action<IEmote> onEmoteFetched,
            CancellationToken ct)
        {
            if (emoteStorage.TryGetElement(pointer, out var emote))
            {
                // Emote could be in the storage but still loading their data. Wait until they finish loading.
                await UniTask.WaitWhile(() => emote.IsLoading, cancellationToken: ct);

                await UniTask.SwitchToMainThread();
                onEmoteFetched(emote);

                return;
            }

            List<URN> urnRequest = ListPool<URN>.Get();
            List<IEmote> results = ListPool<IEmote>.Get();

            try
            {
                urnRequest.Add(pointer);

                BodyShape currenBodyShape = BodyShape.FromStringSafe(equippedWearables.Wearable(WearableCategories.Categories.BODY_SHAPE)!.GetUrn());

                await emoteProvider.GetEmotesAsync(urnRequest, currenBodyShape, ct, results);

                if (results != null)
                    foreach (var result in results)
                        if (result.GetUrn() == pointer)
                        {
                            await UniTask.SwitchToMainThread();
                            onEmoteFetched(result);
                            return;
                        }

                ReportHub.LogError(new ReportData(ReportCategory.EMOTE), $"Couldn't fetch emote for pointer: {pointer}");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.EMOTE)); }
            finally
            {
                ListPool<URN>.Release(urnRequest);
                ListPool<IEmote>.Release(results);
            }
        }
    }
}
