using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using Utility;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class PlayerTransformPropagationSystem : BaseUnityLoopSystem
    {
        public PlayerTransformPropagationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            PropagateTransformToSceneQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateTransformToScene(in CharacterTransform characterTransform, in PlayerCRDTEntity playerCRDTEntity)
        {
            if (!characterTransform.Transform.hasChanged) return;

            // Main player Transform is handled by 'WriteMainPlayerTransformSystem'
            if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

            World sceneEcsWorld = playerCRDTEntity.SceneFacade.EcsExecutor.World;

            // Position is updated to scene-relative on the writer system
            if (sceneEcsWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out SDKTransform? sdkTransform))
            {
                sdkTransform!.Position.Value = characterTransform.Transform.position;
                sdkTransform.Rotation.Value = characterTransform.Transform.rotation;
                sdkTransform.IsDirty = true;
                sceneEcsWorld.Set(playerCRDTEntity.SceneWorldEntity, sdkTransform);
                return;
            }

            sdkTransform = new SDKTransform
            {
                Position = new CanBeDirty<Vector3>(characterTransform.Transform.position),
                Rotation = new CanBeDirty<Quaternion>(characterTransform.Transform.rotation),
                IsDirty = true
            };
            sceneEcsWorld.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform);
        }
    }
}
