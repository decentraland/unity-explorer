using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using Utility.Primitives;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Resets material to the default one if the component was deleted
    /// </summary>
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateBefore(typeof(ApplyMaterialSystem))]
    public class ResetMaterialSystem : BaseUnityLoopSystem
    {
        private readonly IMaterialsCache materialsCache;

        internal ResetMaterialSystem(World world, IMaterialsCache materialsCache) : base(world)
        {
            this.materialsCache = materialsCache;
        }

        protected override void Update(float t)
        {
            ResetQuery(World);
            World.Remove<PrimitiveMeshRendererComponent>(in Reset_QueryDescription);
        }

        [Query]
        [None(typeof(PBMaterial))]
        private void Reset(ref PrimitiveMeshRendererComponent meshRendererComponent, ref MaterialComponent materialComponent)
        {
            meshRendererComponent.MeshRenderer.sharedMaterial = DefaultMaterial.Shared;
            ReleaseMaterial.Execute(World, ref materialComponent, materialsCache);
        }
    }
}
