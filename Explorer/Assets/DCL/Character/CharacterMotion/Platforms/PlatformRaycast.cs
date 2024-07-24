using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;
using Physics = DCL.Utilities.Physics;

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

            if (!Physics.SphereCast(ray, radius, out RaycastHit hitInfo, rayDistance + radius, PhysicsLayers.CHARACTER_ONLY_MASK, QueryTriggerInteraction.Ignore))
                platformComponent.CurrentPlatform = null;
            else if (platformComponent.CurrentPlatform != hitInfo.collider.transform)
            {
                Debug.Log("VVV PLATFORM CHANGE!!!");
                platformComponent.CurrentPlatform = hitInfo.collider.transform;
                platformComponent.LastPlatformPosition = hitInfo.collider.transform.position;
                platformComponent.LastPlatformPosition = null;
                platformComponent.LastAvatarRelativePosition = platformComponent.CurrentPlatform.InverseTransformPoint(characterTransform.position);
                platformComponent.LastAvatarRelativeRotation = platformComponent.CurrentPlatform.InverseTransformDirection(characterTransform.forward);
            }
        }
    }
}
