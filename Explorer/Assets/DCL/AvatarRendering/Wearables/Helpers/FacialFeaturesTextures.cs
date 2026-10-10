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

        public Texture this[string category, int originalTextureId] => Value[category][originalTextureId];

        public void CopyInto(ref FacialFeaturesTextures other)
        {
            var texturesByCategory = other.texturesByCategory;

            foreach ((string? category, Dictionary<int, Texture>? existingTextures) in Value)
            {
                if (!texturesByCategory.ContainsKey(category))
                    texturesByCategory[category] = new Dictionary<int, Texture>();

                var newTextures = texturesByCategory[category];
                newTextures.Clear();

                foreach ((int textureId, Texture? texture) in existingTextures)
                    newTextures[textureId] = texture;
            }
        }
    }
}
