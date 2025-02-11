using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.Character.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Transforms.Systems;

namespace ECS.Unity.AvatarShape.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class UpdateAvatarShapeTargetPositionSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;

        private UpdateAvatarShapeTargetPositionSystem(World world, World globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            UpdateAvatarShapeLastPositionQuery(World);
        }

        [Query]
        private void UpdateAvatarShapeLastPosition(ref SDKAvatarShapeComponent sdkAvatarShapeComponent, ref SDKTransform sdkTransform)
        {
            if (!sdkTransform.IsDirty)
                return;

            ref CharacterTargetPosition characterTargetPosition = ref globalWorld.TryGetRef<CharacterTargetPosition>(
                sdkAvatarShapeComponent.globalWorldEntity,
                out bool hasCharacterLastPosition);

            if (!hasCharacterLastPosition)
                return;

            characterTargetPosition.TargetPosition = sdkTransform.Position;
            characterTargetPosition.FinalRotation = sdkTransform.Rotation;
        }
    }
}
