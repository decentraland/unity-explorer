using System;
using System.Collections.Generic;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Wearables.Components
{
    /// <summary>
    ///     Final result of wearables resolution
    /// </summary>
    public readonly struct WearablesResolution : IDisposable
    {
        public static readonly WearablesResolution EMPTY = new (new List<IWearable>());
        private readonly List<IWearable> wearables;

        /// <summary>
        ///     This list is calculated on wearables resolution and it's used on avatar instantiation, poolable
        /// </summary>
        public readonly HashSet<string>? HiddenCategories;

        /// <summary>
        ///     Poolable collection of result wearables
        /// </summary>
        public IReadOnlyList<IWearable> Wearables => wearables;

        public WearablesResolution(List<IWearable> wearables, HashSet<string>? hiddenCategories = null)
        {
            HiddenCategories = hiddenCategories;
            this.wearables = wearables;
        }

        public void Dispose()
        {
            WEARABLES_POOL.Release(wearables);

            if (HiddenCategories != null)
                CATEGORIES_POOL.Release(HiddenCategories);
        }
    }
}
