using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
    public partial class CleanUpMaterialsSystem : BaseUnityLoopSystem
    {
        private readonly IMaterialsCache materialsCache;

        internal CleanUpMaterialsSystem(World world, IMaterialsCache materialsCache) : base(world)
        {
            this.materialsCache = materialsCache;
        }

        protected override void Update(float t)
        {
            TryReleaseQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void TryRelease(ref MaterialComponent materialComponent)
        {
            ReleaseMaterial.Execute(World, ref materialComponent, materialsCache);
        }
    }
}
