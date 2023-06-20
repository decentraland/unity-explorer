using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    /// <summary>
    ///     Prepares URL from the scene ID
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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
            ReadOnlySpan<char> sceneId = intention.SceneId.AsSpan();
            GetEntityIdFromSceneId(ref sceneId);
            intention.CommonArguments = new CommonLoadingArguments($"{assetBundleURL}manifest/{sceneId.ToString()}.json");
        }

        private static void GetEntityIdFromSceneId(ref ReadOnlySpan<char> sceneId)
        {
            // This case happens when loading worlds
            if (sceneId.StartsWith(URN_PREFIX))
            {
                sceneId = sceneId[URN_PREFIX.Length..];
                int indexOfQuestionMark = sceneId.LastIndexOf('?');

                if (indexOfQuestionMark > -1)
                {
                    // from "?" char onwards we delete everything
                    sceneId = sceneId[..indexOfQuestionMark];
                }
            }
        }
    }
}
