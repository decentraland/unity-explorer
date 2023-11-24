using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;
using Utility.Pool;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Utility to write Gltf Container Loading State to CRDT (propagate it back to the scene)
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(LoadGltfContainerSystem))]
    public partial class WriteGltfContainerLoadingStateSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBGltfContainerLoadingState> componentPool;

        public WriteGltfContainerLoadingStateSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<PBGltfContainerLoadingState> componentPool)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.componentPool = componentPool;
        }

        protected override void Update(float t)
        {
            ExecuteQuery(World);
            RemoveQuery(World);
        }

        [Query]
        [All(typeof(PBGltfContainer))]
        private void Execute(ref CRDTEntity sdkEntity, ref GltfContainerComponent component)
        {
            if (!component.State.ChangedThisFrame())
                return;

            using PoolExtensions.Scope<PBGltfContainerLoadingState> scope = componentPool.AutoScope();
            PBGltfContainerLoadingState sdkComponent = scope.Value;
            sdkComponent.CurrentState = component.State;
            ecsToCRDTWriter.PutMessage(sdkEntity, sdkComponent);
        }

        [Query]
        private void Remove(ref CRDTEntity sdkEntity, ref RemovedComponents removedComponents)
        {
            if (removedComponents.Remove<PBGltfContainer>())
                ecsToCRDTWriter.DeleteMessage<PBGltfContainerLoadingState>(sdkEntity);
        }
    }
}
