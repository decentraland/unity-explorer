using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Unity.Transforms.Components;
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
        private Vector3 compensatedParentScale;

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
            compensatedParentScale = Vector3.zero;
        }

        public void TryAssignArea(IComponentPool<SDKEntityTriggerArea> pool, Transform mainPlayerTransform, TransformComponent transformComponent)
        {
            bool useTransformScaleAsAreaSize = AreaSize == Vector3.zero;

            SDKEntityTriggerArea? area = monoBehaviour;

            if (area == null)
            {
                area = pool.Get();
                SetMonoBehaviour(area);

                Transform triggerAreaTransform = area.transform;
                triggerAreaTransform.SetParent(transformComponent.Transform, worldPositionStays: false);
                triggerAreaTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            if (useTransformScaleAsAreaSize)
                area.transform.localScale = Vector3.one;
            else
                CompensateParentScale(area.transform, transformComponent.Transform.lossyScale);

            // Re-applied on every assignment so a mask update rebinds or clears the filter.
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

        /// <summary>
        ///     Keeps an explicit area size independent of entity and parent scale changes made after the area was assigned
        /// </summary>
        public void TryUpdateParentScaleCompensation(Transform parent)
        {
            if (!hasMonoBehaviour || AreaSize == Vector3.zero) return;

            Vector3 parentScale = parent.lossyScale;

            if (parentScale == compensatedParentScale) return;

            CompensateParentScale(monoBehaviour!.transform, parentScale);
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

            // Same fast-path predicate as the initial setup.
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

        private void CompensateParentScale(Transform areaTransform, Vector3 parentScale)
        {
            compensatedParentScale = parentScale;
            areaTransform.localScale = new Vector3(InverseScale(parentScale.x), InverseScale(parentScale.y), InverseScale(parentScale.z));
        }

        // A zero parent axis collapses the area along it, like the parent itself
        private static float InverseScale(float scale) =>
            Mathf.Approximately(scale, 0f) ? 0f : 1f / scale;
    }
}
