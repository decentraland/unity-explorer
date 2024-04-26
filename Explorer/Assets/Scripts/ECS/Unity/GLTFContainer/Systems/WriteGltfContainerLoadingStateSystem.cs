using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
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
    [UpdateAfter(typeof(LoadGltfContainerSystem))]
    public partial class WriteGltfContainerLoadingStateSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteGltfContainerLoadingStateSystem(World world, IECSToCRDTWriter ecsToCRDTWriter)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
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

            var entity = sdkEntity;
            ecsToCRDTWriter.PutMessage<PBGltfContainerLoadingState, LoadingState>(
                //TODO move to static
                (component, loadingState) =>
                {
                    component.CurrentState = loadingState;
                    ReportHub.Log(ReportCategory.CRDT, $"Loading state for entity {entity.Id}: {loadingState}");
                },
                sdkEntity,
                component.State.Value
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
