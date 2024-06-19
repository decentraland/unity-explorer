using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    /// <summary>
    ///     <para>
    ///         Raycasts from the player camera and prepares data that will be consumed by other systems
    ///     </para>
    ///     <para>
    ///         Runs in the global world
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class PlayerOriginatedRaycastSystem : BaseUnityLoopSystem
    {
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly float maxRaycastDistance;
        private readonly PlayerInteractionEntity playerInteractionEntity;
        private readonly InputAction pointInput;

        internal PlayerOriginatedRaycastSystem(World world, InputAction pointInput,
            IEntityCollidersGlobalCache collidersGlobalCache, PlayerInteractionEntity playerInteractionEntity,
            float maxRaycastDistance) : base(world)
        {
            this.pointInput = pointInput;
            this.collidersGlobalCache = collidersGlobalCache;
            this.playerInteractionEntity = playerInteractionEntity;
            this.maxRaycastDistance = maxRaycastDistance;
        }

        protected override void Update(float t)
        {
            RaycastFromCameraQuery(World);
        }

        [Query]
        private void RaycastFromCamera(ref CameraComponent camera, in CursorComponent cursorComponent)
        {
            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            ref PlayerOriginRaycastResultForGlobalEntities raycastResultForGlobalEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForGlobalEntities;

            if (cursorComponent.CursorState == CursorState.Panning)
            {
                raycastResultForSceneEntities.Reset();
                raycastResultForGlobalEntities.Reset();
                return;
            }

            Ray ray = CreateRay(in camera, in cursorComponent);

            // we are interested in one hit only
            bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxRaycastDistance, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK);

            raycastResultForSceneEntities.SetRay(ray);
            raycastResultForGlobalEntities.SetRay(ray);

            if (hasHit)
            {
                float distance = camera.Mode == CameraMode.FirstPerson ? hitInfo.distance : Vector3.Distance(hitInfo.point, camera.PlayerFocus.position);

                if (collidersGlobalCache.TryGetSceneEntity(hitInfo.collider, out GlobalColliderSceneEntityInfo sceneEntityInfo))
                    raycastResultForSceneEntities.SetupHit(hitInfo, sceneEntityInfo, distance);
                else if (collidersGlobalCache.TryGetGlobalEntity(hitInfo.collider, out GlobalColliderGlobalEntityInfo globalEntityInfo))
                    raycastResultForGlobalEntities.SetupHit(hitInfo, globalEntityInfo, distance);
                else
                {
                    raycastResultForSceneEntities.Reset();
                    raycastResultForGlobalEntities.Reset();
                }
            }
            else
            {
                raycastResultForSceneEntities.Reset();
                raycastResultForGlobalEntities.Reset();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Ray CreateRay(in CameraComponent cameraComponent, in CursorComponent cursorComponent) =>
            cameraComponent.Camera.ScreenPointToRay(cursorComponent.CursorState != CursorState.Free
                ? new Vector3(cameraComponent.Camera.pixelWidth / 2f, cameraComponent.Camera.pixelHeight / 2f, 0)
                : pointInput.ReadValue<Vector2>());
    }
}
