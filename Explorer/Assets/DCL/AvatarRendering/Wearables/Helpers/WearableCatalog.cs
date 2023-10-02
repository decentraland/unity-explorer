using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class WearableCatalog : IDisposable
    {
        internal Dictionary<string, IWearable> wearableDictionary;

        public WearableCatalog()
        {
            wearableDictionary = new Dictionary<string, IWearable>();
        }

        public void AddWearableByDTO(WearableDTO wearableDto, out IWearable addedWearable)
        {
            if (wearableDictionary.TryGetValue(wearableDto.metadata.id, out IWearable exitingWearable))
                addedWearable = exitingWearable;
            else
            {
                var wearable = new Wearable();
                wearable.WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto);
                wearable.IsLoading = false;
                wearableDictionary.Add(wearable.GetUrn(), wearable);
                addedWearable = wearable;
            }
        }

        public void AddEmptyWearable(string loadingIntentionPointer)
        {
            wearableDictionary.Add(loadingIntentionPointer, new Wearable());
        }

        public bool TryGetWearable(string wearableURN, out IWearable wearable)
        {
            if (wearableDictionary.TryGetValue(wearableURN, out IWearable resultWearable))
            {
                wearable = resultWearable;
                return true;
            }

            wearable = null;
            return false;
        }

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category) =>
            wearableDictionary[WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category)];

        public void Dispose()
        {
            foreach (IWearable wearableDictionaryValue in wearableDictionary.Values)
                wearableDictionaryValue.Dispose();
        }
    }
}
