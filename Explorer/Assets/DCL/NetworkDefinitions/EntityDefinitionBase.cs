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
#pragma warning disable UAC1001 // Newtonsoft-only wire field: the JSON key is "versions" (a JsonProperty rename Unity serialization cannot express), and every other producer assigns this runtime object from C#.
        [JsonProperty("versions")]
        public AssetBundleManifestVersion? assetBundleManifestVersion;
#pragma warning restore UAC1001

        /// <summary>The manifest version, or the failed sentinel when none was resolved — AB intentions require a manifest, and the sentinel is the same dead end the pipeline already handles.</summary>
        [JsonIgnore]
        public AssetBundleManifestVersion AssetBundleManifestVersionOrFailed => assetBundleManifestVersion ?? AssetBundleManifestVersion.FAILED;

        [JsonProperty("status")]
        public AssetBundleRegistryEnum assetBundleRegistryEnum;
    }
}
