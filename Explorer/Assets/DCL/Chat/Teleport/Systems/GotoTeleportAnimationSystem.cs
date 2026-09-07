using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.CharacterCamera.Systems;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Components;
using DCL.Chat.Commands;
using DCL.Input;
using DCL.Input.Component;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System;
using UnityEngine;

namespace DCL.Chat.Teleport
{
    /// <summary>
    /// Keeps goto presentation and input ownership active through departure, loading, and arrival.
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(UpdateCinemachineBrainSystem))]
    [UpdateBefore(typeof(PrepareExposedCameraDataSystem))]
    [UpdateBefore(typeof(AvatarShapeVisibilitySystem))]
    public partial class GotoTeleportAnimationSystem : BaseUnityLoopSystem
    {
        private const float DEPARTURE_DURATION = 4.8f;
        private const float ARRIVAL_DURATION = 2f;
        private static readonly int EFFECT_ID = Shader.PropertyToID("_DCLTeleportEffect");
        private static readonly InputMapComponent.Kind[] BLOCKED_INPUTS =
        {
            InputMapComponent.Kind.Player, InputMapComponent.Kind.Camera, InputMapComponent.Kind.FreeCamera,
            InputMapComponent.Kind.Emotes, InputMapComponent.Kind.InWorldCamera,
        };

        private readonly GotoTeleportAnimation animation;
        private SingleInstanceEntity player;
        private SingleInstanceEntity camera;
        private SingleInstanceEntity input;

        internal GotoTeleportAnimationSystem(World world, GotoTeleportAnimation animation) : base(world)
        {
            this.animation = animation;
        }

        public override void Initialize()
        {
            player = World.CachePlayer();
            camera = World.CacheCamera();
            input = World.CacheInputMap();
            Shader.SetGlobalVector(EFFECT_ID, Vector4.zero);
            Shader shader = Resources.Load<Shader>("GotoTeleportTrail");
            World.Add(player, new GotoTeleportState(new GotoTeleportTrails(shader)));
        }

        protected override void OnDispose()
        {
            animation.Finish();
            if (!World.TryGet(player, out GotoTeleportState state)) return;
            Restore(state);
            state.Trails.Dispose();
        }

        protected override void Update(float t)
        {
            if (!World.TryGet(player, out GotoTeleportState state)) return;

            try
            {
                if (!animation.IsRequested || animation.CancellationToken.IsCancellationRequested
                    || World.Has<DeleteEntityIntention>(player))
                {
                    Restore(state);
                    return;
                }

                if (state.Active && state.Departure != animation.Departure)
                    Restore(state);

                if (!state.Active && animation.Departure?.Task.Status == Cysharp.Threading.Tasks.UniTaskStatus.Succeeded)
                {
                    animation.Arrival?.TrySetResult();
                    return;
                }

                if (!World.TryGet(player, out AvatarBase avatar) || !World.TryGet(player, out AvatarCustomSkinningComponent skinning)
                    || (state.Active && state.Avatar != avatar))
                {
                    Restore(state);
                    animation.Departure?.TrySetResult();
                    animation.Arrival?.TrySetResult();
                    return;
                }

                if (!state.Active)
                    Begin(state, avatar);

                if (animation.Arrival != null && !World.Has<PlayerTeleportIntent>(player))
                {
                    if (!state.Landing)
                    {
                        state.Landing = true;
                        state.LandingElapsed = 0f;
                    }

                    float progress = Mathf.Clamp01(state.LandingElapsed / ARRIVAL_DURATION);
                    GotoTeleportEmote.ApplyLanding(state, progress);
                    ref CameraComponent cameraComponent = ref World.Get<CameraComponent>(camera);
                    GotoTeleportPresentation.ApplyLanding(state, skinning, ref cameraComponent, progress);
                    state.LandingElapsed += UnityEngine.Time.unscaledDeltaTime;

                    if (progress >= 1f)
                    {
                        Restore(state);
                        animation.Arrival.TrySetResult();
                    }

                    return;
                }

                state.Elapsed += UnityEngine.Time.unscaledDeltaTime;
                GotoTeleportEmote.Apply(World, player, state);
                GotoTeleportPresentation.ApplyDeparture(state, skinning, World.Get<CameraComponent>(camera), DEPARTURE_DURATION);

                if (state.Elapsed >= DEPARTURE_DURATION)
                    animation.Departure?.TrySetResult();
            }
            catch (Exception exception)
            {
                Restore(state);
                animation.Departure?.TrySetException(exception);
                animation.Arrival?.TrySetException(exception);
                throw;
            }
        }

        private void Begin(GotoTeleportState state, AvatarBase avatar)
        {
            // Add the marker before taking component references, as it changes the archetype.
            state.OwnsMotionStop = !World.Has<StopCharacterMotion>(player);
            if (state.OwnsMotionStop)
                World.Add(player, new StopCharacterMotion());

            GotoTeleportEmote.Request(World, player);

            ref CameraComponent cameraComponent = ref World.Get<CameraComponent>(camera);
            state.CameraMode = cameraComponent.Mode;
            state.CameraPosition = cameraComponent.Camera.transform.position;
            state.CameraRotation = cameraComponent.Camera.transform.rotation;
            state.Avatar = avatar;
            state.AvatarLocalPosition = avatar.transform.localPosition;
            state.Origin = avatar.transform.position;
            state.Elapsed = 0f;
            state.Landing = false;
            state.LandingEmoteStarted = false;
            state.EmoteClip = null;
            state.EmoteFrozen = false;
            state.HighestHandPosition = float.NegativeInfinity;
            state.HighestHandNormalizedTime = 0f;
            state.Departure = animation.Departure;
            state.Active = true;
            cameraComponent.Mode = CameraMode.ThirdPerson;
            cameraComponent.AddCameraInputLock();
            ref InputMapComponent inputMap = ref World.Get<InputMapComponent>(input);
            foreach (InputMapComponent.Kind kind in BLOCKED_INPUTS)
                inputMap.BlockInput(kind);
        }

        private void Restore(GotoTeleportState state)
        {
            Shader.SetGlobalVector(EFFECT_ID, Vector4.zero);
            if (!state.Active) return;

            state.Active = false;
            GotoTeleportEmote.Restore(World, player, state);
            if (state.Avatar != null)
                state.Avatar.transform.localPosition = state.AvatarLocalPosition;
            state.Avatar = null;
            state.Trails.Hide();

            ref CameraComponent cameraComponent = ref World.Get<CameraComponent>(camera);
            if (cameraComponent.Mode == CameraMode.ThirdPerson)
                cameraComponent.Mode = state.CameraMode;
            cameraComponent.RemoveCameraInputLock();
            ref InputMapComponent inputMap = ref World.Get<InputMapComponent>(input);
            foreach (InputMapComponent.Kind kind in BLOCKED_INPUTS)
                inputMap.UnblockInput(kind);

            if (state.OwnsMotionStop)
                World.Remove<StopCharacterMotion>(player);
        }
    }
}
