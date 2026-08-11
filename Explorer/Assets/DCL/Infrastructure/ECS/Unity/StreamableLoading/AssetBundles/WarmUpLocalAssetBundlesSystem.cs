#nullable enable

using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Local-scene development with in-process bundle building: pre-issues an asset-bundle promise for every
    ///     GLTF in the scene content so conversions run eagerly while the scene is still loading. On-demand
    ///     requests for the same hash join the in-flight load through the streamable cache, so nothing converts
    ///     twice. Progress is visible in the scene dev console's "AB Conversion" panel via
    ///     <see cref="AbgenConversionMetrics" />.
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateBefore(typeof(LoadAssetBundleSystem))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class WarmUpLocalAssetBundlesSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ISceneData sceneData;
        private readonly List<AssetPromise<AssetBundleData, GetAssetBundleIntention>> promises = new ();

        private bool started;

        internal WarmUpLocalAssetBundlesSystem(World world, ISceneData sceneData) : base(world)
        {
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            if (!started)
            {
                started = true;
                StartWarmUp();
            }

            PollPromises();
        }

        private void StartWarmUp()
        {
            AssetBundleManifestVersion abManifest = sceneData.SceneEntityDefinition.AssetBundleManifestVersionOrFailed;
            string sceneId = sceneData.SceneEntityDefinition.id ?? string.Empty;

            foreach (ContentDefinition entry in sceneData.SceneEntityDefinition.content)
            {
                if (!entry.file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) && !entry.file.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                    continue;

                GetAssetBundleIntention intention = GetAssetBundleIntention.Create(typeof(GameObject), abManifest.GetCdnRequestHash(entry.hash), entry.file, abManifest, sceneId);

                // MIN_PRIORITY keeps the warmup behind every direct request in the loading budget queue;
                // a direct request for the same hash piggybacks on the warmup load (and vice versa).
                promises.Add(AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, intention, PartitionComponent.MIN_PRIORITY));
            }

            AbgenConversionMetrics.INSTANCE.Reset();
            AbgenConversionMetrics.INSTANCE.OnPlanned(promises.Count);
        }

        private void PollPromises()
        {
            for (int i = promises.Count - 1; i >= 0; i--)
            {
                AssetPromise<AssetBundleData, GetAssetBundleIntention> promise = promises[i];

                if (!promise.TryGetResult(World, out StreamableLoadingResult<AssetBundleData> _))
                {
                    promises[i] = promise;
                    continue;
                }

                // The bundle stays available through the AssetBundleCache; the promise entity is no longer needed.
                promise.Consume(World);
                promises.RemoveAt(i);
            }
        }

        public void FinalizeComponents(in Query query)
        {
            for (var i = 0; i < promises.Count; i++)
                promises[i].ForgetLoading(World);

            promises.Clear();
        }
    }
}
