using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public struct FacialFeaturesTextures
    {
        public readonly IReadOnlyDictionary<string, Dictionary<int, Texture>> Value;

        public FacialFeaturesTextures(IReadOnlyDictionary<string, Dictionary<int, Texture>> value)
        {
            Value = value;
        }
    }
}
