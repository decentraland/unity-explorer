using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Components
{
    public struct CharacterTriggerAreaComponent : IDirtyMarker
    {
        private static readonly IReadOnlyCollection<Transform> EMPTY_COLLECTION = Array.Empty<Transform>();
        public CharacterTriggerArea? monoBehaviour { get; private set; }

        private readonly bool targetOnlyMainPlayer;
        private bool hasMonoBehaviour;

        public Vector3 AreaSize { get; private set; }
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

        public CharacterTriggerAreaComponent(Vector3 areaSize, bool targetOnlyMainPlayer = false, CharacterTriggerArea? monoBehaviour = null)
        {
            AreaSize = areaSize;
            this.targetOnlyMainPlayer = targetOnlyMainPlayer;

            this.monoBehaviour = monoBehaviour;
            hasMonoBehaviour = monoBehaviour != null;

            IsDirty = true;
        }

        public void TryAssignArea(IComponentPool<CharacterTriggerArea> pool, Transform mainPlayerTransform, TransformComponent transformComponent)
        {
            if (hasMonoBehaviour && !monoBehaviour!.BoxCollider.enabled)
                monoBehaviour.BoxCollider.enabled = true;

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

            monoBehaviour!.BoxCollider.size = useTransformScaleAsAreaSize ? Vector3.one : AreaSize;
            monoBehaviour.BoxCollider.enabled = true;
        }

        public void UpdateAreaSize(Vector3 size)
        {
            AreaSize = size;
            IsDirty = true;
        }

        public void TryRelease(IComponentPool<CharacterTriggerArea> pool)
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

        internal void SetMonoBehaviour(CharacterTriggerArea newMonoBehaviour)
        {
            monoBehaviour = newMonoBehaviour;
            hasMonoBehaviour = newMonoBehaviour != null;
        }
    }
}
