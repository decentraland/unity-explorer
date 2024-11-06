using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
// using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.GltfNode.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Systems;

namespace DCL.SDKComponents.GltfNode.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    // [LogCategory(ReportCategory.LogCategory)]
    public partial class GltfNodeSystem : BaseUnityLoopSystem
    {
        public GltfNodeSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            SetupGltfNodeQuery(World);
        }

        [Query]
        [None(typeof(GltfNodeComponent))]
        private void SetupGltfNode(Entity entity, in PBGltfNode pbComponent)
        {
            UnityEngine.Debug.Log($"PRAVS - SetupGltfNode() - gltfEntity: {pbComponent.GltfContainerEntity} / path: {pbComponent.NodePath}");
        }
    }
}
