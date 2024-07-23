using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace SceneRunner.EmptyScene
{
    /// <summary>
    ///     Exists in a single instance, shared between all empty scenes
    /// </summary>
    public class EmptySceneData : ISceneData
    {
        public bool SceneLoadingConcluded { get => true; set { } }

        /// <summary>
        ///     Per scene data is not resolved as empty scenes use the shared world for all instances
        /// </summary>
        public SceneShortInfo SceneShortInfo { get; }
        public IReadOnlyList<Vector2Int> Parcels { get; }
        public ISceneContent SceneContent { get; }
        public SceneEntityDefinition SceneEntityDefinition { get; }

        public ParcelMathHelper.SceneGeometry Geometry => ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY;

        public SceneAssetBundleManifest AssetBundleManifest => SceneAssetBundleManifest.NULL;
        public StaticSceneMessages StaticSceneMessages => StaticSceneMessages.EMPTY;

        public EmptySceneData(IReadOnlyList<Vector2Int> parcels)
        {
            Parcels = parcels;
            SceneShortInfo = new SceneShortInfo(Vector2Int.zero, "Empty Scene");
        }

        public bool HasRequiredPermission(string permission) =>
            throw new NotImplementedException();

        public bool TryGetMainScriptUrl(out URLAddress result) =>
            throw new NotImplementedException();

        public bool TryGetContentUrl(string url, out URLAddress result) =>
            throw new NotImplementedException();

        public bool TryGetHash(string name, out string hash) =>
            throw new NotImplementedException();

        public bool TryGetMediaUrl(string url, out URLAddress result) =>
            throw new NotImplementedException();

        public bool IsUrlDomainAllowed(string url) =>
            throw new NotImplementedException();

        public bool IsSdk7() =>
            true;
    }
}
