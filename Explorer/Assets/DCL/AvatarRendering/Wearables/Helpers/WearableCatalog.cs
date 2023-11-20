using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class WearableCatalog
    {
        public Dictionary<string, IWearable> WearableDictionary { get; } = new ();

        public int WearableAssetsInCatalog
        {
            get
            {
                var sum = 0;

                foreach (IWearable wearable in WearableDictionary.Values)
                {
                    if (wearable.WearableAssets != null)
                    {
                        var count = 0;

                        foreach (StreamableLoadingResult<WearableAsset>? result in wearable.WearableAssets)
                            if (result is { Asset: not null })
                                count++;

                        sum += count;
                    }
                }

                return sum;
            }
        }

        public IWearable GetOrAddWearableByDTO(WearableDTO wearableDto)
        {
            if (WearableDictionary.TryGetValue(wearableDto.metadata.id, out IWearable exitingWearable))
                return exitingWearable;

            var wearable = new Wearable
            {
                WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto),
                IsLoading = false,
            };

            WearableDictionary.Add(wearable.GetUrn(), wearable);
            return wearable;
        }

        public void AddEmptyWearable(string loadingIntentionPointer)
        {
            WearableDictionary.Add(loadingIntentionPointer, new Wearable());
        }

        public bool TryGetWearable(string wearableURN, out IWearable wearable)
        {
            if (WearableDictionary.TryGetValue(wearableURN, out IWearable resultWearable))
            {
                wearable = resultWearable;
                return true;
            }

            wearable = null;
            return false;
        }

        public void UnloadWearableAssets()
        {
            foreach (IWearable wearable in WearableDictionary.Values)
                for (var i = 0; i < wearable.WearableAssets?.Length; i++)
                {
                    WearableAsset wearableAsset = wearable.WearableAssets[i]?.Asset;

                    if (wearableAsset == null)
                    {
                        wearable.WearableAssets[i] = null;
                        continue;
                    }

                    if (wearableAsset.ReferenceCount == 0)
                    {
                        wearableAsset.Dispose();
                        wearable.WearableAssets[i] = null;
                    }
                }
        }

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category) =>
            WearableDictionary[WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category)];
    }
}
