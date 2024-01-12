using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.Components.Transform;
using DCL.ECS7;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace Global
{
    /// <summary>
    ///     Registers all components that should exist in the ECS
    /// </summary>
    public struct ComponentsContainer
    {
        public ISDKComponentsRegistry SDKComponentsRegistry { get; private set; }

        public IComponentPoolsRegistry ComponentPoolsRegistry { get; private set; }

        public static ComponentsContainer Create()
        {
            var sdkComponentsRegistry = new SDKComponentsRegistry();

            // SDK RaycastHit (used only as an element in the list)
            var raycastHitPool = new ComponentPool<RaycastHit>(defaultCapacity: 100, onGet: c => c.Reset());

            // Add all SDK components here
            sdkComponentsRegistry
               .Add(SDKComponentBuilder<SDKTransform>.Create(ComponentID.TRANSFORM).WithPool(SDKComponentBuilderExtensions.SetAsDirty).WithCustomSerializer(new SDKTransformSerializer()).Build())
               .Add(SDKComponentBuilder<PBGltfContainer>.Create(ComponentID.GLTF_CONTAINER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshCollider>.Create(ComponentID.MESH_COLLIDER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshRenderer>.Create(ComponentID.MESH_RENDERER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBTextShape>.Create(ComponentID.TEXT_SHAPE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMaterial>.Create(ComponentID.MATERIAL).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBRaycast>.Create(ComponentID.RAYCAST).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBUiTransform>.Create(ComponentID.UI_TRANSFORM).AsProtobufComponent(true))
               .Add(SDKComponentBuilder<PBUiText>.Create(ComponentID.UI_TEXT).AsProtobufComponent())

                // Special logic for pooling/releasing PBRaycastResult
               .Add(SDKComponentBuilder<PBRaycastResult>.Create(ComponentID.RAYCAST_RESULT)
                                                        .WithProtobufSerializer()
                                                        .WithPool(onGet: raycastHitResult => raycastHitResult.Reset(),
                                                             onRelease: raycastHitResult =>
                                                             {
                                                                 // Return hits to their own pool
                                                                 for (var i = 0; i < raycastHitResult.Hits.Count; i++)
                                                                     raycastHitPool.Release(raycastHitResult.Hits[i]);
                                                             })
                                                        .Build())
               .Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT)
                                                              .WithProtobufSerializer()
                                                              .WithPool(onRelease: pointerEventsResult =>
                                                               {
                                                                   raycastHitPool.Release(pointerEventsResult.Hit);
                                                                   pointerEventsResult.Hit = null;
                                                               })
                                                              .Build())
               .Add(SDKComponentBuilder<PBPointerEvents>.Create(ComponentID.POINTER_EVENTS)
                                                        .WithProtobufSerializer()
                                                        .WithPool(
                                                             onGet: SDKComponentBuilderExtensions.SetAsDirty,
                                                             onRelease: pbe => pbe.Reset())
                                                        .Build())
               .Add(SDKComponentBuilder<PBCameraMode>.Create(ComponentID.CAMERA_MODE).AsProtobufResult())
               .Add(SDKComponentBuilder<PBPointerLock>.Create(ComponentID.POINTER_LOCK).AsProtobufResult())
               .Add(SDKComponentBuilder<PBBillboard>.Create(ComponentID.BILLBOARD).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBEngineInfo>.Create(ComponentID.ENGINE_INFO).AsProtobufResult())
               .Add(SDKComponentBuilder<PBVisibilityComponent>.Create(ComponentID.VISIBILITY_COMPONENT).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBGltfContainerLoadingState>.Create(ComponentID.GLTF_CONTAINER_LOADING_STATE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarShape>.Create(ComponentID.AVATAR_SHAPE).AsProtobufComponent())
                .Add(SDKComponentBuilder<PBAudioSource>.Create(ComponentID.AUDIO_SOURCE).AsProtobufComponent());

            Transform rootContainer = new GameObject("ROOT_POOL_CONTAINER").transform;

            // add others as required

            var componentPoolsRegistry = new ComponentPoolsRegistry(

                // merge SDK components with Non-SDK
                sdkComponentsRegistry.SdkComponents
                                     .Select(c => (c.ComponentType, c.Pool))
                                     .Append((typeof(RaycastHit), raycastHitPool))
                                     .Concat(GetMiscComponents())
                                     .ToDictionary(x => x.Item1, x => x.Item2), rootContainer);

            return new ComponentsContainer { SDKComponentsRegistry = sdkComponentsRegistry, ComponentPoolsRegistry = componentPoolsRegistry };
        }

        private static IEnumerable<(Type type, IComponentPool pool)> GetMiscComponents()
        {
            // Partition Component
            yield return (typeof(PartitionComponent), new ComponentPool<PartitionComponent>(defaultCapacity: 2000, onRelease: p =>
            {
                p.IsBehind = false;
                p.Bucket = byte.MaxValue;
                p.RawSqrDistance = 0;
            }));
        }
    }
}
