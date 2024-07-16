using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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

            SetupAvatarAttachQuery(World!);
            UpdateAvatarAttachTransformQuery(World!);

            World!.Remove<AvatarAttachComponent>(COMPONENT_REMOVAL_QUERY);
            World.Remove<AvatarAttachComponent, PBAvatarAttach>(ENTITY_DESTRUCTION_QUERY);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void SetupAvatarAttach(in Entity entity, ref TransformComponent transformComponent, ref PBAvatarAttach pbAvatarAttach)
        {
            if (!sceneStateProvider.IsCurrent) return;

            var component = new AvatarAttachComponent(
                GetAnchorPointTransform(pbAvatarAttach.AnchorPointId),
                pbAvatarAttach.AnchorPointId
            );

            ApplyAnchorPointTransformValues(transformComponent, component);
            transformComponent.UpdateCache();

            World!.Add(entity, component);
        }

        [Query]
        private void UpdateAvatarAttachTransform(ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent, ref TransformComponent transformComponent)
        {
            if (!sceneStateProvider.IsCurrent) return;

            if (pbAvatarAttach.IsDirty)
                avatarAttachComponent.Update(GetAnchorPointTransform(pbAvatarAttach.AnchorPointId), pbAvatarAttach.AnchorPointId);

            if (ApplyAnchorPointTransformValues(transformComponent, avatarAttachComponent))
                transformComponent.UpdateCache();
        }

        [Query]
        [All(typeof(AvatarAttachComponent))]
        private void FinalizeComponents(in Entity entity)
        {
            World!.Remove<AvatarAttachComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World!);
        }

        private Transform GetAnchorPointTransform(AvatarAnchorPointType anchorPointType) =>
            anchorPointType switch
            {
                AvatarAnchorPointType.AaptPosition => mainPlayerAvatarBaseProxy.Object!.transform,
                AvatarAnchorPointType.AaptNameTag => mainPlayerAvatarBaseProxy.Object!.NameTagAnchorPoint,
                AvatarAnchorPointType.AaptHead => mainPlayerAvatarBaseProxy.Object!.HeadAnchorPoint,
                AvatarAnchorPointType.AaptNeck => mainPlayerAvatarBaseProxy.Object!.NeckAnchorPoint,
                AvatarAnchorPointType.AaptSpine => mainPlayerAvatarBaseProxy.Object!.SpineAnchorPoint,
                AvatarAnchorPointType.AaptSpine1 => mainPlayerAvatarBaseProxy.Object!.Spine1AnchorPoint,
                AvatarAnchorPointType.AaptSpine2 => mainPlayerAvatarBaseProxy.Object!.Spine2AnchorPoint,
                AvatarAnchorPointType.AaptHip => mainPlayerAvatarBaseProxy.Object!.HipAnchorPoint,
                AvatarAnchorPointType.AaptLeftShoulder => mainPlayerAvatarBaseProxy.Object!.LeftShoulderAnchorPoint,
                AvatarAnchorPointType.AaptLeftArm => mainPlayerAvatarBaseProxy.Object!.LeftArmAnchorPoint,
                AvatarAnchorPointType.AaptLeftForearm => mainPlayerAvatarBaseProxy.Object!.LeftForearmAnchorPoint,
                AvatarAnchorPointType.AaptLeftHand => mainPlayerAvatarBaseProxy.Object!.LeftHandAnchorPoint,
                AvatarAnchorPointType.AaptLeftHandIndex => mainPlayerAvatarBaseProxy.Object!.LeftHandIndexAnchorPoint,
                AvatarAnchorPointType.AaptRightShoulder => mainPlayerAvatarBaseProxy.Object!.RightShoulderAnchorPoint,
                AvatarAnchorPointType.AaptRightArm => mainPlayerAvatarBaseProxy.Object!.RightArmAnchorPoint,
                AvatarAnchorPointType.AaptRightForearm => mainPlayerAvatarBaseProxy.Object!.RightForearmAnchorPoint,
                AvatarAnchorPointType.AaptRightHand => mainPlayerAvatarBaseProxy.Object!.RightHandAnchorPoint,
                AvatarAnchorPointType.AaptRightHandIndex => mainPlayerAvatarBaseProxy.Object!.RightHandIndexAnchorPoint,
                AvatarAnchorPointType.AaptLeftUpLeg => mainPlayerAvatarBaseProxy.Object!.LeftUpLegAnchorPoint,
                AvatarAnchorPointType.AaptLeftLeg => mainPlayerAvatarBaseProxy.Object!.LeftLegAnchorPoint,
                AvatarAnchorPointType.AaptLeftFoot => mainPlayerAvatarBaseProxy.Object!.LeftFootAnchorPoint,
                AvatarAnchorPointType.AaptLeftToeBase => mainPlayerAvatarBaseProxy.Object!.LeftToeBaseAnchorPoint,
                AvatarAnchorPointType.AaptRightUpLeg => mainPlayerAvatarBaseProxy.Object!.RightUpLegAnchorPoint,
                AvatarAnchorPointType.AaptRightLeg => mainPlayerAvatarBaseProxy.Object!.RightLegAnchorPoint,
                AvatarAnchorPointType.AaptRightFoot => mainPlayerAvatarBaseProxy.Object!.RightFootAnchorPoint,
                AvatarAnchorPointType.AaptRightToeBase => mainPlayerAvatarBaseProxy.Object!.RightToeBaseAnchorPoint,
                _ => throw new ArgumentOutOfRangeException(nameof(anchorPointType), anchorPointType, "Unknown anchor point type")
            };

        private static bool ApplyAnchorPointTransformValues(TransformComponent targetTransform, AvatarAttachComponent avatarAttachComponent)
        {
            Vector3 anchorPointPosition = avatarAttachComponent.AnchorPointTransform!.position;
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
