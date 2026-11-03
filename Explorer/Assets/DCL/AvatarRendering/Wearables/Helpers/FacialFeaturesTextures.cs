using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public readonly struct FacialFeaturesTextures
    {
        private readonly Dictionary<string, Dictionary<int, Texture>> texturesByCategory;
        public IReadOnlyDictionary<string, Dictionary<int, Texture>> Value => texturesByCategory;

        public FacialFeaturesTextures(Dictionary<string, Dictionary<int, Texture>> value)
        {
            texturesByCategory = value;
        }

        public void CopyInto(ref FacialFeaturesTextures other)
        {
            var texturesByCategory = other.texturesByCategory;

            foreach ((string? category, Dictionary<int, Texture>? existingTextures) in Value)
            {
                if (!texturesByCategory.TryGetValue(category, out Dictionary<int, Texture>? newTextures))
                {
                    newTextures = new Dictionary<int, Texture>();
                    texturesByCategory[category] = newTextures;
                }

                newTextures.Clear();

                foreach ((int textureId, Texture? texture) in existingTextures)
                    newTextures[textureId] = texture;
            }
        }
    }
}
