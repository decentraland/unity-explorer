using UnityEngine;

namespace Utility
{
    public static class GameObjectExtensions
    {
        public static readonly WaitForEndOfFrame WAIT_FOR_END_OF_FRAME = new ();

        public static T TryAddComponent<T>(this GameObject gameObject) where T: Component
        {
            T component = gameObject.GetComponent<T>();
            return component ? component : gameObject.AddComponent<T>();
        }

        public static void ResetLocalTRS(this Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
    }
}
