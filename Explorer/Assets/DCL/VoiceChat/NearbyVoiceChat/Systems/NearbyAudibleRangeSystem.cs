using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Pull-mirror from local-player ↔ avatar distance to range/suspend archetype markers.
    ///     Pass-through under the listening gate (markers reflect spatial reality; consumers gate on policy).
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateAfter(typeof(NearbyLivekitBridgeSystem))]
    [UpdateBefore(typeof(NearbyAudioBindingSystem))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyAudibleRangeSystem : BaseUnityLoopSystem
    {
        private readonly float outerOutSqr;
        private readonly float outerInSqr;
        private readonly float suspendOutSqr;
        private readonly float suspendInSqr;
        private readonly NearbyListenerState listenerState;

        private SingleInstanceEntity cameraEntity;
        private Transform cameraTransform = null!;
        private Transform playerFocusTransform = null!;

        internal NearbyAudibleRangeSystem(World world, VoiceChatConfiguration configuration, NearbyListenerState listenerState) : base(world)
        {
            this.listenerState = listenerState;

            Vector2 rangeBand = configuration.nearbyAudibleRangeBand;
            Vector2 suspendBand = configuration.nearbyAudibleSuspendBand;

            AssertHysteresisInvariant(rangeBand, suspendBand);

            // Vector2 convention: x = inner radius, y = outer radius.
            outerInSqr = rangeBand.x * rangeBand.x;
            outerOutSqr = rangeBand.y * rangeBand.y;
            suspendInSqr = suspendBand.x * suspendBand.x;
            suspendOutSqr = suspendBand.y * suspendBand.y;
        }

        private static void AssertHysteresisInvariant(Vector2 rangeBand, Vector2 suspendBand)
        {
            Debug.Assert(rangeBand.y > rangeBand.x, $"NearbyAudibleRange: RangeBand outer ({rangeBand.y}) must be > inner ({rangeBand.x}).");
            Debug.Assert(rangeBand.x > suspendBand.y, $"NearbyAudibleRange: RangeBand inner ({rangeBand.x}) must be > SuspendBand outer ({suspendBand.y}).");
            Debug.Assert(suspendBand.y > suspendBand.x, $"NearbyAudibleRange: SuspendBand outer ({suspendBand.y}) must be > inner ({suspendBand.x}).");
            Debug.Assert(suspendBand.x > 0f, $"NearbyAudibleRange: SuspendBand inner ({suspendBand.x}) must be > 0.");
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();

            cameraTransform = World.Get<CameraComponent>(cameraEntity).Camera.transform;
            playerFocusTransform = World.Get<PlayerComponent>(World.CachePlayer()).CameraFocus;

            listenerState.BindListener(cameraTransform);
        }

        protected override void Update(float t)
        {
            bool isFirstPerson = cameraEntity.GetCameraComponent(World).Mode == CameraMode.FirstPerson;
            Vector3 playerHeadPosition = isFirstPerson ? cameraTransform.position : playerFocusTransform.position;

            listenerState.IsFirstPerson = isFirstPerson;
            listenerState.PlayerHeadPosition = playerHeadPosition;

            // Order matters:
            TryEnterAudibleRangeQuery(World, playerHeadPosition); // 1. Avatars without the tag: enter the outer-in band .
            UpdateInAudibleRangeQuery(World, playerHeadPosition); // 2. Avatars with the tag: exit on outer-out, otherwise mutate IsSuspended via the suspend hysteresis
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(InAudibleRangeTag))]
        [All(typeof(AvatarBase), typeof(NearbyAudioStreamerComponent))]
        private void TryEnterAudibleRange([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase, in RemotePlayerMovementComponent remoteMovement)
        {
            if (!remoteMovement.Initialized) return;

            float distSqr = (avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude;

            if (distSqr <= outerInSqr)
                World.Add(entity, new InAudibleRangeTag { IsSuspended = distSqr >= suspendOutSqr });
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(NearbyAudioStreamerComponent))]
        private void UpdateInAudibleRange([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase, ref InAudibleRangeTag tag)
        {
            float distSqr = (avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude;

            // Exit: drop membership entirely.
            if (distSqr > outerOutSqr)
            {
                World.Remove<InAudibleRangeTag>(entity);
                return;
            }

            if (tag.IsSuspended)
            {
                if (distSqr < suspendInSqr)
                    tag.IsSuspended = false;
            }
            else if (distSqr >= suspendOutSqr)
                tag.IsSuspended = true;
        }
    }
}
