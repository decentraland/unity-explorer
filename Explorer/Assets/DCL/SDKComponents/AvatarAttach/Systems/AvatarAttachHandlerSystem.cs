using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [LogCategory(ReportCategory.AVATAR_ATTACH)]
    [ThrottlingEnabled]
    public partial class AvatarAttachHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly QueryDescription ENTITY_DESTRUCTION_QUERY = new QueryDescription().WithAll<DeleteEntityIntention, AvatarAttachComponent>();
        private static readonly QueryDescription COMPONENT_REMOVAL_QUERY = new QueryDescription().WithAll<AvatarAttachComponent>().WithNone<DeleteEntityIntention, PBAvatarAttach>();
        private static AvatarBase mainPlayerAvatarBase;

        public AvatarAttachHandlerSystem(World world, WorldProxy globalWorld) : base(world)
        {
            if (mainPlayerAvatarBase == null)
                mainPlayerAvatarBase = globalWorld.GetWorld().Get<AvatarBase>(globalWorld.GetMainPlayerEntity().Value);
        }

        protected override void Update(float t)
        {
            SetupAvatarAttachQuery(World);
            UpdateAvatarAttachTransformQuery(World);

            World.Remove<AvatarAttachComponent>(COMPONENT_REMOVAL_QUERY);
            World.Remove<AvatarAttachComponent, PBAvatarAttach>(ENTITY_DESTRUCTION_QUERY);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void SetupAvatarAttach(in Entity entity, ref TransformComponent transformComponent, ref PBAvatarAttach pbAvatarAttach)
        {
            var component = new AvatarAttachComponent
            {
                anchorPointTransform = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId),
            };

            ApplyAnchorPointTransformValues(transformComponent, component);
            transformComponent.UpdateCache();

            World.Add(entity, component);
        }

        [Query]
        private void UpdateAvatarAttachTransform(ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent, ref TransformComponent transformComponent)
        {
            if (pbAvatarAttach.IsDirty)
                avatarAttachComponent.anchorPointTransform = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId);

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

        private Transform GetAnchorPointTransform(AvatarAnchorPointType anchorPointType)
        {
            switch (anchorPointType)
            {
                case AvatarAnchorPointType.AaptLeftHand:
                    return mainPlayerAvatarBase.LeftHandAnchorPoint;
                case AvatarAnchorPointType.AaptRightHand:
                    return mainPlayerAvatarBase.RightHandAnchorPoint;
                case AvatarAnchorPointType.AaptNameTag:
                default: // AvatarAnchorPointType.AaptPosition
                    return mainPlayerAvatarBase.transform;
            }
        }

        private bool ApplyAnchorPointTransformValues(TransformComponent targetTransform, AvatarAttachComponent avatarAttachComponent)
        {
            Vector3 anchorPointPosition = avatarAttachComponent.anchorPointTransform.position;
            Quaternion anchorPointRotation = avatarAttachComponent.anchorPointTransform.rotation;
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
