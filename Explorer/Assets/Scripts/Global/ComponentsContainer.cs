using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.ECS7;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Global
{
    /// <summary>
    /// Registers all components that should exist in the ECS
    /// </summary>
    public readonly struct ComponentsContainer
    {
        public ISDKComponentsRegistry SDKComponentsRegistry { get; internal init; }

        public IComponentPoolsRegistry ComponentPoolsRegistry { get; internal init; }

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
               .Add(SDKComponentBuilder<PBBillboard>.Create(ComponentID.BILLBOARD).AsProtobufComponent());

            // add others as required

            var componentPoolsRegistry = new ComponentPoolsRegistry(
                // merge SDK components with Non-SDK, currently there are SDK only
                sdkComponentsRegistry.SdkComponents.ToDictionary(bridge => bridge.ComponentType, bridge => bridge.Pool)
                                     .Concat(GetUnityComponentDictionary())
                                     .ToDictionary(x => x.Key, x => x.Value));

            return new ComponentsContainer { SDKComponentsRegistry = sdkComponentsRegistry, ComponentPoolsRegistry = componentPoolsRegistry };
        }

        private static Dictionary<Type, IComponentPool> GetUnityComponentDictionary()
        {
            Transform rootContainer = new GameObject("ROOT_POOL_CONTAINER").transform;

            return new Dictionary<Type, IComponentPool> { { typeof(GameObject), new UnityGameObjectPool(rootContainer) } };
        }

    }
}
