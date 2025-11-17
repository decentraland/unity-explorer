using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public interface ISceneData
    {
        /// <summary>
        ///     SceneLoadingConcluded is TRUE when the scene has been repositioned to its rightful place away from MORDOR
        /// </summary>
        bool SceneLoadingConcluded { get; set; }
        IInitialSceneState InitialSceneStateInfo { get; }

        SceneShortInfo SceneShortInfo { get; }

        IReadOnlyList<Vector2Int> Parcels { get; }

        ISceneContent SceneContent { get; }

        SceneEntityDefinition SceneEntityDefinition { get; }

        /// <summary>
        ///     Position of the base parcel in the world
        /// </summary>
        ParcelMathHelper.SceneGeometry Geometry { get; }

        /// <summary>
        ///     Main.crdt file that should be applied first before launching the scene
        /// </summary>
        StaticSceneMessages StaticSceneMessages { get; }

        /// <summary>
        ///     Whether this scene was loaded from a wearable which is part of a collection preview.
        /// </summary>
        /// <remarks>
        ///     This has been introduced to fully support the --self-preview-builder-collections command line arg.
        /// </remarks>
        bool IsWearableBuilderCollectionPreview => false;

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

        bool TryGetMediaFileHash(string url, out string fileHash);

        bool IsUrlDomainAllowed(string url);

        bool IsSdk7();

        bool IsPortableExperience();

        /// <summary>
        ///     Gets the specific SDK version (e.g., "7.5.6") from package.json, or null if not available
        /// </summary>
        string GetSDKVersion();

        /// <summary>
        ///     Checks if the scene's SDK version is the specified version or higher
        /// </summary>
        /// <param name="minVersion">Minimum version to check (e.g., "7.5.0")</param>
        /// <returns>True if scene SDK version >= minVersion, false if version unknown or less than minVersion</returns>
        bool IsSDKVersionOrHigher(string minVersion);

        /// <summary>
        ///     Checks if the scene's SDK version matches a specific version
        /// </summary>
        bool IsSDKVersion(string version);

        class Fake : ISceneData
        {
            public bool SceneLoadingConcluded
            {
                get => true;
                set { }
            }

            public IInitialSceneState InitialSceneStateInfo { get; } = new FakeInitialSceneState();
            public SceneShortInfo SceneShortInfo => new (Vector2Int.zero, "Fake");
            public IReadOnlyList<Vector2Int> Parcels { get; } = new List<Vector2Int>();

            public ISceneContent SceneContent => new SceneNonHashedContent(new URLDomain());
            public SceneEntityDefinition SceneEntityDefinition => new (string.Empty, new SceneMetadata());
            public ParcelMathHelper.SceneGeometry Geometry => new (Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes(), 0.0f);
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

            public bool TryGetMediaFileHash(string url, out string fileHash)
            {
                fileHash = string.Empty;
                return false;
            }

            public bool IsUrlDomainAllowed(string url) =>
                false;

            public bool IsSdk7() =>
                true;

            public bool IsPortableExperience() =>
                false;

            public string GetSDKVersion() =>
                null;

            public bool IsSDKVersionOrHigher(string minVersion) =>
                false;

            public bool IsSDKVersion(string version) =>
                false;
        }

        public class FakeInitialSceneState : IInitialSceneState
        {
            public void Dispose()
            {
            }

            public HashSet<string> ISSAssets { get; } = new HashSet<string>();
        }
    }

}
