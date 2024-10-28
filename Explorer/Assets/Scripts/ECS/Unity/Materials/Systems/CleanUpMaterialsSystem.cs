using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Materials.Components;
using System;

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

        public CleanUpMaterialsSystem(World world, DestroyMaterial destroyMaterial) : base(world)
        {
            this.destroyMaterial = destroyMaterial;
        }

        protected override void Update(float t)
        {
            TryReleaseQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TryRelease(ref MaterialComponent materialComponent)
        {
            ReleaseMaterial.Execute(World, ref materialComponent, destroyMaterial);
        }

        [Query]
        private void ReleaseUnconditionally(ref MaterialComponent materialComponent)
        {
            ReleaseMaterial.Execute(World, ref materialComponent, destroyMaterial);
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseUnconditionallyQuery(World);
        }
    }
}
