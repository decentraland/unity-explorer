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
        ///     Defaults to <see cref="ISSDescriptor.NONE"/> so consumers never have to null-check
        ///     (definitions built outside the loader — wearable-preview / portable-experience —
        ///     keep the NONE singleton and behave as "no ISS").
        /// </summary>
        public ISSDescriptor ISSDescriptor = ISSDescriptor.NONE;

        public SceneEntityDefinition() { }

        public SceneEntityDefinition(string id, SceneMetadata metadata, AssetBundleManifestVersion? assetBundleManifestVersion = null) : base(id, metadata)
        {
            this.assetBundleManifestVersion = assetBundleManifestVersion;
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
