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
        private static readonly IReadOnlyCollection<Transform> EMPTY_COLLECTION = Array.Empty<Transform>();
        public SDKEntityTriggerArea? monoBehaviour { get; private set; }

        private readonly bool targetOnlyMainPlayer;
        private bool hasMonoBehaviour;

        public Vector3 AreaSize { get; private set; }
        public SDKEntityTriggerAreaMeshType MeshType { get; private set; }
        public bool IsDirty { get; set; }

        public readonly IReadOnlyCollection<Transform> EnteredAvatarsToBeProcessed => hasMonoBehaviour
            ? monoBehaviour!.EnteredAvatarsToBeProcessed
            : EMPTY_COLLECTION;

        public readonly IReadOnlyCollection<Transform> ExitedAvatarsToBeProcessed => hasMonoBehaviour
            ? monoBehaviour!.ExitedAvatarsToBeProcessed
            : EMPTY_COLLECTION;

        public readonly IReadOnlyCollection<Transform> CurrentAvatarsInside => hasMonoBehaviour
            ? monoBehaviour!.CurrentAvatarsInside
            : EMPTY_COLLECTION;

        public SDKEntityTriggerAreaComponent(Vector3 areaSize, bool targetOnlyMainPlayer = false, SDKEntityTriggerArea? monoBehaviour = null, SDKEntityTriggerAreaMeshType meshType = SDKEntityTriggerAreaMeshType.BOX)
        {
            AreaSize = areaSize;
            this.MeshType = meshType;
            this.targetOnlyMainPlayer = targetOnlyMainPlayer;

            this.monoBehaviour = monoBehaviour;
            hasMonoBehaviour = monoBehaviour != null;

            IsDirty = true;
        }

        public void TryAssignArea(IComponentPool<SDKEntityTriggerArea> pool, Transform mainPlayerTransform, TransformComponent transformComponent)
        {
            if (hasMonoBehaviour)
            {
                switch (MeshType)
                {
                    case SDKEntityTriggerAreaMeshType.BOX:
                        monoBehaviour!.SphereCollider.enabled = false;
                        monoBehaviour!.BoxCollider.enabled = true;
                        break;
                    case SDKEntityTriggerAreaMeshType.SPHERE:
                        monoBehaviour!.BoxCollider.enabled = false;
                        monoBehaviour!.SphereCollider.enabled = true;
                        break;
                }
            }

            if (IsDirty == false) return;
            IsDirty = false;

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
                    monoBehaviour!.BoxCollider.enabled = true;
                    monoBehaviour!.BoxCollider.size = useTransformScaleAsAreaSize ? Vector3.one : AreaSize;
                    break;
                case SDKEntityTriggerAreaMeshType.SPHERE:
                    monoBehaviour!.BoxCollider.enabled = false;
                    monoBehaviour!.SphereCollider.enabled = true;
                    monoBehaviour!.SphereCollider.radius = useTransformScaleAsAreaSize ? 0.5f : AreaSize.magnitude / 2;
                    break;
            }
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
            monoBehaviour?.ClearEnteredAvatarsToBeProcessed();

        public void TryClearExitedAvatarsToBeProcessed() =>
            monoBehaviour?.ClearExitedAvatarsToBeProcessed();

        public bool TryDispose(ISceneStateProvider sceneStateProvider)
        {
            if (!sceneStateProvider.IsCurrent && hasMonoBehaviour)
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
