using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using DCL.ECS7;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
               .Add(SDKComponentBuilder<PBGltfContainerLoadingState>.Create(ComponentID.GLTF_CONTAINER_LOADING_STATE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarShape>.Create(ComponentID.AVATAR_SHAPE).AsProtobufComponent());

            Transform rootContainer = new GameObject("ROOT_POOL_CONTAINER").transform;
            // add others as required

            var componentPoolsRegistry = new ComponentPoolsRegistry(

                // merge SDK components with Non-SDK
                sdkComponentsRegistry.SdkComponents
                                     .Select(c => (c.ComponentType, c.Pool))
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
