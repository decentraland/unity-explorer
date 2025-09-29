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
        BOX,
        SPHERE
    }

    public struct SDKEntityTriggerAreaComponent : IDirtyMarker
    {
        private static readonly IReadOnlyCollection<Collider> EMPTY_COLLECTION = Array.Empty<Collider>();
        public SDKEntityTriggerArea? monoBehaviour { get; private set; }

        private readonly bool targetOnlyMainPlayer;
        private bool hasMonoBehaviour;

        public Vector3 AreaSize { get; private set; }
        public SDKEntityTriggerAreaMeshType MeshType { get; private set; }
        public ColliderLayer LayerMask { get; private set; }
        public bool IsDirty { get; set; }

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
            SDKEntityTriggerAreaMeshType meshType = SDKEntityTriggerAreaMeshType.BOX,
            ColliderLayer layerMask = ColliderLayer.ClPlayer)
        {
            AreaSize = areaSize;
            this.MeshType = meshType;
            this.LayerMask = layerMask;
            this.targetOnlyMainPlayer = targetOnlyMainPlayer;

            this.monoBehaviour = monoBehaviour;
            hasMonoBehaviour = monoBehaviour != null;

            IsDirty = true;
        }

        public void TryAssignArea(IComponentPool<SDKEntityTriggerArea> pool, Transform mainPlayerTransform, TransformComponent transformComponent)
        {
            bool useTransformScaleAsAreaSize = AreaSize == Vector3.zero;

            if (!hasMonoBehaviour)
            {
                SetMonoBehaviour(pool.Get());

                if (targetOnlyMainPlayer)
                    monoBehaviour!.TargetTransform = mainPlayerTransform;

                Transform triggerAreaTransform = monoBehaviour!.transform;
                triggerAreaTransform.SetParent(transformComponent.Transform, worldPositionStays: !useTransformScaleAsAreaSize);
                triggerAreaTransform.localPosition = Vector3.zero;
                triggerAreaTransform.localRotation = Quaternion.identity;
            }

            switch (MeshType)
            {
                case SDKEntityTriggerAreaMeshType.BOX:
                    monoBehaviour!.SphereCollider.enabled = false;
                    monoBehaviour.BoxCollider.enabled = true;
                    monoBehaviour.BoxCollider.size = useTransformScaleAsAreaSize ? Vector3.one : AreaSize;
                    break;
                case SDKEntityTriggerAreaMeshType.SPHERE:
                    monoBehaviour!.BoxCollider.enabled = false;
                    monoBehaviour.SphereCollider.enabled = true;
                    monoBehaviour.SphereCollider.radius = useTransformScaleAsAreaSize ? 0.5f : AreaSize.magnitude / 2;
                    break;
            }

            // Optimization to use an avatars specific physics layer if the trigger only cares about avatars
            monoBehaviour!.gameObject.layer = LayerMask == ColliderLayer.ClPlayer ? PhysicsLayers.ALL_AVATARS : PhysicsLayers.SDK_ENTITY_TRIGGER_AREA;
        }

        public void UpdateAreaSize(Vector3 size)
        {
            AreaSize = size;
            IsDirty = true;
        }

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
