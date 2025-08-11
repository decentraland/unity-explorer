using DCL.Platforms;
using Newtonsoft.Json;
using System;

namespace DCL.Ipfs
{
    /// <summary>
    /// Base class for entity definitions that provides common properties and asset bundle manifest functionality
    /// </summary>
    [Serializable]
    public abstract class EntityDefinitionBase
    {
        public string? id;
        public string type;
        public long timestamp;
        public string version;
        public ContentDefinition[] content;
        public string[] pointers;

        // Asset bundle manifest properties
        public bool assetBundleManifestRequestFailed;
        public string assetBundleBuildDate;
        private const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;
        public AssetBundleManifestVersion? versions;
        private bool? HasHashInPathValue;

        [JsonProperty("status")]
        public AssetBundleRegistryEnum assetBundleRegistryEnum;

        protected EntityDefinitionBase() { }

        protected EntityDefinitionBase(string id)
        {
            this.id = id;
        }

        public string GetAssetBundleManifestVersion()
        {
            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
                return versions.assets.windows;
            else
                return versions.assets.mac;
        }

        public bool HasHashInPath()
        {
            if (HasHashInPathValue == null)
                HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;

            return HasHashInPathValue.Value;
        }

        public override string ToString() => id ?? string.Empty;
    }
}
