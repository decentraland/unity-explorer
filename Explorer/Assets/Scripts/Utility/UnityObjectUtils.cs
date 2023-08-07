using UnityEngine;

namespace Utility
{
    public static class UnityObjectUtils
    {
        private static bool isQuitting;

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

        public static bool IsQuitting => Application.isPlaying && isQuitting;

        /// <summary>
        ///     Tries to destroy Game Object based on the current state of the Application
        /// </summary>
        public static void SafeDestroyGameObject<T>(T component) where T: Component
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
    }
}
