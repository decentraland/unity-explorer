using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.Unity.Transforms;
using SceneRunner.Scene;
using Utility;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Executes on the scene level to propagate the shared transform data to the SDK Scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WriteMainPlayerTransformSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly ISceneData sceneData;
        private readonly IExposedTransform exposedTransform;
        private readonly IPartitionComponent scenePartition;
        private readonly byte bucketThreshold;
        private IComponentPool<SDKTransform> sdkTransformPool;
        private readonly Entity playerEntity;

        internal WriteMainPlayerTransformSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneData sceneData, IExposedTransform exposedTransform, IPartitionComponent scenePartition,
            byte bucketThreshold, IComponentPool<SDKTransform> sdkTransformPool, Entity playerEntity) : base(world)
        {
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedTransform = exposedTransform;
            this.bucketThreshold = bucketThreshold;
            this.playerEntity = playerEntity;
            this.sdkTransformPool = sdkTransformPool;
            this.sceneData = sceneData;
            this.scenePartition = scenePartition;
        }

        public override void Initialize()
        {
            // If SDKTransform is not added to the entity by the scene before calling this logic,
            // CRDTBridge will assume it was already added as it's written by IECSToCRDTWriter

            // Regardless of dirty state, we need to send the initial transform
            var sdkTransform = ExposedTransformUtils.Put(ecsToCrdtWriter, exposedTransform, SpecialEntitiesID.PLAYER_ENTITY, sceneData.Geometry.BaseParcelPosition, false)
                                                    .EnsureNotNull();

            if (!World.Has<SDKTransform>(playerEntity))
            {
                var newComponent = sdkTransformPool.Get();
                // Copy all fields
                newComponent.Position = sdkTransform.Position;
                newComponent.Rotation = sdkTransform.Rotation;
                newComponent.Scale = sdkTransform.Scale;

                World.Add(playerEntity, newComponent);
            }
        }

        protected override void Update(float t)
        {
            if (scenePartition.Bucket > bucketThreshold)
                return;

            ExposedTransformUtils.Put(ecsToCrdtWriter, exposedTransform, SpecialEntitiesID.PLAYER_ENTITY, sceneData.Geometry.BaseParcelPosition, true);
        }
    }
}
