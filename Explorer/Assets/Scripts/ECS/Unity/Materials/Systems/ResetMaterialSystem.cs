﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
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
    [ThrottlingEnabled]
    public partial class ResetMaterialSystem : BaseUnityLoopSystem
    {
        private readonly DestroyMaterial destroyMaterial;

        internal ResetMaterialSystem(World world, DestroyMaterial destroyMaterial) : base(world)
        {
            this.destroyMaterial = destroyMaterial;
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
            ReleaseMaterial.Execute(World, ref materialComponent, destroyMaterial);
        }
    }
}
