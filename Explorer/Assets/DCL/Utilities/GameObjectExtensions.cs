using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.Utilities
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Sets the Component's GameObject active state, ensuring it happens on the main thread.
        /// If called from a non-main thread, it will switch to the main thread asynchronously.
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
