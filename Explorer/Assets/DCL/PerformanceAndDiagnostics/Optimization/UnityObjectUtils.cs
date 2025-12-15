using DCL.Diagnostics;
using Sentry;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
                SentrySdk.AddBreadcrumb("Application is quitting");
            }

            Application.quitting += SetQuitting;
        }

        // This code fixes the following situation: enter play mode, exit play
        // mode, run edit mode tests.
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void ResetIsQuittingOnEnteredEditMode()
        {
            EditorApplication.playModeStateChanged +=
                static stateChange =>
                {
                    if (stateChange ==
                        PlayModeStateChange.EnteredEditMode)
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
