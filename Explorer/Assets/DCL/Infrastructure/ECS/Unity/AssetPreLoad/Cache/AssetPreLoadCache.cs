using DCL.SDKComponents.MediaStream;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Textures;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ECS.Unity.AssetLoad.Cache
{
    public class AssetPreLoadCache : IDisposable
    {
        /// <summary>
        ///     The never-handed-out template. Clones handed out by <see cref="TryGetGltfInstance" /> are owned
        ///     by the containers that checked them out and are released through
        ///     <see cref="IGltfContainerAssetsCache.Dereference" /> — this cache must never dispose them.
        /// </summary>
        private sealed class GltfTemplate
        {
            public readonly GltfContainerAsset Template;
            public readonly string Hash;

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
                instance = duplicate;
                return true;
            }

            instance = null;
            return false;
        }

        public bool TryAddVideo(string key, in VideoTemplateData data) =>
            videoCache.TryAdd(key, data);

        public bool TryGetVideoTemplate(string key, out VideoTemplateData data) =>
            videoCache.TryGetValue(key, out data);

        public bool TryAdd<T>(string key, T asset)
        {
            if (asset is not null && cache.TryAdd(key, asset))
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

        public bool TryGet<T>(string key, [MaybeNullWhen(false)] out T asset)
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
                    // Only the never-handed-out template is released. Checked-out clones are owned by
                    // containers whose lifetime this cache does not control (it is global, Clear runs per
                    // scene teardown) — disposing them here would destroy live Roots under their owners.
                    case GltfTemplate gltfTemplate:
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
