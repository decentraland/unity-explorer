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
        [JsonProperty("versions")]
        public AssetBundleManifestVersion? assetBundleManifestVersion;

        [JsonProperty("status")]
        public AssetBundleRegistryEnum assetBundleRegistryEnum;

        protected EntityDefinitionBase() { }

        protected EntityDefinitionBase(string id)
        {
            this.id = id;
        }

        public override string ToString() => id ?? string.Empty;

        public bool SupportsStaticScene()
        {
            //TODO (JUANI): FOr now, we hardcoded it only for GP. We will later check it with manifest
            return id.Equals("bafkreicboazl7vyrwx7xujne53e63di6khbcfoi4vabafomar4u5mznpzy");
        }
    }
}
