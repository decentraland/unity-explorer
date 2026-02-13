using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Diagnostics;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables
{
    public static class ElementProviderHelper
    {
        public static async UniTaskVoid FetchElementByPointerAndExecuteAsync<TTrimmedElement, TElement, TParams, TElementDTO>(
            string pointer,
            IElementsProvider<TTrimmedElement, TElement, TParams> elementProvider,
            IAvatarElementStorage<TElement, TElementDTO> elementStorage,
            IReadOnlyEquippedWearables equippedWearables,
            Action<TElement> onElementFetched,
            CancellationToken ct,
            ReportData reportData)
            where TElement: IAvatarAttachment<TElementDTO>
            where TElementDTO: AvatarAttachmentDTO
        {
            if (elementStorage.TryGetElement(pointer, out var element))
            {
                // Element could be in the storage but still loading their data. Wait until they finish loading.
                await UniTask.WaitWhile(() => element.IsLoading, cancellationToken: ct);

                await UniTask.SwitchToMainThread();
                onElementFetched(element);

                return;
            }

            List<URN> urnRequest = ListPool<URN>.Get();
            List<TElement> results = ListPool<TElement>.Get();

            try
            {
                urnRequest.Add(pointer);

                BodyShape currenBodyShape = BodyShape.FromStringSafe(equippedWearables.Wearable(WearableCategories.Categories.BODY_SHAPE)!.GetUrn());

                await elementProvider.GetByPointersAsync(urnRequest, currenBodyShape, ct, results);

                if (results != null)
                    foreach (var result in results)
                        if (result.GetUrn() == pointer)
                        {
                            await UniTask.SwitchToMainThread();
                            onElementFetched(result);
                            return;
                        }

                ReportHub.LogError(reportData, $"Couldn't fetch element of type {typeof(TElement)} for pointer: {pointer}");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.EMOTE)); }
            finally
            {
                ListPool<URN>.Release(urnRequest);
                ListPool<TElement>.Release(results);
            }
        }
    }
}
