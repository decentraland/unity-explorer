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
        private readonly List<Transform> disposedCollection;
        public Vector3 AreaSize;
        private CharacterTriggerArea? monoBehaviour;
        private readonly bool targetOnlyMainPlayer;

        public readonly IReadOnlyCollection<Transform> EnteredThisFrame => monoBehaviour != null
            ? monoBehaviour.EnteredThisFrame
            : EMPTY_COLLECTION;

        public readonly IReadOnlyCollection<Transform> ExitedThisFrame => monoBehaviour != null
            ? monoBehaviour.ExitedThisFrame
            : EMPTY_COLLECTION;

        public readonly IReadOnlyCollection<Transform> CurrentAvatarsInside => monoBehaviour != null
            ? monoBehaviour.CurrentAvatarsInside :
            disposedCollection.Count > 0 ? disposedCollection : EMPTY_COLLECTION;

        public CharacterTriggerAreaComponent(Vector3 areaSize, bool targetOnlyMainPlayer = false, CharacterTriggerArea? monoBehaviour = null)
        {
            AreaSize = areaSize;
            this.targetOnlyMainPlayer = targetOnlyMainPlayer;

            this.monoBehaviour = monoBehaviour;

            IsDirty = true;
            disposedCollection = new List<Transform>();
        }

        public void ForceAssignArea(CharacterTriggerArea characterTriggerArea)
        {
            monoBehaviour = characterTriggerArea;
        }

        public void TryAssignArea(IComponentPool<CharacterTriggerArea> pool, Transform mainPlayerTransform)
        {
            if (IsDirty == false) return;

            IsDirty = false;

            if (monoBehaviour == null)
            {
                monoBehaviour = pool.Get();

                if (targetOnlyMainPlayer)
                    monoBehaviour!.TargetTransform = mainPlayerTransform;
            }

            monoBehaviour!.BoxCollider.size = AreaSize;
        }

        public readonly void TryUpdateTransform(ref TransformComponent transformComponent)
        {
            if (monoBehaviour == null)
                return;

            Transform triggerAreaTransform = monoBehaviour.transform;

            if (transformComponent.Cached.WorldPosition != triggerAreaTransform.position)
                triggerAreaTransform.position = transformComponent.Cached.WorldPosition;

            if (transformComponent.Cached.WorldRotation != triggerAreaTransform.rotation)
                triggerAreaTransform.rotation = transformComponent.Cached.WorldRotation;

            if (!monoBehaviour.BoxCollider.enabled)
                monoBehaviour.BoxCollider.enabled = true;
        }

        public void TryRelease(IComponentPool<CharacterTriggerArea> pool)
        {
            if (monoBehaviour != null)
            {
                if (CurrentAvatarsInside.Count > 0)
                    disposedCollection.AddRange(CurrentAvatarsInside);

                pool.Release(monoBehaviour);
                monoBehaviour = null;
            }
        }

        public void TryClear()
        {
            if (monoBehaviour != null)
                monoBehaviour.Clear();

            disposedCollection.Clear();
        }

        public bool TryDispose(ISceneStateProvider sceneStateProvider)
        {
            if (!sceneStateProvider.IsCurrent && monoBehaviour != null)
            {
                monoBehaviour.Dispose();
                return true;
            }

            return false;
        }

        public bool IsDirty { get; set; }
    }
}
