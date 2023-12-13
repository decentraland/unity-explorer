using ECS.ComponentsPooling;
using System;
using UnityEngine;

namespace DCL.Landscape
{
    public class FastGameObjectPool : GameObjectPool<Transform>
    {
        public FastGameObjectPool(Transform rootContainer, Func<Transform> creationHandler = null, Action<Transform> onRelease = null, int maxSize = 2048, bool collectionCheck = true)
            : base(rootContainer, creationHandler, onRelease, maxSize, collectionCheck) { }

        protected override void OnHandleRelease(Transform transform)
        {
            transform.position = Vector3.one * -9999;
        }
    }
}
