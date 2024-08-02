using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class BadgesHeightReachedSystem : BaseUnityLoopSystem
    {
        private const float HEIGHT = 500;

        private readonly IRealmData realmData;
        private readonly Entity playerEntity;
        private readonly IAnalyticsController analytics;

        private CharacterRigidTransform? rigidTransform;

        // TODO (Vit): This is a temporary solution until we get badges implementation inside the project
        private bool badgeHeightReached;

        public BadgesHeightReachedSystem(World world, IAnalyticsController analytics, IRealmData realmData, in Entity playerEntity) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            if (badgeHeightReached || !realmData.Configured) return;

            AnimationStates playerStates = World.Get<CharacterAnimationComponent>(playerEntity).States;

            if (playerStates.IsFalling || playerStates.IsJumping || !playerStates.IsGrounded)
                return;

            Vector3 currentPosition = World.Get<CharacterTransform>(playerEntity).Transform.position;

            if (currentPosition.y > HEIGHT)
            {
                badgeHeightReached = true;
                analytics.Track(AnalyticsEvents.Badges.HEIGHT_REACHED);
            }
        }
    }
}
