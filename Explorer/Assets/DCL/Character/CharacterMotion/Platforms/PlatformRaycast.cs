﻿using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Utilities;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformRaycast
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(CharacterPlatformComponent platformComponent, float radius, Transform characterTransform, ICharacterControllerSettings settings)
        {
            float rayDistance = settings.PlatformRaycastLength;
            float halfDistance = (rayDistance * 0.5f) + radius;

            Vector3 rayOrigin = characterTransform.position + (Vector3.up * halfDistance);

            var ray = new Ray
            {
                origin = rayOrigin,
                direction = Vector3.down,
            };

            if (!DCLPhysics.SphereCast(ray, radius, out RaycastHit hitInfo, rayDistance + radius, PhysicsLayers.CHARACTER_ONLY_MASK, QueryTriggerInteraction.Ignore))
            {
                platformComponent.PlatformCollider = null;
                platformComponent.CurrentPlatform = null;
                platformComponent.LastPlatformPosition = null;
            }
            else if (platformComponent.CurrentPlatform != hitInfo.collider.transform)
            {
                platformComponent.PlatformCollider = hitInfo.collider;
                platformComponent.CurrentPlatform = hitInfo.collider.transform;

                platformComponent.LastPlatformPosition = null;
                platformComponent.LastAvatarRelativePosition = platformComponent.CurrentPlatform.InverseTransformPoint(characterTransform.position);
                platformComponent.LastAvatarRelativeRotation = platformComponent.CurrentPlatform.InverseTransformDirection(characterTransform.forward);
            }
        }
    }
}
