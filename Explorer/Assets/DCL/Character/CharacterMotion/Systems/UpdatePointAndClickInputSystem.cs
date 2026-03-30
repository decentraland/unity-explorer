using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using DCL.Input;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    /// Detects a double left-click on world geometry and drives the avatar toward the clicked
    /// position using the normal physics pipeline.
    ///
    /// On each double-click a <see cref="PointAndClickMovementComponent"/> is added (or updated)
    /// on the player entity. While active, this system overrides <see cref="MovementInputComponent"/>
    /// so that <see cref="CalculateCharacterVelocitySystem"/> steers the avatar toward the target.
    ///
    /// The movement is cancelled when:
    ///   - the avatar arrives within <see cref="ICharacterControllerSettings.PointAndClickArrivalDistance"/>;
    ///   - the avatar is stuck (moved less than <see cref="ICharacterControllerSettings.PointAndClickStuckMinMovement"/>
    ///     in a <see cref="ICharacterControllerSettings.PointAndClickStuckCheckInterval"/> window);
    ///   - the player provides keyboard/gamepad movement or jump input.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateInputMovementSystem))]
    public partial class UpdatePointAndClickInputSystem : BaseUnityLoopSystem
    {
        private const float MARKER_RADIUS = 0.2f;
        private const float MARKER_DURATION = 3f;

        private SingleInstanceEntity camera;
        private ICharacterControllerSettings cachedSettings;
        private InputAction sprintAction;
        private InputAction walkAction;

        // Stores the time of the previous left-click to detect double-clicks.
        private float lastClickTime = float.MinValue;

        private GameObject targetMarker;
        private float markerTimer;
        private bool markerShouldBeDestroyed;

        internal UpdatePointAndClickInputSystem(World world) : base(world) { }

        protected override void OnDispose()
        {
            DestroyMarker();
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();

            // Cache the settings reference once — it is a ScriptableObject and never reassigned
            World.Query(in new QueryDescription().WithAll<ICharacterControllerSettings>(),
                (ref ICharacterControllerSettings s) => cachedSettings = s);

            sprintAction = DCLInput.Instance.Player.Sprint;
            walkAction = DCLInput.Instance.Player.Walk;
        }

        protected override void Update(float t)
        {
            markerShouldBeDestroyed = false;

            ref readonly CameraComponent cameraComponent = ref camera.GetCameraComponent(World);

            // Detect a double-click and resolve the world-space target
            bool hasDoubleClick = false;
            Vector3 clickTarget = default;

            var mouse = Mouse.current;

            if (mouse != null
                && mouse.leftButton.wasPressedThisFrame)
            {
                float now = UnityEngine.Time.unscaledTime;

                if (now - lastClickTime <= cachedSettings.PointAndClickDoubleClickThreshold)
                {
                    Vector2 screenPos = mouse.position.ReadValue();
                    Ray ray = cameraComponent.Camera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));

                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        clickTarget = hit.point;
                        hasDoubleClick = true;
                    }

                    // Reset so a triple-click does not re-trigger
                    lastClickTime = float.MinValue;
                }
                else
                    lastClickTime = now;
            }

            // Pre-compute camera-relative basis vectors needed for axes projection.
            // CalculateCharacterVelocitySystem uses the same method to interpret axes.
            Transform camTransform = cameraComponent.Camera.transform;
            Vector3 viewerForward = LookDirectionUtils.FlattenLookDirection(camTransform.forward, camTransform.up);
            Vector3 viewerRight = Vector3.Cross(-viewerForward, Vector3.up);

            if (hasDoubleClick)
                ShowMarker(clickTarget);

            // Tick down the marker lifetime independently of movement state
            if (targetMarker != null)
            {
                markerTimer -= t;
                if (markerTimer <= 0f)
                    DestroyMarker();
            }

            UpdateTargetQuery(World, hasDoubleClick, clickTarget);
            DriveMovementQuery(World, t, viewerForward, viewerRight);

            if (markerShouldBeDestroyed)
                DestroyMarker();
        }

        private void ShowMarker(Vector3 position)
        {
            DestroyMarker();

            targetMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetMarker.name = "PointAndClickMarker";

            targetMarker.transform.SetPositionAndRotation(position, Quaternion.identity);

            targetMarker.transform.localScale = Vector3.one * (MARKER_RADIUS * 2f);

            // Remove the collider so it never intercepts subsequent raycasts
            Object.Destroy(targetMarker.GetComponent<Collider>());

            // Bright cyan tint — visible against most surfaces
            targetMarker.GetComponent<Renderer>().material.color = new Color(0f, 0.9f, 1f);

            markerTimer = MARKER_DURATION;
        }

        private void DestroyMarker()
        {
            if (targetMarker == null) return;
            Object.Destroy(targetMarker);
            targetMarker = null;
        }

        /// <summary>
        /// Adds or updates <see cref="PointAndClickMovementComponent"/> when a double-click lands.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateTarget(
            [Data] bool hasDoubleClick,
            [Data] Vector3 clickTarget,
            Entity entity,
            in PlayerComponent _)
        {
            if (!hasDoubleClick)
                return;

            var component = new PointAndClickMovementComponent
            {
                TargetPosition = clickTarget,
                StuckCheckElapsed = 0f,
                PositionAtLastStuckCheck = World.Get<CharacterTransform>(entity).Position,
            };

            if (World.Has<PointAndClickMovementComponent>(entity))
                World.Set(entity, component);
            else
                World.Add(entity, component);
        }

        /// <summary>
        /// While <see cref="PointAndClickMovementComponent"/> is present, overrides
        /// <see cref="MovementInputComponent"/> to steer toward the target.
        /// Handles arrival, stuck detection, and manual-input cancellation.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PlayerMoveToWithDurationIntent), typeof(PlayerTeleportIntent))]
        private void DriveMovement(
            [Data] float dt,
            [Data] in Vector3 viewerForward,
            [Data] in Vector3 viewerRight,
            Entity entity,
            ref PointAndClickMovementComponent pcm,
            ref MovementInputComponent movementInput,
            in CharacterTransform characterTransform,
            in JumpInputComponent jumpInput,
            in ICharacterControllerSettings settings)
        {
            // Cancel when the player takes manual control
            bool hasManualMovement = movementInput.Kind != MovementKind.IDLE
                                     || movementInput.Axes != Vector2.zero;

            if (hasManualMovement || jumpInput.IsPressed)
            {
                markerShouldBeDestroyed = true;
                World.Remove<PointAndClickMovementComponent>(entity);
                return;
            }

            Vector3 characterPos = characterTransform.Position;
            Vector3 toTarget = pcm.TargetPosition - characterPos;
            toTarget.y = 0f;
            float xzDistance = toTarget.magnitude;

            // Arrival: stop when close enough
            if (xzDistance <= settings.PointAndClickArrivalDistance)
            {
                movementInput.Axes = Vector2.zero;
                movementInput.Kind = MovementKind.IDLE;
                markerShouldBeDestroyed = true;
                World.Remove<PointAndClickMovementComponent>(entity);
                return;
            }

            // Stuck detection: compare XZ movement over a fixed interval
            pcm.StuckCheckElapsed += dt;

            if (pcm.StuckCheckElapsed >= settings.PointAndClickStuckCheckInterval)
            {
                float xzMoved = new Vector2(
                    characterPos.x - pcm.PositionAtLastStuckCheck.x,
                    characterPos.z - pcm.PositionAtLastStuckCheck.z).magnitude;

                if (xzMoved < settings.PointAndClickStuckMinMovement)
                {
                    movementInput.Axes = Vector2.zero;
                    movementInput.Kind = MovementKind.IDLE;
                    markerShouldBeDestroyed = true;
                    World.Remove<PointAndClickMovementComponent>(entity);
                    return;
                }

                pcm.StuckCheckElapsed = 0f;
                pcm.PositionAtLastStuckCheck = characterPos;
            }

            // Project world-space direction onto viewer (camera) frame to produce axes.
            // This matches how CalculateCharacterVelocitySystem interprets MovementInputComponent.Axes:
            //   velocity = viewerForward * axes.y + viewerRight * axes.x
            Vector3 direction = toTarget / xzDistance; // already normalized
            movementInput.Axes = new Vector2(
                Vector3.Dot(direction, viewerRight),
                Vector3.Dot(direction, viewerForward));

            // Honour sprint/walk modifier keys the same way UpdateInputMovementSystem does
            if (sprintAction.IsPressed())
                movementInput.Kind = MovementKind.RUN;
            else if (walkAction.IsPressed())
                movementInput.Kind = MovementKind.WALK;
            else
                movementInput.Kind = MovementKind.JOG;
        }
    }
}
