using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using Utility;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Executes on the scene level to propagate the shared transform data to the SDK Scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WritePlayerTransformSystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     It's sufficient to store one instance only as we are going to override it every time we write the data
        /// </summary>
        private static readonly SDKTransform PLAYER_TRANSFORM_SHARED = new ();

        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly ISceneData sceneData;
        private readonly IExposedTransform exposedTransform;
        private readonly IPartitionComponent scenePartition;
        private readonly byte bucketThreshold;

        public WritePlayerTransformSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneData sceneData, IExposedTransform exposedTransform, IPartitionComponent scenePartition,
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
            PutMessage();
        }

        protected override void Update(float t)
        {
            if (scenePartition.Bucket > bucketThreshold)
                return;

            if (!exposedTransform.Position.IsDirty && !exposedTransform.Rotation.IsDirty)
                return;

            PutMessage();
        }

        private void PutMessage()
        {
            PLAYER_TRANSFORM_SHARED.Position = ParcelMathHelper.GetSceneRelativePosition(exposedTransform.Position.Value, sceneData.Geometry.BaseParcelPosition);
            PLAYER_TRANSFORM_SHARED.Rotation = exposedTransform.Rotation.Value;

            ecsToCrdtWriter.PutMessage(SpecialEntitiesID.PLAYER_ENTITY, PLAYER_TRANSFORM_SHARED);
        }
    }
}
