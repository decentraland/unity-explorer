using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Materials.Components;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Dereferences materials on the dying entities
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.MATERIALS)]
    public partial class CleanUpMaterialsSystem : BaseUnityLoopSystem
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
    }
}
