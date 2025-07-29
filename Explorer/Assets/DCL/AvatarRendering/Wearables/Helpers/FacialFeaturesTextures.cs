using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public readonly struct FacialFeaturesTextures : IDisposable
    {
        public readonly IReadOnlyDictionary<string, Dictionary<int, Texture>> Value;

        public FacialFeaturesTextures(IReadOnlyDictionary<string, Dictionary<int, Texture>> value)
        {
            Value = value;
        }

        public Texture this[string category, int originalTextureId] => Value[category][originalTextureId];

        public FacialFeaturesTextures CreateCopy()
        {
            var texturesByCategory = DictionaryPool<string, Dictionary<int, Texture>>.Get();

            foreach ((string? category, Dictionary<int, Texture>? existingTextures) in Value)
            {
                var newTextures = DictionaryPool<int, Texture>.Get();

                foreach ((int textureId, Texture? texture) in existingTextures)
                    newTextures[textureId] = texture;

                texturesByCategory[category] = newTextures;
            }

            return new FacialFeaturesTextures(texturesByCategory);
        }

        public void Dispose()
        {
            foreach (var textures in Value.Values)
                DictionaryPool<int, Texture>.Release(textures);

            DictionaryPool<string, Dictionary<int, Texture>>.Release((Dictionary<string, Dictionary<int, Texture>>)Value);
        }
    }
}
