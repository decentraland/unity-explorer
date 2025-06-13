using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.Utilities
{
    public static class GameObjectExtensions
    {
        public static void ThreadSafeSetActive(this GameObject gameObject, bool isActive)
        {
            if (!UniTask.IsMainThread())
            {
                ThreadSafeSetActiveAsync(gameObject, isActive).Forget();
                return;
            }
            
            gameObject.SetActive(isActive);
        }

        private static async UniTaskVoid ThreadSafeSetActiveAsync(GameObject gameObject, bool isActive)
        {
            await UniTask.SwitchToMainThread();
            gameObject.SetActive(isActive);
        }
    }
} 