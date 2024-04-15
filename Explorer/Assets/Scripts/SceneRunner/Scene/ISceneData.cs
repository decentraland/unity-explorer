using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public interface ISceneData
    {
        SceneShortInfo SceneShortInfo { get; }

        IReadOnlyList<Vector2Int> Parcels { get; }
        public ISceneContent SceneContent { get;}
        public SceneEntityDefinition SceneEntityDefinition { get; }

        /// <summary>
        ///     Position of the base parcel in the world
        /// </summary>
        ParcelMathHelper.SceneGeometry Geometry { get; }

        SceneAssetBundleManifest AssetBundleManifest { get; }

        /// <summary>
        ///     Main.crdt file that should be applied first before launching the scene
        /// </summary>
        StaticSceneMessages StaticSceneMessages { get; }

        bool HasRequiredPermission(string permission);

        /// <summary>
        ///     Translates URL encoded in SDK components into a path in the scene bundle
        ///     from which an asset can be downloaded from
        /// </summary>
        bool TryGetMainScriptUrl(out URLAddress result);

        /// <summary>
        ///     Translates URL encoded in SDK components into a path in the scene bundle
        ///     from which an asset can be downloaded from
        /// </summary>
        bool TryGetContentUrl(string url, out URLAddress result);

        /// <summary>
        ///     Translates the name of the scene asset into the hash, that can be used as part of URL
        /// </summary>
        bool TryGetHash(string name, out string hash);

        /// <summary>
        ///     Provides an internal (from the scene bundle) or an external URL based on scene permissions and allowed media hosts
        /// </summary>
        /// <param name="url"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        bool TryGetMediaUrl(string url, out URLAddress result);

        bool IsUrlDomainAllowed(string url);

        bool IsSdk7();

        class Fake : ISceneData
        {
            public SceneShortInfo SceneShortInfo => new (Vector2Int.zero, "Fake");
            public IReadOnlyList<Vector2Int> Parcels { get; } = new List<Vector2Int>();

            public ISceneContent SceneContent => new SceneNonHashedContent(new URLDomain());
            public SceneEntityDefinition SceneEntityDefinition => new (string.Empty, new SceneMetadata());
            public ParcelMathHelper.SceneGeometry Geometry => new (Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes());
            public SceneAssetBundleManifest AssetBundleManifest => SceneAssetBundleManifest.NULL;
            public StaticSceneMessages StaticSceneMessages => StaticSceneMessages.EMPTY;

            public bool HasRequiredPermission(string permission) =>
                true;

            public bool TryGetMainScriptUrl(out URLAddress result)
            {
                result = URLAddress.EMPTY;
                return false;
            }

            public bool TryGetContentUrl(string url, out URLAddress result)
            {
                result = URLAddress.EMPTY;
                return false;
            }

            public bool TryGetHash(string name, out string hash)
            {
                hash = string.Empty;
                return false;
            }

            public bool TryGetMediaUrl(string url, out URLAddress result)
            {
                result = URLAddress.EMPTY;
                return false;
            }

            public bool IsUrlDomainAllowed(string url) =>
                false;

            public bool IsSdk7() =>
                true;
        }
    }
}
