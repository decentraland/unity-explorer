using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AvatarAttach.Components;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using System;

namespace DCL.SDKComponents.AvatarAttach.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarAttachHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        public AvatarAttachHandlerSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InitializeAvatarAttachQuery(World);
            UpdateAvatarAttachQuery(World);
        }

        [Query]
        [None(typeof(AvatarAttachComponent))]
        private void InitializeAvatarAttach(in Entity entity, ref PBAvatarAttach pbAvatarAttach)
        {
            var component = new AvatarAttachComponent();

            /*switch (pbAvatarAttach.AnchorPointId)
            {
                // case AvatarAnchorPointType.AaptNameTag: break;
                // case AvatarAnchorPointType.AaptLeftHand: break;
                // case AvatarAnchorPointType.AaptRightHand: break;
                default: // AvatarAnchorPointType.AaptPosition
                    // component.anchorPointTransform =
                    break;
            }*/

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
