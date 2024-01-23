using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using DCL.SDKComponents.AvatarAttach.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarAttachHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private AvatarBase ownPlayerAvatarBase;

        public AvatarAttachHandlerSystem(World world, WorldProxy globalWorld) : base(world)
        {
            // Query GLOBAL world with: typeof(AvatarBase) + typeof(PlayerComponent)
            // PlayerEntity is being created at GlobalWorldFactory...

            // world.Query(new QueryDescription().WithAll<DummyComponent>(), (ref DummyComponent dummy) => DummySystem(ref dummy));
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
        private void SetupAvatarAttach(in Entity entity, ref PBAvatarAttach pbAvatarAttach, ref TransformComponent transformComponent, ref PartitionComponent partitionComponent)
        {
            Debug.Log("PRAVS - InitializeAvatarAttach...", transformComponent.Transform);

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

            transformComponent.Transform.SetParent(component.anchorPointTransform);

            World.Add(entity, component);
        }

        // [Query]
        // private void UpdateAvatarAttach(ref PBAvatarAttach pbAvatarAttach, ref AvatarAttachComponent avatarAttachComponent) { }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
