using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Materials.Components;
using ECS.Unity.Textures.Components;
using System;
using UnityEngine;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Dereferences materials on the dying entities
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MATERIALS)]
    public partial class CleanUpMaterialsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly DestroyMaterial destroyMaterial;
        private readonly IExtendedObjectPool<Texture2D> videoTexturesPool;

        public CleanUpMaterialsSystem(World world, DestroyMaterial destroyMaterial, IExtendedObjectPool<Texture2D> videoTexturesPool) : base(world)
        {
            this.destroyMaterial = destroyMaterial;
            this.videoTexturesPool = videoTexturesPool;
        }

        protected override void Update(float t)
        {
            TryReleaseQuery(World);
            TryReleaseConsumerQuery(World);
            HandleTextureWithoutConsumersQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TryRelease(Entity entity, ref MaterialComponent materialComponent)
        {
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
        }

        /// <summary>
        /// Release of VideoTextureConsumer component should be in this scope because it is a part of the material system
        /// StartMaterialsLoadingSystem -> CleanUpMaterialsSystem
        /// </summary>
        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TryReleaseConsumer(Entity entity, ref VideoTextureConsumer textureConsumer)
        {
            CleanUpVideoTexture(ref textureConsumer);
            World.Remove<VideoTextureConsumer>(entity);
        }

        /// <summary>
        ///     Prevents CPU and memory leaks by cleaning up video textures and media players that are not being used anymore.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleTextureWithoutConsumers(Entity entity, ref VideoTextureConsumer textureConsumer)
        {
            if (textureConsumer.ConsumersCount == 0)
            {
                CleanUpVideoTexture(ref textureConsumer);
                World.Remove<VideoTextureConsumer>(entity);
            }
        }

        private void CleanUpVideoTexture(ref VideoTextureConsumer videoTextureConsumer)
        {
            videoTexturesPool.Release(videoTextureConsumer.Texture);
            videoTextureConsumer.Dispose();
        }

        [Query]
        private void ReleaseUnconditionally(Entity entity, ref MaterialComponent materialComponent)
        {
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
        }

        [Query]
        private void FinalizeVideoTextureConsumerComponent(ref VideoTextureConsumer component) =>
            CleanUpVideoTexture(ref component);

        public void FinalizeComponents(in Query query)
        {
            ReleaseUnconditionallyQuery(World);
            FinalizeVideoTextureConsumerComponentQuery(World);
        }
    }
}
