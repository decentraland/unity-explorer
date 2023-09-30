using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.Components.Transform;
using DCL.ECS7;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;
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
               .Add(SDKComponentBuilder<PBPointerEvents>.Create(ComponentID.POINTER_EVENTS).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBCameraMode>.Create(ComponentID.CAMERA_MODE).AsProtobufResult())
               .Add(SDKComponentBuilder<PBPointerLock>.Create(ComponentID.POINTER_LOCK).AsProtobufResult())
               .Add(SDKComponentBuilder<PBBillboard>.Create(ComponentID.BILLBOARD).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBEngineInfo>.Create(ComponentID.ENGINE_INFO).AsProtobufResult())
               .Add(SDKComponentBuilder<PBVisibilityComponent>.Create(ComponentID.VISIBILITY_COMPONENT).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBGltfContainerLoadingState>.Create(ComponentID.GLTF_CONTAINER_LOADING_STATE).AsProtobufComponent());

            // add others as required

            var componentPoolsRegistry = new ComponentPoolsRegistry(

                // merge SDK components with Non-SDK
                sdkComponentsRegistry.SdkComponents
                                     .Select(c => (c.ComponentType, c.Pool))
                                     .Append((typeof(RaycastHit), raycastHitPool))
                                     .Concat(GetMiscComponents())
                                     .Concat(GetPrimitivesMeshesDictionary())
                                     .ToDictionary(x => x.Item1, x => x.Item2));

            return new ComponentsContainer { SDKComponentsRegistry = sdkComponentsRegistry, ComponentPoolsRegistry = componentPoolsRegistry };
        }

        private static IEnumerable<(Type type, IComponentPool pool)> GetPrimitivesMeshesDictionary()
        {
            (Type type, IComponentPool pool) CreateExtraComponentPool<T>(Action<T> onGet = null, Action<T> onRelease = null) where T: class, new() =>
                (typeof(T), new ComponentPool<T>(onGet, onRelease));

            yield return CreateExtraComponentPool<BoxPrimitive>();
            yield return CreateExtraComponentPool<SpherePrimitive>();
            yield return CreateExtraComponentPool<PlanePrimitive>();
            yield return CreateExtraComponentPool<CylinderPrimitive>();
        }

        private static IEnumerable<(Type type, IComponentPool pool)> GetMiscComponents()
        {
            Transform rootContainer = new GameObject("ROOT_POOL_CONTAINER").transform;

            (Type type, IComponentPool pool) CreateComponentPool<T>(Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 1024) where T: Component =>
                (typeof(T), new UnityComponentPool<T>(rootContainer, creationHandler, onRelease, maxSize: maxSize));

            yield return CreateComponentPool<Transform>();

            // Primitive Colliders
            yield return CreateComponentPool<MeshCollider>();
            yield return CreateComponentPool<BoxCollider>();
            yield return CreateComponentPool<SphereCollider>();
            yield return CreateComponentPool(MeshRendererPoolUtils.CreateMeshRendererComponent, MeshRendererPoolUtils.ReleaseMeshRendererComponent);

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
