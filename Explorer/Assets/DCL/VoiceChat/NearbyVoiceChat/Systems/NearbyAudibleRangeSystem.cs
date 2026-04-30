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
using UnityEngine;

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
        private const float OUTER_OUT_SQR = 22f * 22f;   // crossing outward beyond → remove InAudibleRangeTag
        private const float OUTER_IN_SQR = 18f * 18f;    // crossing inward below   → add    InAudibleRangeTag
        private const float SUSPEND_OUT_SQR = 17f * 17f; // crossing outward beyond → add    IsSuspendedTag
        private const float SUSPEND_IN_SQR = 16f * 16f;  // crossing inward below   → remove IsSuspendedTag

        private SingleInstanceEntity playerEntity;
        private SingleInstanceEntity cameraEntity;

        internal NearbyAudibleRangeSystem(World world) : base(world) { }

        public override void Initialize()
        {
            playerEntity = World.CachePlayer();
            cameraEntity = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            Vector3 listenerPos = GetListenerPosition();

            // Order matters:
            // 1. Tag avatars whose distance just dropped inside the outer-in threshold.
            // 2. Untag avatars whose distance just crossed the outer-out threshold (cascades suspend removal).
            // 3. Tag avatars in the suspend band.
            // 4. Untag avatars whose distance just dropped below suspend-in.
            AddAudibleRangeTagQuery(World, listenerPos);
            RemoveAudibleRangeTagQuery(World, listenerPos);
            AddSuspendedTagQuery(World, listenerPos);
            RemoveSuspendedTagQuery(World, listenerPos);
        }

        // Mirrors NearbyAudioPositionSystem's listener-position resolution so the cull boundary
        // is evaluated against the same spatial reference the player actually hears.
        private Vector3 GetListenerPosition()
        {
            ref readonly CameraComponent cam = ref cameraEntity.GetCameraComponent(World);
            if (cam.Mode == CameraMode.FirstPerson) return cam.Camera.transform.position;
            if (World.TryGet(playerEntity, out PlayerComponent playerComp)) return playerComp.CameraFocus.position;
            return cam.Camera.transform.position;
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(InAudibleRangeTag))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent))]
        private void AddAudibleRangeTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            float sqr = (avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude;
            if (sqr > OUTER_IN_SQR) return;

            World.Add<InAudibleRangeTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent), typeof(InAudibleRangeTag))]
        private void RemoveAudibleRangeTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            float sqr = (avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude;
            if (sqr <= OUTER_OUT_SQR) return;

            World.Remove<InAudibleRangeTag>(entity);

            // Cascade: maintain invariant I1 (suspended ⊆ inAudibleRange) on every observable point.
            if (World.Has<IsSuspendedTag>(entity))
                World.Remove<IsSuspendedTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(IsSuspendedTag))]
        [All(typeof(AvatarBase), typeof(InAudibleRangeTag))]
        private void AddSuspendedTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            float sqr = (avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude;
            if (sqr < SUSPEND_OUT_SQR) return;

            World.Add<IsSuspendedTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(InAudibleRangeTag), typeof(IsSuspendedTag))]
        private void RemoveSuspendedTag([Data] Vector3 listenerPos, Entity entity, in AvatarBase avatarBase)
        {
            float sqr = (avatarBase.HeadAnchorPoint.position - listenerPos).sqrMagnitude;
            if (sqr >= SUSPEND_IN_SQR) return;

            World.Remove<IsSuspendedTag>(entity);
        }
    }
}
