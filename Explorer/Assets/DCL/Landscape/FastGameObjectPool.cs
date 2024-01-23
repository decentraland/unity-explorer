using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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

        public void Prewarm(int count)
        {
            List<Transform> tempList = ListPool<Transform>.Get();

            for (var i = 0; i < count; i++)
                tempList.Add(gameObjectPool.Get());

            for (var i = 0; i < count; i++)
                gameObjectPool.Release(tempList[i]);

            ListPool<Transform>.Release(tempList);
        }
    }
}
