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
        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        private const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;


        public string? id;
        public string type;
        public long timestamp;
        public string version;
        public ContentDefinition[] content;
        public string[] pointers;

        // Asset bundle manifest properties
        public bool assetBundleManifestRequestFailed;
        public string assetBundleBuildDate;
        public AssetBundleManifestVersion versions;
        private bool? HasHashInPathValue;

        [JsonProperty("status")]
        public AssetBundleRegistryEnum assetBundleRegistryEnum;

        protected EntityDefinitionBase() { }

        protected EntityDefinitionBase(string id)
        {
            this.id = id;
        }

        public string GetAssetBundleManifestVersion() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? versions.assets.windows : versions.assets.mac;

        public bool HasHashInPath()
        {
            if (HasHashInPathValue == null)
                HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;

            return HasHashInPathValue.Value;
        }

        public override string ToString() => id ?? string.Empty;
    }
}
