using CommunicationData.URLHelpers;
using Diagnostics;
using Utility;

namespace SceneRunner.Scene
{
    public interface ISceneData
    {
        SceneShortInfo SceneShortInfo { get; }

        /// <summary>
        ///     Position of the base parcel in the world
        /// </summary>
        ParcelMathHelper.SceneGeometry Geometry { get; }

        SceneAssetBundleManifest AssetBundleManifest { get; }

        /// <summary>
        /// Main.crdt file that should be applied first before launching the scene
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
    }
}
