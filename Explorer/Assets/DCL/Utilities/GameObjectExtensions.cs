using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.Utilities
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Sets the GameObject's active state, ensuring it happens on the main thread.
        /// If called from a non-main thread, it will switch to the main thread asynchronously.
        /// </summary>
        public static void SetActiveOnMainThread(this GameObject gameObject, bool isActive)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                SetActiveOnMainThreadAsync(gameObject, isActive).Forget();
                return;
            }

            gameObject.SetActive(isActive);
        }

        private static async UniTaskVoid SetActiveOnMainThreadAsync(GameObject gameObject, bool isActive)
        {
            await UniTask.SwitchToMainThread();
            gameObject.SetActive(isActive);
        }
    }
}
