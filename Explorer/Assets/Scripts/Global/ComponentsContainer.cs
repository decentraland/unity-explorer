using CrdtEcsBridge.Components;
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

namespace Global
{
    /// <summary>
    /// Registers all components that should exist in the ECS
    /// </summary>
    public struct ComponentsContainer
    {
        public ISDKComponentsRegistry SDKComponentsRegistry { get; private set; }

        public IComponentPoolsRegistry ComponentPoolsRegistry { get; private set; }

        public static ComponentsContainer Create()
        {
            var sdkComponentsRegistry = new SDKComponentsRegistry();

            // Add all SDK components here
            sdkComponentsRegistry
               .Add(SDKComponentBuilder<SDKTransform>.Create(ComponentID.TRANSFORM).WithPool(SDKComponentBuilderExtensions.SetAsDirty).WithCustomSerializer(new SDKTransformSerializer()).Build())
               .Add(SDKComponentBuilder<PBGltfContainer>.Create(ComponentID.GLTF_CONTAINER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshCollider>.Create(ComponentID.MESH_COLLIDER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshRenderer>.Create(ComponentID.MESH_RENDERER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBTextShape>.Create(ComponentID.TEXT_SHAPE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMaterial>.Create(ComponentID.MATERIAL).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBPointerEvents>.Create(ComponentID.POINTER_EVENTS).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBBillboard>.Create(ComponentID.BILLBOARD).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBVisibilityComponent>.Create(ComponentID.VISIBILITY_COMPONENT).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBGltfContainerLoadingState>.Create(ComponentID.GLTF_CONTAINER_LOADING_STATE).AsProtobufComponent());

            // add others as required

            var componentPoolsRegistry = new ComponentPoolsRegistry(

                // merge SDK components with Non-SDK
                sdkComponentsRegistry.SdkComponents
                                     .Select(c => (c.ComponentType, c.Pool))
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
