using DCL.SDKComponents.MediaStream;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;

namespace ECS.Unity.AssetLoad.Cache
{
    public class AssetPreLoadCache : IDisposable
    {
        /// <summary>
        ///     The never-handed-out template plus its live clones, so every copy can be released on teardown.
        /// </summary>
        private sealed class GltfTemplate
        {
            public readonly GltfContainerAsset Template;
            public readonly string Hash;
            public readonly List<GltfContainerAsset> Copies = new ();

            public GltfTemplate(GltfContainerAsset template, string hash)
            {
                Template = template;
                Hash = hash;
            }
        }

        private readonly IGltfContainerAssetsCache gltfCache;
        private readonly Dictionary<string, object> cache = new ();
        private readonly Dictionary<string, VideoTemplateData> videoCache = new ();

        public AssetPreLoadCache(IGltfContainerAssetsCache gltfCache)
        {
            this.gltfCache = gltfCache;
        }

        public bool TryAddGltf(string key, string hash, GltfContainerAsset template) =>
            cache.TryAdd(key, new GltfTemplate (template, hash));

        public bool ContainsGltf(string key) =>
            cache.TryGetValue(key, out object? value) && value is GltfTemplate;

        public bool TryGetGltfInstance(string key, out GltfContainerAsset? instance)
        {
            if (cache.TryGetValue(key, out object? value) && value is GltfTemplate gltfTemplate
                && Utils.TryDuplicateGltfAssetFromTemplate(gltfTemplate.Template, gltfTemplate.Hash, out GltfContainerAsset? duplicate))
            {
                gltfTemplate.Copies.Add(duplicate!);
                instance = duplicate;
                return true;
            }

            instance = null;
            return false;
        }

        public void ReleaseGltfInstance(string key, GltfContainerAsset instance)
        {
            if (cache.TryGetValue(key, out object? value) && value is GltfTemplate gltfTemplate)
                gltfTemplate.Copies.Remove(instance);

            instance.Dispose();
        }

        public bool TryAddVideo(string key, in VideoTemplateData data) =>
            videoCache.TryAdd(key, data);

        public bool TryGetVideoTemplate(string key, out VideoTemplateData data) =>
            videoCache.TryGetValue(key, out data);

        public bool TryAdd<T>(string key, T asset)
        {
            if (cache.TryAdd(key, asset))
            {
                switch (asset)
                {
                    // AudioClipData and TextureData are reference counted, so we need to acquire a reference when adding them to the cache so that they are not disposed while cached and not being used.
                    // GltfContainerAsset is handled differently as it is not ref counted
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

        public void Dispose() =>
            Clear();

        public void Clear()
        {
            foreach(var kvp in cache)
                switch (kvp.Value)
                {
                    case GltfTemplate gltfTemplate:
                        foreach (GltfContainerAsset copy in gltfTemplate.Copies)
                            copy.Dispose();

                        gltfCache.Dereference(kvp.Key, gltfTemplate.Template, handleAssetLoad: false);
                        break;
                    case AudioClipData audioClipData:
                        audioClipData.Dereference();
                        break;
                    case TextureData textureData:
                        textureData.Dereference();
                        break;
                }

            cache.Clear();
            videoCache.Clear();
        }
    }
}
