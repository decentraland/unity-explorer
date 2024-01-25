using JetBrains.Annotations;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct HideWearablesResolution
    {
        public readonly IReadOnlyCollection<string> ForceRender;
        [CanBeNull] public List<IWearable> VisibleWearables;

        public HideWearablesResolution(IReadOnlyCollection<string> forceRender)
        {
            ForceRender = forceRender;
            VisibleWearables = null;
        }
    }
}
