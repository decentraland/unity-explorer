using Newtonsoft.Json;
using System;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace DCL.Ipfs
{
    /// <summary>
    /// Base class for entity definitions that provides common properties and asset bundle manifest functionality
    /// </summary>
    // Server schema: decentraland/common-schemas src/platform/entity.ts#/Entity
    [Serializable]
    public abstract class EntityDefinitionBase : TrimmedEntityDefinitionBase
    {
        public string type = null!;
        public long timestamp;
        public string version = null!;
        public string[] pointers = null!;
        public ContentDefinition[] content = null!;

        protected EntityDefinitionBase() { }

        protected EntityDefinitionBase(string id)
        {
            this.id = id;
        }

        public override string ToString() =>
            id ?? string.Empty;
    }

    [Serializable]
    public class TrimmedEntityDefinitionBase
    {
        public string? id;
        public string? thumbnail;

        // Asset bundle manifest properties
        [JsonProperty("versions")]
        public AssetBundleManifestVersion? assetBundleManifestVersion;

        //Cached: CreateFailed() allocates three objects and this property is read from per-entity-load paths.
        private static readonly AssetBundleManifestVersion FAILED_MANIFEST = AssetBundleManifestVersion.CreateFailed();

        /// <summary>The manifest version, or a failed sentinel when none was resolved — AB intentions require a manifest, and the sentinel is the same dead end the pipeline already handles.</summary>
        [JsonIgnore]
        public AssetBundleManifestVersion AssetBundleManifestVersionOrFailed => assetBundleManifestVersion ?? FAILED_MANIFEST;

        [JsonProperty("status")]
        public AssetBundleRegistryEnum assetBundleRegistryEnum;
    }
}
