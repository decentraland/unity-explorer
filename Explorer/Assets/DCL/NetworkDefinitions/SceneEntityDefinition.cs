using System;
using UnityEngine;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata>
    {
        private string? logSceneName;
        private SceneFastLookup? SceneLookup;

        /// <summary>
        ///     Initial Scene State for this scene (descriptor JSON + bundle reachability).
        ///     Populated by <see cref="ISSDescriptor.ResolveAsync"/> during scene-definition loading.
        ///     Null until the load systems have run (e.g. for definitions built outside the loader, like wearable-preview scenes).
        /// </summary>
        public ISSDescriptor ISSDescriptor;

        public SceneEntityDefinition() { }

        public SceneEntityDefinition(string id, SceneMetadata metadata, AssetBundleManifestVersion? assetBundleManifestVersion = null) : base(id, metadata)
        {
            this.assetBundleManifestVersion = assetBundleManifestVersion;
            ISSDescriptor = ISSDescriptor.NULL;
        }

        public string GetLogSceneName() =>
            logSceneName ??= $"{metadata.scene?.DecodedBase} - {id}";

        public bool Contains(int x, int y)
        {
            if(SceneLookup.HasValue)
                return SceneLookup.Value.Contains(x, y);

            SceneLookup = new SceneFastLookup(metadata.scene.DecodedParcels);
            return SceneLookup.Value.Contains(x, y);
        }

        public bool Contains(Vector2Int parcel) =>
            Contains(parcel.x, parcel.y);
    }
}
