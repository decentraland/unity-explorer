using DCL.SDKComponents.MediaStream;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;

namespace DCL.ECS.Unity.AssetLoad.Cache
{
    public class AssetLoadCache : IDisposable
    {
        private readonly IGltfContainerAssetsCache gltfCache;
        private readonly Dictionary<string, object> cache = new ();

        public AssetLoadCache(IGltfContainerAssetsCache gltfCache)
        {
            this.gltfCache = gltfCache;
        }

        public bool TryAdd<T>(string key, T asset)
        {
            if (cache.TryAdd(key, asset))
            {
                switch (asset)
                {
                    case AudioClipData audioClipData:
                        audioClipData.AcquireRef();
                        break;
                    case TextureData textureData:
                        textureData.AcquireRef();
                        break;
                }

                return true;
            }

            return false;
        }

        public bool TryGet<T>(string key, out T asset)
        {
            if (cache.TryGetValue(key, out object? value) && value is T typedValue)
            {
                asset = typedValue;
                return true;
            }

            asset = default;
            return false;
        }

        public void Dispose()
        {
            foreach(var kvp in cache)
                switch (kvp.Value)
                {
                    case GltfContainerAsset gltfAsset:
                        gltfCache.Dereference(kvp.Key, gltfAsset);
                        break;
                    case AudioClipData audioClipData:
                        audioClipData.Dereference();
                        break;
                    case TextureData textureData:
                        textureData.Dereference();
                        break;
                    case MediaPlayerComponent mediaPlayerComponent:
                        mediaPlayerComponent.Dispose();
                        break;
                }

            cache.Clear();
        }
    }
}
