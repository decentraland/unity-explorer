using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character;
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

        public WriteMainPlayerTransformSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneData sceneData, IExposedTransform exposedTransform, IPartitionComponent scenePartition,
            byte bucketThreshold) : base(world)
        {
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedTransform = exposedTransform;
            this.bucketThreshold = bucketThreshold;
            this.sceneData = sceneData;
            this.scenePartition = scenePartition;
        }

        public override void Initialize()
        {
            // Regardless of dirty state, we need to send the initial transform
            ExposedTransformUtils.Put(ecsToCrdtWriter, exposedTransform, SpecialEntitiesID.PLAYER_ENTITY, sceneData.Geometry.BaseParcelPosition, false);
        }

        protected override void Update(float t)
        {
            if (scenePartition.Bucket > bucketThreshold)
                return;

            ExposedTransformUtils.Put(ecsToCrdtWriter, exposedTransform, SpecialEntitiesID.PLAYER_ENTITY, sceneData.Geometry.BaseParcelPosition, true);
        }
    }
}
