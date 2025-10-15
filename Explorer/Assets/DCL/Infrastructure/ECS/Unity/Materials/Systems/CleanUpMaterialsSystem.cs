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
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TryRelease(Entity entity, ref MaterialComponent materialComponent)
        {
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
        }

        [Query]
        private void ReleaseUnconditionally(Entity entity, ref MaterialComponent materialComponent)
        {
            ReleaseMaterial.Execute(entity, World, ref materialComponent, destroyMaterial);
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseUnconditionallyQuery(World);
        }
    }
}
