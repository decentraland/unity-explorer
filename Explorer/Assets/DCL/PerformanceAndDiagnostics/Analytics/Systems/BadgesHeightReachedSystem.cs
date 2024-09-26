using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Web3.Identities;
using ECS;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class BadgesHeightReachedSystem : BaseUnityLoopSystem
    {
        private const float HEIGHT = 500;
        private const float MIN_THRESHOLD = 0.01f; // 1 [cm]. Prevents accumulation of very small changes
        private const float MAX_THRESHOLD = 10; // m. Prevents exploits by teleporting (via beam for example)

        private readonly IRealmData realmData;
        private readonly Entity playerEntity;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;

        private CharacterRigidTransform? rigidTransform;

        // TODO (Vit): This is a temporary solution until we get badges implementation inside the project
        private bool badgeHeightReached;

        private float previousPositionY;
        private bool isTeleporting;

        private float totalElevationGain;
        private IWeb3Identity? currentIdentity;

        public BadgesHeightReachedSystem(World world, IAnalyticsController analytics, IRealmData realmData, in Entity playerEntity, IWeb3IdentityCache identityCache) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.identityCache = identityCache;

            currentIdentity = identityCache.Identity;
        }

        protected override void Update(float t)
        {
            if (!currentIdentity.Address.Equals(identityCache.Identity.Address))
            {
                currentIdentity = identityCache.Identity;
                badgeHeightReached = false;
                totalElevationGain = 0;
            }

            if (badgeHeightReached || !realmData.Configured) return;

            AnimationStates playerStates = World.Get<CharacterAnimationComponent>(playerEntity).States;
            bool isMovingPlatform = World.Get<CharacterPlatformComponent>(playerEntity).IsMovingPlatform;

            if (playerStates.IsFalling || playerStates.IsJumping || !playerStates.IsGrounded || isMovingPlatform)
                return;

            if (World.TryGet(playerEntity, out PlayerTeleportIntent _))
            {
                isTeleporting = true;
                return;
            }

            Vector3 currentPosition = World.Get<CharacterTransform>(playerEntity).Transform.position;

            if (isTeleporting)
            {
                isTeleporting = false;
                previousPositionY = currentPosition.y;
                return;
            }

            float diff = currentPosition.y - previousPositionY;
            previousPositionY = currentPosition.y;

            // filtering out small changes
            if (diff is < MIN_THRESHOLD or > MAX_THRESHOLD) return;

            totalElevationGain += diff;
            if (totalElevationGain > HEIGHT)
            {
                badgeHeightReached = true;
                analytics.Track(AnalyticsEvents.Badges.HEIGHT_REACHED);
            }

            Debug.Log($"VVV [{currentIdentity.Address}] : {totalElevationGain} {badgeHeightReached}");
        }
    }
}
