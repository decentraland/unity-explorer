using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    public partial class HandPointAtSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput dclInput;

        private SingleInstanceEntity camera;

        private HandPointAtSystem(World world) : base(world)
        {
            dclInput = DCLInput.Instance;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            UpdateHandPointAtQuery(World, in camera.GetCameraComponent(World), t);
            ApplyPointAtIKQuery(World, t);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateHandPointAt(
            [Data] in CameraComponent cameraComponent,
            [Data] float dt,
            ref HandPointAtComponent handPointAtComponent,
            in AvatarBase avatarBase,
            in CharacterRigidTransform rigidTransform,
            in StunComponent stunComponent,
            in CharacterEmoteComponent emoteComponent,
            in CharacterPlatformComponent platformComponent,
            in ICharacterControllerSettings settings)
        {
            bool canPointAt = rigidTransform.IsGrounded
                              && !(rigidTransform.MoveVelocity.Velocity.sqrMagnitude > 0.5f)
                              && !stunComponent.IsStunned
                              && !emoteComponent.IsPlayingEmote
                              && !platformComponent.PositionChanged;

            handPointAtComponent.TickDuration(dt);

            if (!canPointAt)
                handPointAtComponent.RefreshDuration(0f);

            if (!canPointAt || !dclInput.Player.PointAt.IsPressed())
                return;

            handPointAtComponent.RefreshDuration(settings.PointAtDuration);

            Ray ray = cameraComponent.Camera.ScreenPointToRay(dclInput.UI.Point.ReadValue<Vector2>());
            Vector3 hitPoint = Vector3.zero;

            if (Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, settings.PointAtMaxDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
                hitPoint = hit.point;
            else
            {
                Camera cam = cameraComponent.Camera;
                Plane farPlane = new Plane(
                    -cam.transform.forward,
                    cam.transform.position + (cam.transform.forward * cam.farClipPlane)
                );

                if (farPlane.Raycast(ray, out float enter))
                    hitPoint = ray.GetPoint(enter);
            }

            Debug.DrawLine(ray.origin, hitPoint, Color.red);
            Vector3 shoulderPos = avatarBase.RightShoulderAnchorPoint.position;
            Vector3 directionToTarget = (hitPoint - shoulderPos).normalized;

            Vector3 ikTargetPos = shoulderPos + (directionToTarget * settings.PointAtArmReach);
            handPointAtComponent.Point = ikTargetPos;

            handPointAtComponent.IsPointing = true;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyPointAtIK(
            [Data] float dt,
            ref HandPointAtComponent pointAt,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings)
        {
            if (!pointAt.IsPointing) return;

            // Drive the existing right hand constraint directly
            avatarBase.RightHandIK.weight = Mathf.MoveTowards(
                avatarBase.RightHandIK.weight, 1f, settings.HandsIKWeightSpeed * dt);

            Transform target = avatarBase.RightHandSubTarget;
            target.position = Vector3.MoveTowards(
                target.position, pointAt.Point, settings.IKPositionSpeed * dt);

            Vector3 pointDirection = (pointAt.Point - avatarBase.RightShoulderAnchorPoint.position).normalized;
            target.forward = pointDirection;
        }
    }
}
