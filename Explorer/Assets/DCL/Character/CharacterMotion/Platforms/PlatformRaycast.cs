using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformRaycast
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(CharacterPlatformComponent platformComponent, float radius, Transform transform, ICharacterControllerSettings settings)
        {
            float rayDistance = settings.PlatformRaycastLength;
            float halfDistance = (rayDistance * 0.5f) + radius;

            var ray = new Ray
            {
                origin = transform.position + (Vector3.up * halfDistance),
                direction = Vector3.down,
            };

            bool hasHit = Physics.SphereCast(ray, radius, out RaycastHit hitInfo, rayDistance + radius, PhysicsLayers.CHARACTER_ONLY_MASK);

            if (hasHit)
            {
                if (platformComponent.CurrentPlatform != hitInfo.collider.transform)
                {
                    platformComponent.CurrentPlatform = hitInfo.collider.transform;
                    platformComponent.LastAvatarRelativePosition = platformComponent.CurrentPlatform.InverseTransformPoint(transform.position);
                    platformComponent.LastAvatarRelativeRotation = platformComponent.CurrentPlatform.InverseTransformDirection(transform.forward);
                }
            }
            else
                platformComponent.CurrentPlatform = null;
        }
    }
}
