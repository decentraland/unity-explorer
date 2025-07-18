﻿using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using System;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Cancel promises on the dying entities
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CleanUpGltfContainerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription ENTITY_DESTROY_QUERY = new QueryDescription()
           .WithAll<DeleteEntityIntention, GltfContainerComponent>();

        private ReleaseOnEntityDestroy releaseOnEntityDestroy;

        internal CleanUpGltfContainerSystem(World world, IGltfContainerAssetsCache cache, IEntityCollidersSceneCache entityCollidersSceneCache) : base(world)
        {
            releaseOnEntityDestroy = new ReleaseOnEntityDestroy(cache, entityCollidersSceneCache, World);
        }

        protected override void Update(float t)
        {
            World.InlineEntityQuery<ReleaseOnEntityDestroy, GltfContainerComponent>(in ENTITY_DESTROY_QUERY, ref releaseOnEntityDestroy);
        }

        public void FinalizeComponents(in Query query)
        {
            World.InlineEntityQuery<ReleaseOnEntityDestroy, GltfContainerComponent>(in new QueryDescription().WithAll<GltfContainerComponent>(), ref releaseOnEntityDestroy);
        }

        private readonly struct ReleaseOnEntityDestroy : IForEachWithEntity<GltfContainerComponent>
        {
            private readonly IEntityCollidersSceneCache entityCollidersSceneCache;
            private readonly IGltfContainerAssetsCache cache;
            private readonly World world;

            public ReleaseOnEntityDestroy(IGltfContainerAssetsCache cache, IEntityCollidersSceneCache entityCollidersSceneCache, World world)
            {
                this.cache = cache;
                this.world = world;
                this.entityCollidersSceneCache = entityCollidersSceneCache;
            }

            public void Update(Entity entity, ref GltfContainerComponent component)
            {
                if (component.Promise.TryGetResult(world, out StreamableLoadingResult<GltfContainerAsset> result) && result.Succeeded)
                {
                    component.ResetOriginalMaterials();

                    cache.Dereference(component.Hash, result.Asset);
                    entityCollidersSceneCache.Remove(result.Asset);

                    // Since NoCache is used for Raw GLTFs, we have to manually dispose of the Data
                    if (result.Asset.AssetData is GLTFData)
                        result.Asset.Dispose();
                }

                if (world.Has<GltfNodeModifiers>(entity))
                    world.Add(entity, new GltfNodeModifiersCleanupIntention());

                // Clear the root GameObject reference
                component.RootGameObject = null;
                component.Promise.ForgetLoading(world);
            }
        }
    }
}
