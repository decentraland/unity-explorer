using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.SceneBoundsChecker;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromRawGltfSystem : BaseUnityLoopSystem
    {
        private const string COLLIDER_SUFFIX = "_collider";
        private const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        internal CreateGltfAssetFromRawGltfSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            ConvertFromGLTFDataQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>))]
        private void ConvertFromGLTFData(in Entity entity,
            ref GetGltfContainerAssetIntention assetIntention,
            ref StreamableLoadingResult<GLTFData> gltfDataResult)
        {
            if (!instantiationFrameTimeBudget.TrySpendBudget() || !memoryBudget.TrySpendBudget())
                return;

            if (assetIntention.CancellationTokenSource.IsCancellationRequested || gltfDataResult.Asset == null)
                return;

            World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(CreateGltfObject(gltfDataResult.Asset)));
        }

        private static GltfContainerAsset CreateGltfObject(GLTFData gltfData)
        {
            var result = GltfContainerAsset.Create(gltfData.MainAsset, gltfData);

            using (PoolExtensions.Scope<List<Renderer>> instanceRenderers = GltfContainerAsset.RENDERERS_POOL.AutoScope())
            {
                gltfData.MainAsset.GetComponentsInChildren(true, instanceRenderers.Value);
                result.Renderers.AddRange(instanceRenderers.Value);
            }

            // Collect all Animations as they are used in Animation System (only for legacy support, as all of them will eventually be converted to Animators)
            using (PoolExtensions.Scope<List<Animation>> animationScope = GltfContainerAsset.ANIMATIONS_POOL.AutoScope())
            {
                gltfData.MainAsset.GetComponentsInChildren(true, animationScope.Value);
                result.Animations.AddRange(animationScope.Value);
            }

            // Collect all Animators as they are used in Animation System
            using (PoolExtensions.Scope<List<Animator>> animatorScope = GltfContainerAsset.ANIMATORS_POOL.AutoScope())
            {
                gltfData.MainAsset.GetComponentsInChildren(true, animatorScope.Value);
                result.Animators.AddRange(animatorScope.Value);
            }

            using (PoolExtensions.Scope<List<MeshFilter>> meshFilterScope = GltfContainerAsset.MESH_FILTERS_POOL.AutoScope())
            {
                List<MeshFilter> list = meshFilterScope.Value;
                gltfData.MainAsset.GetComponentsInChildren(true, list);

                foreach (MeshFilter meshFilter in list)
                {
                    GameObject go = meshFilter.gameObject;

                    // This treatment mimics what's being done in the AB converter
                    if (meshFilter.name.Contains(COLLIDER_SUFFIX, IGNORE_CASE))
                    {
                        MeshCollider newCollider = AddMeshCollider(meshFilter, go);
                        result.InvisibleColliders.Add(new SDKCollider(newCollider));
                    }
                    else
                    {
                        // Note from Alejandro Alvarez Melucci <alejandro.alvarez@decentraland.org>:
                        // I'm not sure why on the AssetBundle flow there's this check,
                        // I introduced it here just in case it's needed. I already reached out to Nico Lorusso to investigate further

                        // Consider it a visible collider when it has a renderer on it
                        if (go.GetComponent<Renderer>())
                            AddVisibleMeshCollider(result.VisibleColliderMeshes, go, meshFilter.sharedMesh);
                        else
                            // Gather invisible colliders
                            CreateAndAddMeshCollider(result.InvisibleColliders, go);
                    }
                }
            }

            // Collect colliders from skinned mesh renderers
            using (PoolExtensions.Scope<List<SkinnedMeshRenderer>> instanceRenderers = GltfContainerAsset.SKINNED_RENDERERS_POOL.AutoScope())
            {
                gltfData.MainAsset.GetComponentsInChildren(true, instanceRenderers.Value);

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in instanceRenderers.Value)
                {
                    GameObject go = skinnedMeshRenderer.gameObject;

                    // Always considered as visible collider
                    AddVisibleMeshCollider(result.VisibleColliderMeshes, go, skinnedMeshRenderer.sharedMesh);
                }
            }

            return result;
        }


        // If we update AddVisibleMeshCollider and/or CreateAndAddMeshCollider please check and update them in CreateGltfAssetFromAssetBundleSystem.cs
        // As a tech-debt we might want to move these functions elsewhere to avoid repetition, but for now it's acceptable since this is only for local development

#region Helper Collider Methods
        private static void AddVisibleMeshCollider(List<GltfContainerAsset.VisibleMeshCollider> result, GameObject go, Mesh mesh)
        {
            result.Add(new GltfContainerAsset.VisibleMeshCollider
            {
                GameObject = go,
                Mesh = mesh,
            });
        }

        private static void CreateAndAddMeshCollider(List<SDKCollider> result, GameObject go)
        {
            // Asset Bundle converter creates Colliders during the processing in some cases
            Collider collider = go.GetComponent<Collider>();

            if (collider)
            {
                // Disable it as its activity controlled by another system based on PBGltfContainer component
                collider.enabled = false;

                result.Add(new SDKCollider(collider));
            }
        }

        private static MeshCollider AddMeshCollider(MeshFilter meshFilter, GameObject go)
        {
            Physics.BakeMesh(meshFilter.sharedMesh.GetInstanceID(), false);
            MeshCollider newCollider = go.AddComponent<MeshCollider>();
            var renderer = go.GetComponent<MeshRenderer>();

            if (renderer)
                renderer.enabled = false;

            UnityObjectUtils.SafeDestroy(meshFilter);
            return newCollider;
        }
#endregion
    }
}
