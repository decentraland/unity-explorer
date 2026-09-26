using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.CommonBehavior
{
    /// <summary>
    /// Represents Poolable behaviour of the map object
    /// </summary>
    internal struct MapMarkerPoolableBehavior<T> where T : MonoBehaviour
    {
        internal readonly IObjectPool<T> objectsPool;

        internal T? instance { get; private set; }

        internal bool isVisible { get; private set; }

        internal Vector3 currentPosition { get; private set; }

        internal MapMarkerPoolableBehavior(IObjectPool<T> objectsPool) : this()
        {
            this.objectsPool = objectsPool;
        }

        public void SetCurrentPosition(Vector3 pos)
        {
            currentPosition = pos;

            if (isVisible)
                instance.transform.localPosition = pos;
        }

        public T OnBecameVisible()
        {
            if (instance == null) { instance = objectsPool.Get(); }
            instance.transform.localPosition = currentPosition;
            isVisible = true;
            return instance;
        }

        public void OnBecameInvisible()
        {
            if (instance)
            {
                objectsPool.Release(instance);
                instance = null;
            }

            isVisible = false;
        }
    }
}
