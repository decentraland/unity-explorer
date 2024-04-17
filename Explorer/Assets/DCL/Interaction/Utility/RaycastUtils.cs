using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.Interaction.Utility
{
    public static class RaycastUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsSDKLayerInCollisionMask(ColliderLayer layer, ColliderLayer mask) =>
            (mask & layer) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsPlayer(Collider collider) =>
            collider.gameObject.layer == PhysicsLayers.CHARACTER_LAYER;

        internal static bool TryCreateRay(this PBRaycast sdkRaycast,
            World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap,
            Vector3 sceneRootPos, in TransformComponent entityTransform, out Ray ray)
        {
            Vector3 rayOrigin = entityTransform.Cached.WorldPosition + sdkRaycast.OriginOffset;
            Vector3 rayDirection;

            switch (sdkRaycast.DirectionCase)
            {
                case PBRaycast.DirectionOneofCase.LocalDirection:
                    rayDirection = entityTransform.Cached.WorldRotation * sdkRaycast.LocalDirection;
                    break;
                case PBRaycast.DirectionOneofCase.GlobalTarget:
                    rayDirection = sceneRootPos + sdkRaycast.GlobalTarget - entityTransform.Cached.WorldPosition;
                    break;
                case PBRaycast.DirectionOneofCase.TargetEntity:
                    if (entitiesMap.TryGetValue((int)sdkRaycast.TargetEntity, out Entity targetEntity))
                    {
                        TransformComponent targetTransform = world.Get<TransformComponent>(targetEntity);
                        rayDirection = targetTransform.Cached.WorldPosition - entityTransform.Cached.WorldPosition;
                    }
                    else
                    {
                        // Use Scene Root Position (why?)
                        rayDirection = sceneRootPos - entityTransform.Cached.WorldPosition;
                    }

                    break;
                case PBRaycast.DirectionOneofCase.GlobalDirection:
                    rayDirection = sdkRaycast.GlobalDirection;
                    break;
                default:
                    ray = default(Ray);
                    return false;
            }

            if (rayDirection == Vector3.zero)
            {
                ray = default(Ray);
                return false;
            }

            ray = new Ray(rayOrigin, rayDirection.normalized);
            return true;
        }

        public static void FillSDKRaycastHit(this RaycastHit target, Vector3 sceneRootPosition, in UnityEngine.RaycastHit unityHit, string colliderName, CRDTEntity crdtEntity,
            Vector3 globalOrigin,
            Vector3 direction)
        {
            target.EntityId = (uint)crdtEntity.Id;

            // There is no real value in passing MeshName
            target.MeshName = colliderName;
            target.Length = unityHit.distance;
            target.GlobalOrigin.Set(globalOrigin);
            target.Position.Set(ParcelMathHelper.GetSceneRelativePosition(unityHit.point, sceneRootPosition));
            target.NormalHit.Set(unityHit.normal);
            target.Direction.Set(direction);
        }
    }
}
