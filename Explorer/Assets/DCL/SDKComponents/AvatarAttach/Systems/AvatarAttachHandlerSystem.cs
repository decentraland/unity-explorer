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
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using System;
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
            globalWorld.GetWorld()
                       .Query(new QueryDescription().WithAll<Profile, AvatarBase>(), (ref Profile profile, ref AvatarBase avatarBase) =>
                        {
                            if (profile.UserId == "fakeOwnUserId") // TODO: improve
                                ownPlayerAvatarBase = avatarBase;
                        });
        }

        protected override void Update(float t)
        {
            SetupAvatarAttachQuery(World);

            // UpdateAvatarAttachQuery(World);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void SetupAvatarAttach(in Entity entity, ref TransformComponent transformComponent, ref PBAvatarAttach pbAvatarAttach)
        {
            var component = new AvatarAttachComponent();

            switch (pbAvatarAttach.AnchorPointId)
            {
                case AvatarAnchorPointType.AaptLeftHand:
                    component.anchorPointTransform = ownPlayerAvatarBase.LeftHandAnchorPoint;
                    break;
                case AvatarAnchorPointType.AaptRightHand:
                    component.anchorPointTransform = ownPlayerAvatarBase.RightHandAnchorPoint;
                    break;
                case AvatarAnchorPointType.AaptNameTag:
                default: // AvatarAnchorPointType.AaptPosition
                    component.anchorPointTransform = ownPlayerAvatarBase.transform;
                    break;
            }

            // Debug.Log("PRAVS - SetupAvatarAttach() - 1", transformComponent.Transform);
            // Debug.Log("PRAVS - SetupAvatarAttach() - 2", component.anchorPointTransform);

            transformComponent.Transform.SetParent(component.anchorPointTransform);
            transformComponent.Transform.localPosition = Vector3.zero;
            transformComponent.Transform.localRotation = Quaternion.identity;

            World.Add(entity, component);
        }

        [Query]
        private void UpdateAvatarAttach(ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent) { }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
