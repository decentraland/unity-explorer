using DCL.Diagnostics;
using Sentry;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Utility
{
    public static class UnityObjectUtils
    {
        public static bool IsQuitting { get; private set; }

        [RuntimeInitializeOnLoadMethod]
        private static void StartTrackingApplicationStatus()
        {
            void SetQuitting()
            {
                IsQuitting = true;
                ReportHub.Log(LogType.Log, ReportCategory.ALWAYS, "Application is quitting");
            }

            Application.quitting += SetQuitting;
        }

        // This code fixes the following situation: enter play mode, exit play
        // mode, run edit mode tests.
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void ResetIsQuittingOnEnteredEditMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged +=
                static stateChange =>
            {
                if (stateChange ==
                    UnityEditor.PlayModeStateChange.EnteredEditMode)
                    IsQuitting = false;
            };
        }
#endif

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

        public static void SafeDestroy(Object? @object)
        {
            // Null reference check is fast in comparison to Unity's overloaded operator
            if (ReferenceEquals(@object, null)) return;

            // If Application is quitting component may be already invalid
            if (IsQuitting && !@object)
                return;

            if (!Application.isPlaying)
                Object.DestroyImmediate(@object);
            else
                Object.Destroy(@object);
        }

        public static void SelfDestroy(this Object @object) =>
            SafeDestroy(@object);

        /// <summary>
        /// Flag to enable/disable using shared materials at runtime.
        /// When enabled, GetSharedMaterials is used instead of GetMaterials to avoid creating material instances.
        /// Set to false to revert to the old behavior where GetMaterials creates instance copies.
        /// </summary>
        public static bool USE_SHARED_MATERIALS_AT_RUNTIME = true;

        /// <summary>
        ///     Gets materials from the renderer. By default uses GetSharedMaterials to avoid creating material instances.
        ///     When USE_SHARED_MATERIALS_AT_RUNTIME is false, uses GetMaterials which creates instance copies.
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
            if (USE_SHARED_MATERIALS_AT_RUNTIME)
                renderer.GetSharedMaterials(targetList);
            else
                renderer.GetMaterials(targetList);
        }
    }
}
