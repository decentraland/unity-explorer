using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.AVATAR_ATTACH)]
    public partial class AvatarAttachHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription ENTITY_DESTRUCTION_QUERY = new QueryDescription().WithAll<DeleteEntityIntention, AvatarAttachComponent>();
        private static readonly QueryDescription COMPONENT_REMOVAL_QUERY = new QueryDescription().WithAll<AvatarAttachComponent>().WithNone<DeleteEntityIntention, PBAvatarAttach>();
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ISceneStateProvider sceneStateProvider;

        public AvatarAttachHandlerSystem(World world, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBaseProxy.Configured) return;

            SetupAvatarAttachQuery(World);
            UpdateAvatarAttachTransformQuery(World);

            World.Remove<AvatarAttachComponent>(COMPONENT_REMOVAL_QUERY);
            World.Remove<AvatarAttachComponent, PBAvatarAttach>(ENTITY_DESTRUCTION_QUERY);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void SetupAvatarAttach(in Entity entity, ref TransformComponent transformComponent, ref PBAvatarAttach pbAvatarAttach)
        {
            if (!sceneStateProvider.IsCurrent) return;

            AvatarAttachComponent component = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId);

            ApplyAnchorPointTransformValues(transformComponent, component);
            transformComponent.UpdateCache();

            World.Add(entity, component);
        }

        [Query]
        private void UpdateAvatarAttachTransform(ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent, ref TransformComponent transformComponent)
        {
            if (!sceneStateProvider.IsCurrent) return;

            if (pbAvatarAttach.IsDirty)
                avatarAttachComponent = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId);

            if (ApplyAnchorPointTransformValues(transformComponent, avatarAttachComponent))
                transformComponent.UpdateCache();
        }

        [Query]
        [All(typeof(AvatarAttachComponent))]
        private void FinalizeComponents(in Entity entity)
        {
            World.Remove<AvatarAttachComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        private AvatarAttachComponent GetAnchorPointTransform(AvatarAnchorPointType anchorPointType)
{
    const float OLD_CLIENT_PIVOT_CORRECTION = -0.75f;

    switch (anchorPointType)
    {
        case AvatarAnchorPointType.AaptPosition:
            return new AvatarAttachComponent(mainPlayerAvatarBaseProxy.Object!.transform, OLD_CLIENT_PIVOT_CORRECTION);
        case AvatarAnchorPointType.AaptNameTag:
            return mainPlayerAvatarBaseProxy.Object!.NameTagAnchorPoint;
        case AvatarAnchorPointType.AaptHead:
            return mainPlayerAvatarBaseProxy.Object!.HeadAnchorPoint;
        case AvatarAnchorPointType.AaptNeck:
            return mainPlayerAvatarBaseProxy.Object!.NeckAnchorPoint;
        case AvatarAnchorPointType.AaptSpine:
            return mainPlayerAvatarBaseProxy.Object!.SpineAnchorPoint;
        case AvatarAnchorPointType.AaptSpine1:
            return mainPlayerAvatarBaseProxy.Object!.Spine1AnchorPoint;
        case AvatarAnchorPointType.AaptSpine2:
            return mainPlayerAvatarBaseProxy.Object!.Spine2AnchorPoint;
        case AvatarAnchorPointType.AaptHip:
            return mainPlayerAvatarBaseProxy.Object!.HipAnchorPoint;
        case AvatarAnchorPointType.AaptLeftShoulder:
            return mainPlayerAvatarBaseProxy.Object!.LeftShoulderAnchorPoint;
        case AvatarAnchorPointType.AaptLeftArm:
            return mainPlayerAvatarBaseProxy.Object!.LeftArmAnchorPoint;
        case AvatarAnchorPointType.AaptLeftForearm:
            return mainPlayerAvatarBaseProxy.Object!.LeftForearmAnchorPoint;
        case AvatarAnchorPointType.AaptLeftHand:
            return mainPlayerAvatarBaseProxy.Object!.LeftHandAnchorPoint;
        case AvatarAnchorPointType.AaptLeftHandIndex:
            return mainPlayerAvatarBaseProxy.Object!.LeftHandIndexAnchorPoint;
        case AvatarAnchorPointType.AaptRightShoulder:
            return mainPlayerAvatarBaseProxy.Object!.RightShoulderAnchorPoint;
        case AvatarAnchorPointType.AaptRightArm:
            return mainPlayerAvatarBaseProxy.Object!.RightArmAnchorPoint;
        case AvatarAnchorPointType.AaptRightForearm:
            return mainPlayerAvatarBaseProxy.Object!.RightForearmAnchorPoint;
        case AvatarAnchorPointType.AaptRightHand:
            return mainPlayerAvatarBaseProxy.Object!.RightHandAnchorPoint;
        case AvatarAnchorPointType.AaptRightHandIndex:
            return mainPlayerAvatarBaseProxy.Object!.RightHandIndexAnchorPoint;
        case AvatarAnchorPointType.AaptLeftUpLeg:
            return mainPlayerAvatarBaseProxy.Object!.LeftUpLegAnchorPoint;
        case AvatarAnchorPointType.AaptLeftLeg:
            return mainPlayerAvatarBaseProxy.Object!.LeftLegAnchorPoint;
        case AvatarAnchorPointType.AaptLeftFoot:
            return mainPlayerAvatarBaseProxy.Object!.LeftFootAnchorPoint;
        case AvatarAnchorPointType.AaptLeftToeBase:
            return mainPlayerAvatarBaseProxy.Object!.LeftToeBaseAnchorPoint;
        case AvatarAnchorPointType.AaptRightUpLeg:
            return mainPlayerAvatarBaseProxy.Object!.RightUpLegAnchorPoint;
        case AvatarAnchorPointType.AaptRightLeg:
            return mainPlayerAvatarBaseProxy.Object!.RightLegAnchorPoint;
        case AvatarAnchorPointType.AaptRightFoot:
            return mainPlayerAvatarBaseProxy.Object!.RightFootAnchorPoint;
        case AvatarAnchorPointType.AaptRightToeBase:
            return mainPlayerAvatarBaseProxy.Object!.RightToeBaseAnchorPoint;
        default:
            throw new ArgumentOutOfRangeException(nameof(anchorPointType), anchorPointType, "Unknown anchor point type");
    }
}

        private bool ApplyAnchorPointTransformValues(TransformComponent targetTransform, AvatarAttachComponent avatarAttachComponent)
        {
            Vector3 anchorPointPosition = avatarAttachComponent.AnchorPointTransform.position + (avatarAttachComponent.PivotCorrection * Vector3.up);
            Quaternion anchorPointRotation = avatarAttachComponent.AnchorPointTransform.rotation;
            var modifiedComponent = false;

            if (anchorPointPosition != targetTransform.Cached.WorldPosition)
            {
                targetTransform.Transform.position = anchorPointPosition;
                modifiedComponent = true;
            }

            if (anchorPointRotation != targetTransform.Cached.WorldRotation)
            {
                targetTransform.Transform.rotation = anchorPointRotation;
                modifiedComponent = true;
            }

            return modifiedComponent;
        }
    }
}
