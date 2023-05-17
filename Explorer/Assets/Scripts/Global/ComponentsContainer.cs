using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.ECS7;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.Unity.Components;
using System.Linq;

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
            var unityComponentsRegistry = new UnityComponentsRegistry();

            // Add all SDK components here
            sdkComponentsRegistry
               .Add(SDKComponentBuilder<SDKTransform>.Create(ComponentID.TRANSFORM).WithDirtyablePool().WithCustomSerializer(new SDKTransformSerializer()).Build())
               .Add(SDKComponentBuilder<PBGltfContainer>.Create(ComponentID.GLTF_CONTAINER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshCollider>.Create(ComponentID.MESH_COLLIDER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshRenderer>.Create(ComponentID.MESH_RENDERER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBTextShape>.Create(ComponentID.TEXT_SHAPE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMaterial>.Create(ComponentID.MATERIAL).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBPointerEvents>.Create(ComponentID.POINTER_EVENTS).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBBillboard>.Create(ComponentID.BILLBOARD).AsProtobufComponent());

            unityComponentsRegistry
               .Add(new UnityTransformHandler());

            // add others as required

            var componentPoolsRegistry = new ComponentPoolsRegistry(
                // merge SDK components with Non-SDK, currently there are SDK only
                sdkComponentsRegistry.SdkComponents.ToDictionary(bridge => bridge.ComponentType, bridge => bridge.Pool)
                                     .Concat(unityComponentsRegistry.unityComponents)
                                     .ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            return new ComponentsContainer { SDKComponentsRegistry = sdkComponentsRegistry, ComponentPoolsRegistry = componentPoolsRegistry };
        }

    }
}
