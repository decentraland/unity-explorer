using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class WritePointerLockStateSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly IExposedCameraData exposedCameraData;
        private readonly IPartitionComponent scenePartition;
        private readonly byte propagationThreshold;

        public WritePointerLockStateSystem(World world,
            IECSToCRDTWriter ecsToCrdtWriter,
            IExposedCameraData exposedCameraData,
            IPartitionComponent scenePartition,
            byte propagationThreshold) : base(world)
        {
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedCameraData = exposedCameraData;
            this.scenePartition = scenePartition;
            this.propagationThreshold = propagationThreshold;
        }

        protected override void Update(float t)
        {
            if (scenePartition.Bucket > propagationThreshold)
                return;

            PropagateStateQuery(World);
        }

        [Query]
        [All(typeof(PBPointerLock))]
        private void PropagateState(ref CRDTEntity crdtEntity)
        {
            ecsToCrdtWriter.PutMessage<PBPointerLock, IExposedCameraData>(static (pointerLock, data) =>
                    pointerLock.IsPointerLocked = data.PointerIsLocked,
                crdtEntity, exposedCameraData);
        }
    }
}
