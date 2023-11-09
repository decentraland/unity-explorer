using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class WearableCatalog
    {
        public readonly Dictionary<string, IWearable> WearableDictionary = new ();

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

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category) =>
            WearableDictionary[WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category)];
    }
}
