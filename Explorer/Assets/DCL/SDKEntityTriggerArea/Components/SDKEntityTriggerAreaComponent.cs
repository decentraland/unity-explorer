using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKEntityTriggerArea.Components
{
    public enum SDKEntityTriggerAreaMeshType
    {
        Box,
        Sphere
    }

    public struct SDKEntityTriggerAreaComponent : IDirtyMarker
    {
        private static readonly IReadOnlyCollection<Collider> EMPTY_COLLECTION = Array.Empty<Collider>();
        public SDKEntityTriggerArea? monoBehaviour { get; private set; }

        private bool targetOnlyMainPlayer;
        private bool hasMonoBehaviour;
        private uint incrementalTick;

        public Vector3 AreaSize { get; private set; }
        public SDKEntityTriggerAreaMeshType MeshType { get; private set; }
        public ColliderLayer LayerMask { get; private set; }
        public bool IsDirty { get; set; }
        public uint IncrementalTick => incrementalTick++;

        public readonly IReadOnlyCollection<Collider> EnteredEntitiesToBeProcessed => hasMonoBehaviour
            ? monoBehaviour!.EnteredEntitiesToBeProcessed
            : EMPTY_COLLECTION;

        public readonly IReadOnlyCollection<Collider> ExitedEntitiesToBeProcessed => hasMonoBehaviour
            ? monoBehaviour!.ExitedEntitiesToBeProcessed
            : EMPTY_COLLECTION;

        public readonly IReadOnlyCollection<Collider> CurrentEntitiesInside => hasMonoBehaviour
            ? monoBehaviour!.CurrentEntitiesInside
            : EMPTY_COLLECTION;

        public SDKEntityTriggerAreaComponent(
            Vector3 areaSize,
            bool targetOnlyMainPlayer = false,
            SDKEntityTriggerArea? monoBehaviour = null,
            SDKEntityTriggerAreaMeshType meshType = SDKEntityTriggerAreaMeshType.Box,
            ColliderLayer layerMask = ColliderLayer.ClPlayer)
        {
            AreaSize = areaSize;
            this.MeshType = meshType;
            this.LayerMask = layerMask;
            this.targetOnlyMainPlayer = targetOnlyMainPlayer;

            this.monoBehaviour = monoBehaviour;
            hasMonoBehaviour = monoBehaviour != null;

            IsDirty = true;
            incrementalTick = 0;
        }

        public void TryAssignArea(IComponentPool<SDKEntityTriggerArea> pool, Transform mainPlayerTransform, TransformComponent transformComponent)
        {
            bool useTransformScaleAsAreaSize = AreaSize == Vector3.zero;

            if (monoBehaviour is not { } area)
            {
                area = pool.Get();
                SetMonoBehaviour(area);

                Transform triggerAreaTransform = area.transform;
                triggerAreaTransform.SetParent(transformComponent.Transform, worldPositionStays: !useTransformScaleAsAreaSize);
                triggerAreaTransform.localPosition = Vector3.zero;
                triggerAreaTransform.localRotation = Quaternion.identity;
            }

            // TargetTransform mirrors targetOnlyMainPlayer on every (re)assignment, so a mask
            // update that toggles the main-player fast path rebinds or clears the filter;
            // binding also evicts insiders that the filter stops tracking.
            area.SetTargetTransform(targetOnlyMainPlayer ? mainPlayerTransform : null);

            switch (MeshType)
            {
                case SDKEntityTriggerAreaMeshType.Box:
                    area.SphereCollider.enabled = false;
                    area.BoxCollider.enabled = true;
                    area.BoxCollider.size = useTransformScaleAsAreaSize ? Vector3.one : AreaSize;
                    break;
                case SDKEntityTriggerAreaMeshType.Sphere:
                    area.BoxCollider.enabled = false;
                    area.SphereCollider.enabled = true;
                    area.SphereCollider.radius = useTransformScaleAsAreaSize ? 0.5f : AreaSize.magnitude / 2;
                    break;
            }

            // Route purely-avatar masks (CL_PLAYER and/or CL_MAIN_PLAYER, no other bits) to
            // SDK_AVATAR_TRIGGER_AREA. Any mixed mask falls back to SDK_ENTITY_TRIGGER_AREA so
            // the trigger box's matrix cells also reach the non-avatar layers the mask targets.
            area.gameObject.layer = PhysicsLayers.IsAvatarOnlyMask(LayerMask)
                ? PhysicsLayers.SDK_AVATAR_TRIGGER_AREA
                : PhysicsLayers.SDK_ENTITY_TRIGGER_AREA;
        }

        public void UpdateAreaSize(Vector3 size)
        {
            AreaSize = size;
            IsDirty = true;
        }

        public void UpdateMaskAndMeshType(ColliderLayer layerMask, SDKEntityTriggerAreaMeshType meshType)
        {
            LayerMask = layerMask;
            MeshType = meshType;

            // Same fast-path predicate as the mask evaluation at setup: only an EXACTLY
            // CL_MAIN_PLAYER mask may filter colliders down to the local player transform.
            targetOnlyMainPlayer = layerMask == ColliderLayer.ClMainPlayer;
            IsDirty = true;
        }

        public readonly bool IsEnterPending(Collider entityCollider) =>
            monoBehaviour?.IsEnterPending(entityCollider) ?? false;

        public void TryRelease(IComponentPool<SDKEntityTriggerArea> pool)
        {
            if (!hasMonoBehaviour) return;

            pool.Release(monoBehaviour!);
            monoBehaviour = null;
            hasMonoBehaviour = false;
        }

        public void TryClear() => monoBehaviour?.Clear();

        public void TryClearEnteredAvatarsToBeProcessed() =>
            monoBehaviour?.ClearEnteredEntitiesToBeProcessed();

        public void TryClearExitedAvatarsToBeProcessed() =>
            monoBehaviour?.ClearExitedEntitiesToBeProcessed();

        public bool TryDispose()
        {
            if (hasMonoBehaviour)
            {
                monoBehaviour!.Dispose();
                return true;
            }

            return false;
        }

        internal void SetMonoBehaviour(SDKEntityTriggerArea newMonoBehaviour)
        {
            monoBehaviour = newMonoBehaviour;
            hasMonoBehaviour = newMonoBehaviour != null;
        }
    }
}
