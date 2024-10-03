using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Web3.Identities;
using ECS;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class MovementBadgesSystem : BaseUnityLoopSystem
    {
        private const float HEIGHT = 500;
        private const float MIN_THRESHOLD = 0.01f; // 1 [cm]. Prevents accumulation of very small changes
        private const float MAX_THRESHOLD = 10; // m. Prevents exploits by teleporting (via beam for example)

        private readonly IRealmData realmData;
        private readonly Entity playerEntity;
        private readonly IWeb3IdentityCache identityCache;
        private readonly WalkedDistanceAnalytics walkedDistanceAnalytics;
        private readonly IAnalyticsController analytics;
        private readonly ElementBinding<string> totalElevationGainBinding = new (string.Empty);
        private readonly ElementBinding<string> stepsCountBinding = new (string.Empty);

        private CharacterRigidTransform? rigidTransform;

        // TODO (Vit): This is a temporary solution until we get badges implementation inside the project
        private bool badgeHeightReached;

        private float previousPositionY;
        private bool isTeleporting;

        private float totalElevationGain;
        private IWeb3Identity? currentIdentity;

        public MovementBadgesSystem(
            World world,
            IAnalyticsController analytics,
            IRealmData realmData,
            in Entity playerEntity,
            IWeb3IdentityCache identityCache,
            IDebugContainerBuilder debugContainerBuilder,
            WalkedDistanceAnalytics walkedDistanceAnalytics) : base(world)
        {
            this.analytics = analytics;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.identityCache = identityCache;
            this.walkedDistanceAnalytics = walkedDistanceAnalytics;

            currentIdentity = identityCache?.Identity;

            debugContainerBuilder
               .TryAddWidget("Badges Tracking")?
               .AddCustomMarker("[Vertical Voyager] elevation gain: ", totalElevationGainBinding)
               .AddCustomMarker("[Walkabout Wanderer] steps count: ", stepsCountBinding);
        }

        protected override void Update(float t)
        {
            if(identityCache?.Identity == null) return;

            HandleIdentityChange();

            walkedDistanceAnalytics.Update(t);
            UpdateHeightAnalytics();

            UpdateDebugInfo();
        }

        private void HandleIdentityChange()
        {
            if (currentIdentity == null || !currentIdentity.Address.Equals(identityCache.Identity.Address))
            {
                currentIdentity = identityCache.Identity;
                badgeHeightReached = false;
                totalElevationGain = 0;

                walkedDistanceAnalytics.Reset();
            }
        }

        private void UpdateHeightAnalytics()
        {
            if (badgeHeightReached || !realmData.Configured) return;
            if (IsNotMovingByFoot()) return;

            Vector3 currentPosition = World.Get<CharacterTransform>(playerEntity).Transform.position;

            if (DidJustTeleport(currentPosition)) return;

            float diff = currentPosition.y - previousPositionY;
            previousPositionY = currentPosition.y;

            AccumulateGain(diff);

            if (totalElevationGain > HEIGHT)
            {
                badgeHeightReached = true;
                analytics.Track(AnalyticsEvents.Badges.HEIGHT_REACHED);
            }
        }

        private void AccumulateGain(float diff)
        {
            // filtering out small and large changes
            if (diff is > MIN_THRESHOLD and < MAX_THRESHOLD)
                totalElevationGain += diff;
        }

        private bool DidJustTeleport(Vector3 currentPosition)
        {
            if (isTeleporting)
            {
                isTeleporting = false;
                previousPositionY = currentPosition.y;
                return true;
            }

            return false;
        }

        private bool IsNotMovingByFoot()
        {
            AnimationStates playerStates = World.Get<CharacterAnimationComponent>(playerEntity).States;
            bool isMovingPlatform = World.Get<CharacterPlatformComponent>(playerEntity).IsMovingPlatform;

            if (playerStates.IsFalling || playerStates.IsJumping || !playerStates.IsGrounded || isMovingPlatform)
                return true;

            if (World.TryGet(playerEntity, out PlayerTeleportIntent _))
            {
                isTeleporting = true;
                return true;
            }

            return false;
        }

        private void UpdateDebugInfo()
        {
            totalElevationGainBinding.Value = $"<color={(badgeHeightReached ? "green" : "white")}>{totalElevationGain} m</color>";
            stepsCountBinding.Value = walkedDistanceAnalytics.StepCount.ToString();
        }
    }
}
