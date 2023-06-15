using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Text.RegularExpressions;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    /// <summary>
    ///     Prepares URL from the scene ID
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateBefore(typeof(LoadAssetBundleManifestSystem))]
    public partial class PrepareAssetBundleManifestParametersSystem : BaseUnityLoopSystem
    {
        private const string URN_PREFIX = "urn:decentraland:entity:";

        private readonly string assetBundleURL;

        internal PrepareAssetBundleManifestParametersSystem(World world, string assetBundleURL) : base(world)
        {
            this.assetBundleURL = assetBundleURL;
        }

        protected override void Update(float t)
        {
            PrepareParametersQuery(World);
        }

        [Query]
        [None(typeof(LoadingInProgress), typeof(StreamableLoadingResult<SceneAssetBundleManifest>))]
        private void PrepareParameters(ref GetAssetBundleManifestIntention intention)
        {
            string entityId = GetEntityIdFromSceneId(intention.SceneId);
            intention.CommonArguments = new CommonLoadingArguments($"{assetBundleURL}/manifest/{entityId}.json");
        }

        private static string GetEntityIdFromSceneId(string sceneId)
        {
            // This case happens when loading worlds
            if (sceneId.StartsWith(URN_PREFIX))
            {
                sceneId = sceneId.Replace(URN_PREFIX, "");
                sceneId = Regex.Replace(sceneId, "\\?.+", "", RegexOptions.Compiled); // from "?" char onwards we delete everything
            }

            return sceneId;
        }
    }
}
