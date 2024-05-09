using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Utility to write Gltf Container Loading State to CRDT (propagate it back to the scene)
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(FinalizeGltfContainerLoadingSystem))]
    public partial class WriteGltfContainerLoadingStateSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        private readonly EntityEventBuffer<GltfContainerComponent> changedGltfs;
        private readonly EntityEventBuffer<GltfContainerComponent>.ForEachDelegate eventHandler;

        public WriteGltfContainerLoadingStateSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, EntityEventBuffer<GltfContainerComponent> changedGltfs)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.changedGltfs = changedGltfs;
            eventHandler = PropagateChangedState;
        }

        protected override void Update(float t)
        {
            changedGltfs.ForEach(eventHandler);

            RemoveQuery(World);
        }

        private void PropagateChangedState(Entity entity, GltfContainerComponent component)
        {
            if (!World.TryGet(entity, out CRDTEntity sdkEntity)) return;

            ecsToCRDTWriter.PutMessage<PBGltfContainerLoadingState, LoadingState>(
                static (component, loadingState) => component.CurrentState = loadingState,
                sdkEntity,
                component.State
            );
        }

        [Query]
        private void Remove(ref CRDTEntity sdkEntity, ref RemovedComponents removedComponents)
        {
            if (removedComponents.Remove<PBGltfContainer>())
                ecsToCRDTWriter.DeleteMessage<PBGltfContainerLoadingState>(sdkEntity);
        }
    }
}
