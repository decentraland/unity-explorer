using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateTransformSystem))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    [LogCategory(ReportCategory.SDK_COMPONENT)]
    [ThrottlingEnabled]
    public partial class AvatarAttachHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private AvatarBase ownPlayerAvatarBase;

        public AvatarAttachHandlerSystem(World world, WorldProxy globalWorld) : base(world)
        {
            globalWorld.Query(new QueryDescription().WithAll<Profile, AvatarBase>(), (ref Profile profile, ref AvatarBase avatarBase) =>
            {
                if (profile.UserId == "fakeOwnUserId") // TODO: improve
                    ownPlayerAvatarBase = avatarBase;
            });
        }

        protected override void Update(float t)
        {
            SetupAvatarAttachQuery(World);
            UpdateAvatarAttachTransformQuery(World);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void SetupAvatarAttach(in Entity entity, ref TransformComponent transformComponent, ref PBAvatarAttach pbAvatarAttach)
        {
            var component = new AvatarAttachComponent();

            component.anchorPointTransform = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId);

            ApplyAnchorPointTransformValues(transformComponent.Transform, component);

            World.Add(entity, component);
        }

        [Query]
        private void UpdateAvatarAttachTransform(in Entity entity, ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent, ref TransformComponent transformComponent)
        {
            if (pbAvatarAttach.IsDirty) { avatarAttachComponent.anchorPointTransform = GetAnchorPointTransform(pbAvatarAttach.AnchorPointId); }

            if (ApplyAnchorPointTransformValues(transformComponent.Transform, avatarAttachComponent))
                World.Set(entity, avatarAttachComponent);
        }

        [Query]
        [All(typeof(AvatarAttachComponent))]
        [None(typeof(PBAvatarAttach), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity)
        {
            World.Remove<AvatarAttachComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(AvatarAttachComponent))]
        private void HandleEntityDestruction(in Entity entity)
        {
            World.Remove<AvatarAttachComponent>(entity);
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
                    return ownPlayerAvatarBase.LeftHandAnchorPoint;
                case AvatarAnchorPointType.AaptRightHand:
                    return ownPlayerAvatarBase.RightHandAnchorPoint;
                case AvatarAnchorPointType.AaptNameTag:
                default: // AvatarAnchorPointType.AaptPosition
                    return ownPlayerAvatarBase.transform;
            }
        }

        private bool ApplyAnchorPointTransformValues(Transform targetTransform, AvatarAttachComponent avatarAttachComponent)
        {
            Vector3 anchorPointPosition = avatarAttachComponent.anchorPointTransform.position;
            Quaternion anchorPointRotation = avatarAttachComponent.anchorPointTransform.rotation;
            var modifiedComponent = false;

            if (anchorPointPosition != avatarAttachComponent.lastAnchorPointPosition)
            {
                targetTransform.position = anchorPointPosition;
                avatarAttachComponent.lastAnchorPointPosition = anchorPointPosition;
                modifiedComponent = true;
            }

            if (anchorPointRotation != avatarAttachComponent.lastAnchorPointRotation)
            {
                targetTransform.rotation = anchorPointRotation;
                avatarAttachComponent.lastAnchorPointRotation = anchorPointRotation;
                modifiedComponent = true;
            }

            return modifiedComponent;
        }
    }
}
