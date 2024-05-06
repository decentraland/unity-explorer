using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
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
            var raycastHitPool = new ComponentPool.WithDefaultCtor<RaycastHit>(defaultCapacity: 100, onGet: c => c.Reset());

            // Add all SDK components here
            sdkComponentsRegistry
               .Add(SDKComponentBuilder<SDKTransform>.Create(ComponentID.TRANSFORM)
                                                     .WithPool(sdkTransform =>
                                                      {
                                                          sdkTransform.Clear();
                                                          SDKComponentBuilderExtensions.SetAsDirty(sdkTransform);
                                                      })
                                                     .WithCustomSerializer(new SDKTransformSerializer())
                                                     .Build())
               .Add(SDKComponentBuilder<PBGltfContainer>.Create(ComponentID.GLTF_CONTAINER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshCollider>.Create(ComponentID.MESH_COLLIDER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMeshRenderer>.Create(ComponentID.MESH_RENDERER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBTextShape>.Create(ComponentID.TEXT_SHAPE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBMaterial>.Create(ComponentID.MATERIAL).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBRaycast>.Create(ComponentID.RAYCAST).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBUiTransform>.Create(ComponentID.UI_TRANSFORM).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBUiText>.Create(ComponentID.UI_TEXT).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBUiBackground>.Create(ComponentID.UI_BACKGROUND).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBUiInput>.Create(ComponentID.UI_INPUT).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBUiInputResult>.Create(ComponentID.UI_INPUT_RESULT).AsProtobufResult())
               .Add(SDKComponentBuilder<PBUiDropdown>.Create(ComponentID.UI_DROPDOWN).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBUiDropdownResult>.Create(ComponentID.UI_DROPDOWN_RESULT).AsProtobufResult())
               .Add(SDKComponentBuilder<PBUiCanvasInformation>.Create(ComponentID.UI_CANVAS_INFORMATION).AsProtobufResult())

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
                                                                   if (pointerEventsResult.Hit != null)
                                                                   {
                                                                       raycastHitPool.Release(pointerEventsResult.Hit);
                                                                       pointerEventsResult.Hit = null;
                                                                   }
                                                               })
                                                              .Build())
               .Add(SDKComponentBuilder<PBPointerEvents>.Create(ComponentID.POINTER_EVENTS)
                                                        .WithProtobufSerializer()
                                                        .WithPool(
                                                             onGet: SDKComponentBuilderExtensions.SetAsDirty,
                                                             onRelease: pbe => pbe.Reset())
                                                        .Build())
               .Add(SDKComponentBuilder<PBVideoEvent>.Create(ComponentID.VIDEO_EVENT).AsProtobufResult())
               .Add(SDKComponentBuilder<PBCameraMode>.Create(ComponentID.CAMERA_MODE).AsProtobufResult())
               .Add(SDKComponentBuilder<PBPointerLock>.Create(ComponentID.POINTER_LOCK).AsProtobufResult())
               .Add(SDKComponentBuilder<PBBillboard>.Create(ComponentID.BILLBOARD).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBEngineInfo>.Create(ComponentID.ENGINE_INFO).AsProtobufResult())
               .Add(SDKComponentBuilder<PBVisibilityComponent>.Create(ComponentID.VISIBILITY_COMPONENT).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBGltfContainerLoadingState>.Create(ComponentID.GLTF_CONTAINER_LOADING_STATE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarShape>.Create(ComponentID.AVATAR_SHAPE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAudioSource>.Create(ComponentID.AUDIO_SOURCE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAudioStream>.Create(ComponentID.AUDIO_STREAM).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBTween>.Create(ComponentID.TWEEN).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBTweenState>.Create(ComponentID.TWEEN_STATE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBVideoPlayer>.Create(ComponentID.VIDEO_PLAYER).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarAttach>.Create(ComponentID.AVATAR_ATTACH).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAnimator>.Create(ComponentID.ANIMATOR).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBCameraModeArea>.Create(ComponentID.CAMERA_MODE_AREA).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarModifierArea>.Create(ComponentID.AVATAR_MODIFIER_AREA).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBPlayerIdentityData>.Create(ComponentID.PLAYER_IDENTITY_DATA).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarBase>.Create(ComponentID.AVATAR_BASE).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarEquippedData>.Create(ComponentID.AVATAR_EQUIPPED_DATA).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBAvatarEmoteCommand>.Create(ComponentID.AVATAR_EMOTE_COMMAND).AsProtobufComponent())
               .Add(SDKComponentBuilder<PBRealmInfo>.Create(ComponentID.REALM_INFO).AsProtobufResult());

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
            yield return (typeof(PartitionComponent), new ComponentPool.WithDefaultCtor<PartitionComponent>(defaultCapacity: 2000, onRelease: p =>
            {
                p.IsBehind = false;
                p.Bucket = byte.MaxValue;
                p.RawSqrDistance = 0;
            }));
        }
    }
}
