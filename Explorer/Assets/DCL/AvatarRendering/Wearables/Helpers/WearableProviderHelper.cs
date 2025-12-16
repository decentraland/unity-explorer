using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Diagnostics;
using Runtime.Wearables;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableProviderHelper
    {
        public static async UniTaskVoid FetchWearableByPointerAndExecuteAsync(string pointer, IWearablesProvider wearablesProvider, IReadOnlyEquippedWearables equippedWearables, Action<IWearable> onWearableFetched, CancellationToken ct)
        {
            List<URN> urnRequest = ListPool<URN>.Get();
            try
            {
                urnRequest.Add(pointer);

                BodyShape currenBodyShape = BodyShape.FromStringSafe(equippedWearables.Wearable(WearableCategories.Categories.BODY_SHAPE)!.GetUrn());

                var results = await wearablesProvider.RequestPointersAsync(urnRequest, currenBodyShape, ct);

                if (results != null)
                    foreach (var result in results)
                        if (result.GetUrn() == pointer)
                        {
                            onWearableFetched(result);
                            return;
                        }

                ReportHub.LogError(new ReportData(ReportCategory.WEARABLE), $"Couldn't fetch wearable for pointer: {pointer}");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.WEARABLE)); }
            finally { ListPool<URN>.Release(urnRequest); }
        }
    }
}
