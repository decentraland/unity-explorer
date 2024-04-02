using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    // ReSharper disable once InconsistentNaming
    public readonly struct WearablesDTOList
    {
        public readonly IReadOnlyList<WearableDTO> Value;

        public WearablesDTOList(IReadOnlyList<WearableDTO> value)
        {
            Value = value;
        }
    }
}
