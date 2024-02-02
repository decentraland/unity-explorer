using JetBrains.Annotations;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct HideWearablesResolution
    {
        public readonly IReadOnlyCollection<string> ForceRender;

        [CanBeNull] public List<IWearable> VisibleWearables;

        /// <summary>
        ///     This list is calculated on wearables resolution and it's used on avatar instantiation
        /// </summary>
        [CanBeNull] public HashSet<string> HiddenCategories;

        public HideWearablesResolution(IReadOnlyCollection<string> forceRender)
        {
            ForceRender = forceRender;
            VisibleWearables = null;
            HiddenCategories = null;
        }
    }
}
