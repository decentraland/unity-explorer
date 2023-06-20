using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    public interface ISceneData
    {
        string SceneName { get; }

        SceneAssetBundleManifest AssetBundleManifest { get; }

        IReadOnlyList<Vector2Int> Parcels { get; }

        Vector2Int BaseParcel { get; }

        bool HasRequiredPermission(string permission);

        /// <summary>
        ///     Translates URL encoded in SDK components into a path in the scene bundle
        ///     from which an asset can be downloaded from
        /// </summary>
        bool TryGetMainScriptUrl(out string result);

        /// <summary>
        ///     Translates URL encoded in SDK components into a path in the scene bundle
        ///     from which an asset can be downloaded from
        /// </summary>
        bool TryGetContentUrl(string url, out string result);

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
        bool TryGetMediaUrl(string url, out string result);

        bool IsUrlDomainAllowed(string url);
    }
}
