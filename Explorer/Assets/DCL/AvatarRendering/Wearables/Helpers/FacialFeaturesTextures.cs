using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public readonly struct FacialFeaturesTextures
    {
        public readonly IReadOnlyDictionary<string, Dictionary<int, Texture>> Value;

        public FacialFeaturesTextures(IReadOnlyDictionary<string, Dictionary<int, Texture>> value)
        {
            Value = value;
        }

        public Texture this[string category, int originalTextureId] => Value[category][originalTextureId];

        public FacialFeaturesTextures Clone()
        {
            var texturesByCategory = new Dictionary<string, Dictionary<int, Texture>>();

            foreach (var kvp in Value)
            {
                var textures = new Dictionary<int, Texture>(kvp.Value);
                texturesByCategory[kvp.Key] = textures;
            }

            return new FacialFeaturesTextures(texturesByCategory);
        }
    }
}
