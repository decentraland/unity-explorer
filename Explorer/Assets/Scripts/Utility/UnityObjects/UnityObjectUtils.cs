using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Utility
{
    public static class UnityObjectUtils
    {
        private static bool isQuitting;

        // Can't check Application.isPlaying if called from the background thread
        public static bool IsQuitting => (!PlayerLoopHelper.IsMainThread || Application.isPlaying) && isQuitting;

        [RuntimeInitializeOnLoadMethod]
        private static void StartTrackingApplicationStatus()
        {
            void SetQuitting()
            {
                isQuitting = true;
                Application.quitting -= SetQuitting;
            }

            Application.quitting += SetQuitting;
        }

        /// <summary>
        ///     Tries to destroy Game Object based on the current state of the Application
        /// </summary>
        public static void SafeDestroyGameObject<T>(T? component) where T: Component
        {
            // If Application is quitting component may be already invalid
            if (IsQuitting && (!component || !component.gameObject))
                return;

            if (!Application.isPlaying)
                Object.DestroyImmediate(component.gameObject);
            else
                Object.Destroy(component.gameObject);
        }

        public static void SafeDestroy(Object @object)
        {
            // If Application is quitting component may be already invalid
            if (IsQuitting && !@object)
                return;

            if (!Application.isPlaying)
                Object.DestroyImmediate(@object);
            else
                Object.Destroy(@object);
        }

        /// <summary>
        ///     Gets shared materials instead of materials if called when Application is not playing (from tests)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SafeGetMaterials(this Renderer renderer, List<Material> targetList)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                renderer.GetSharedMaterials(targetList);
                return;
            }
#endif
            renderer.GetMaterials(targetList);
        }
    }
}
