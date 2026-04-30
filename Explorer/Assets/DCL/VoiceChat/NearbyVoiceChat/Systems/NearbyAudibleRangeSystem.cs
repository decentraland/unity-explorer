using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Pull-mirror from local-player ↔ avatar distance to range/suspend archetype markers.
    ///     Runs every tick in <see cref="AvatarGroup"/>, after <see cref="NearbyLivekitBridgeSystem"/>
    ///     (so streaming markers are current) and before <see cref="NearbyAudioBindingSystem"/>
    ///     (so the binding filter sees a current <see cref="InAudibleRangeTag"/> set). Stateless —
    ///     pass-through under the listening gate (markers reflect spatial reality; consumers gate on policy).
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(NearbyLivekitBridgeSystem))]
    [UpdateBefore(typeof(NearbyAudioBindingSystem))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyAudibleRangeSystem : BaseUnityLoopSystem
    {
        // Squared comparisons throughout — no sqrt per avatar per tick.
        private readonly float outerOutSqr;
        private readonly float outerInSqr;
        private readonly float suspendOutSqr;
        private readonly float suspendInSqr;

        private SingleInstanceEntity playerEntity;
        private SingleInstanceEntity cameraEntity;

        internal NearbyAudibleRangeSystem(World world, VoiceChatConfiguration configuration) : base(world)
        {
            Vector2 rangeBand = configuration.nearbyAudibleRangeBand;
            Vector2 suspendBand = configuration.nearbyAudibleSuspendBand;

            AssertHysteresisInvariant(rangeBand, suspendBand);

            // Vector2 convention across both bands: x = inner radius, y = outer radius.
            outerInSqr = rangeBand.x * rangeBand.x;
            outerOutSqr = rangeBand.y * rangeBand.y;
            suspendInSqr = suspendBand.x * suspendBand.x;
            suspendOutSqr = suspendBand.y * suspendBand.y;
        }

        // Bands must strictly nest so a single distance can't satisfy contradictory tag transitions
        // on the same tick. Checked once at construction — values can't drift at runtime.
        [Conditional("UNITY_ASSERTIONS")]
        private static void AssertHysteresisInvariant(Vector2 rangeBand, Vector2 suspendBand)
        {
            Debug.Assert(rangeBand.y > rangeBand.x, $"NearbyAudibleRange: RangeBand outer ({rangeBand.y}) must be > inner ({rangeBand.x}).");
            Debug.Assert(rangeBand.x > suspendBand.y, $"NearbyAudibleRange: RangeBand inner ({rangeBand.x}) must be > SuspendBand outer ({suspendBand.y}).");
            Debug.Assert(suspendBand.y > suspendBand.x, $"NearbyAudibleRange: SuspendBand outer ({suspendBand.y}) must be > inner ({suspendBand.x}).");
            Debug.Assert(suspendBand.x > 0f, $"NearbyAudibleRange: SuspendBand inner ({suspendBand.x}) must be > 0.");
        }

        public override void Initialize()
        {
            playerEntity = World.CachePlayer();
            cameraEntity = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            Vector3 listenerPos = GetListenerPosition();

            // Order matters:
            AddAudibleRangeTagQuery(World, listenerPos); // 1. Tag avatars whose distance just dropped inside the outer-in threshold.
            RemoveAudibleRangeTagQuery(World, listenerPos); // 2. Untag avatars whose distance just crossed the outer-out threshold (cascades suspend removal).
            AddSuspendedTagQuery(World, listenerPos); // 3. Tag avatars in the suspend band.
            RemoveSuspendedTagQuery(World, listenerPos); // 4. Untag avatars whose distance just dropped below suspend-in.
        }

        // Mirrors NearbyAudioPositionSystem's listener-position resolution so the cull boundary is evaluated against the same spatial reference the player actually hears.
        private Vector3 GetListenerPosition()
        {
            ref readonly CameraComponent cam = ref cameraEntity.GetCameraComponent(World);
            if (cam.Mode == CameraMode.FirstPerson)
                return cam.Camera.transform.position;

            return World.TryGet(playerEntity, out PlayerComponent playerComp)
                ? playerComp.CameraFocus.position
                : cam.Camera.transform.position;
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(InAudibleRangeTag))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent))]
        private void AddAudibleRangeTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            if ((avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude <= outerInSqr)
                World.Add<InAudibleRangeTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent), typeof(InAudibleRangeTag))]
        private void RemoveAudibleRangeTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            if ((avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude > outerOutSqr)
            {
                World.Remove<InAudibleRangeTag>(entity);

                // Cascade: maintain invariant I1 (suspended ⊆ inAudibleRange) on every observable point.
                if (World.Has<IsSuspendedTag>(entity))
                    World.Remove<IsSuspendedTag>(entity);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(IsSuspendedTag))]
        [All(typeof(AvatarBase), typeof(InAudibleRangeTag))]
        private void AddSuspendedTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            if ((avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude >= suspendOutSqr)
                World.Add<IsSuspendedTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(InAudibleRangeTag), typeof(IsSuspendedTag))]
        private void RemoveSuspendedTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            if ((avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude < suspendInSqr)
                World.Remove<IsSuspendedTag>(entity);
        }
    }
}
