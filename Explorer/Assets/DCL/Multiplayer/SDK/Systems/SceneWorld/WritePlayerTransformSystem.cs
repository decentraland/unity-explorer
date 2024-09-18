using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
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
        private void UpdateSDKTransform(in PlayerSceneCRDTEntity playerCRDTEntity, ref SDKTransform sdkTransform)
        {
            if (!sdkTransform.IsDirty) return;

            // Main player Transform is handled by 'WriteMainPlayerTransformSystem'
            if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

            // Patches position to be scene-relative before sending it through CRDT
            ExposedTransformUtils.Put(
                ecsToCRDTWriter,
                sdkTransform,
                playerCRDTEntity.CRDTEntity,
                sceneData.Geometry.BaseParcelPosition,
                false);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<SDKTransform>(playerCRDTEntity.CRDTEntity);
        }
    }
}
