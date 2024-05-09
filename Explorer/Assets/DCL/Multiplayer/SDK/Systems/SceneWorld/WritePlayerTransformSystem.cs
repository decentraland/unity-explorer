using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using Utility;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    // [UpdateBefore(typeof(ResetDirtyFlagSystem<Profile>))]
    // [LogCategory(ReportCategory.PLAYER_AVATAR_BASE)]
    public partial class WritePlayerTransformSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneData sceneData;

        public WritePlayerTransformSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, ISceneData sceneData) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateSDKTransformQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateSDKTransform(ref PlayerCRDTEntity playerCRDTEntity, ref SDKTransform sdkTransform)
        {
            // Main player Transform is handled by 'WriteMainPlayerTransformSystem'
            if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

            // Patch position to be scene-relative
            sdkTransform.Position = ParcelMathHelper.GetSceneRelativePosition(sdkTransform.Position, sceneData.Geometry.BaseParcelPosition);

            ecsToCRDTWriter.PutMessage<SDKTransform, SDKTransform>(static (pbComponent, transform) =>
            {
                pbComponent.Position = transform.Position;
                pbComponent.Rotation = transform.Rotation;
            }, playerCRDTEntity.CRDTEntity, sdkTransform);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref PlayerCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<SDKTransform>(playerCRDTEntity.CRDTEntity);
        }
    }
}
