using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.PrimitiveRenderer.Components;

namespace ECS.Unity.Visibility.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class VisibilitySystem : BaseUnityLoopSystem
    {
        public VisibilitySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateVisibilityQuery(World);
        }

        [Query]
        private void UpdateVisibility(ref PBVisibilityComponent visibilityComponent,
            ref PBMeshRenderer meshRendererComponent, ref PrimitiveMeshRendererComponent primitiveMeshRendererComponent)
        {
            if (!meshRendererComponent.IsDirty && !visibilityComponent.IsDirty)
                return;

            primitiveMeshRendererComponent.MeshRenderer.enabled = visibilityComponent.Visible;
        }
    }
}
