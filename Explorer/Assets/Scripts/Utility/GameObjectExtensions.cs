using UnityEngine;

namespace Utility
{
    public static class GameObjectExtensions
    {
        public static T TryAddComponent<T>(this GameObject gameObject) where T: Component
        {
            T component = gameObject.GetComponent<T>();
            return component ? component : gameObject.AddComponent<T>();
        }
    }
}
