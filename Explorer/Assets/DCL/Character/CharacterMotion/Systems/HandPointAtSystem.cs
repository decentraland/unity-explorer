using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
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
            UpdateHandPointAtQuery(World, in camera.GetCameraComponent(World));
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateHandPointAt(
            [Data] in CameraComponent cameraComponent,
            ref HandPointAtComponent handPointAtComponent,
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

            if (!canPointAt || !dclInput.Player.PointAt.IsPressed()) return;

            Ray ray = cameraComponent.Camera.ScreenPointToRay(dclInput.UI.Point.ReadValue<Vector2>());
            bool rayCastSuccess = Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, settings.PointAtMaxDistance, PhysicsLayers.CHARACTER_ONLY_MASK);

            if (rayCastSuccess)
            {
                Debug.DrawLine(ray.origin, hit.point, Color.red);
                handPointAtComponent.Point = hit.point;
            }

            handPointAtComponent.IsPointing = rayCastSuccess;
        }
    }
}
