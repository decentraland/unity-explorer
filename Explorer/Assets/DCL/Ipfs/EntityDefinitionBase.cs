using DCL.Infrastructure.ECS.StreamableLoading.AssetBundles.AssetBundleManifestHelper;
using Newtonsoft.Json;
using System;

namespace DCL.Ipfs
{
    /// <summary>
    /// Base class for entity definitions that provides common properties and asset bundle manifest functionality
    /// </summary>
    [Serializable]
    public abstract class EntityDefinitionBase : IApplyAssetBundleManifestResult
    {
        public string? id;
        public string type;
        public long timestamp;
        public string version;
        public ContentDefinition[] content;
        public string[] pointers;

        // Asset bundle manifest properties
        public string assetBundleManifestVersion;
        public bool hasSceneInPath;
        public bool assetBundleManifestRequestFailed;
        public string assetBundleBuildDate;

        [JsonProperty("status")]
        public AssetBundleRegistryEnum assetBundleRegistryEnum;

        protected EntityDefinitionBase() { }

        protected EntityDefinitionBase(string id)
        {
            this.id = id;
        }

        public virtual void ApplyAssetBundleManifestResult(string assetBundleManifestVersion, bool hasSceneIDInPath)
        {
            this.assetBundleManifestVersion = assetBundleManifestVersion;
            this.hasSceneInPath = hasSceneIDInPath;
        }

        public virtual void ApplyFailedManifestResult()
        {
            assetBundleManifestRequestFailed = true;
        }

        public override string ToString() => id ?? string.Empty;
    }
}
