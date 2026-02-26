using Cysharp.Threading.Tasks;
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

        /// <summary>
        ///     Sets the Component's GameObject active state, ensuring it happens on the main thread.
        ///     If called from a non-main thread, it will switch to the main thread asynchronously.
        /// </summary>
        public static void SetActiveOnMainThread(this Component component, bool isActive)
        {
            if (component == null) return;

            if (!PlayerLoopHelper.IsMainThread)
            {
                SetActiveOnMainThreadAsync(component, isActive).Forget();
                return;
            }

            component.gameObject.SetActive(isActive);
        }

        private static async UniTaskVoid SetActiveOnMainThreadAsync(Component component, bool isActive)
        {
            await UniTask.SwitchToMainThread();

            if (component != null)
                component.gameObject.SetActive(isActive);
        }
    }
}
