using System;
using System.Collections.Generic;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct HideWearablesResolution
    {
        public readonly IReadOnlyCollection<string>? ForceRender;

        public List<IWearable>? VisibleWearables;

        /// <summary>
        ///     This list is calculated on wearables resolution and it's used on avatar instantiation
        /// </summary>
        public HashSet<string>? HiddenCategories;

        public HideWearablesResolution(IReadOnlyCollection<string> forceRender)
        {
            ForceRender = forceRender;
            VisibleWearables = null;
            HiddenCategories = null;
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
