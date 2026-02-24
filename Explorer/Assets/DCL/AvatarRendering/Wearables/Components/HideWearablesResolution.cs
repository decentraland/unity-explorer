using System.Collections.Generic;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct HideWearablesResolution
    {
        public readonly IReadOnlyCollection<string>? ForceRender;

        public int VisibleWearablesCount { get; private set; }

        public List<IWearable>? VisibleWearables { get; private set; }

        /// <summary>
        ///     This list is calculated on wearables resolution and it's used on avatar instantiation
        /// </summary>
        public HashSet<string>? HiddenCategories;

        public HideWearablesResolution(IReadOnlyCollection<string> forceRender)
        {
            ForceRender = forceRender;
            HiddenCategories = null;
            VisibleWearablesCount = 0;
            VisibleWearables = null;
        }

        public void SetVisibleWearables(List<IWearable> visibleWearables)
        {
            VisibleWearables = visibleWearables;

            // Count is preserved so it can be used after Release is called
            VisibleWearablesCount = visibleWearables.Count;
        }

        public void Release()
        {
            if (VisibleWearables != null)
                WEARABLES_POOL.Release(VisibleWearables);

            if (HiddenCategories != null)
                CATEGORIES_POOL.Release(HiddenCategories);
        }
    }
}
