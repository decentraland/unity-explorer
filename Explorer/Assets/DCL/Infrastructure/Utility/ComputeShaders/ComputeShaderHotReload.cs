using JetBrains.Annotations;
using System.Collections.Generic;
using System.Diagnostics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Utility.ComputeShaders
{
    public static class ComputeShaderHotReload
    {
        public delegate void OnComputeShaderReload(ComputeShader shader);

        private static readonly Dictionary<ComputeShader, Subscription> SUBSCRIPTIONS = new ();

        /// <summary>
        ///     The callback will be called when the shader is imported in the Editor
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void Subscribe(ComputeShader asset, OnComputeShaderReload reload)
        {
            if (!SUBSCRIPTIONS.TryGetValue(asset, out Subscription s))
                SUBSCRIPTIONS[asset] = s = new Subscription();

            s.Event += reload;
            SUBSCRIPTIONS[asset] = s;
        }

        [Conditional("UNITY_EDITOR")]
        public static void Unsubscribe(ComputeShader asset, OnComputeShaderReload reload)
        {
            if (SUBSCRIPTIONS.TryGetValue(asset, out Subscription s))
            {
                s.Event -= reload;
                SUBSCRIPTIONS[asset] = s;
            }
        }

        [Conditional("UNITY_EDITOR")]
        internal static void Invoke([NotNull] ComputeShader computeShader)
        {
            // Invoke is called from the background (import) thread, schedule a call

#if UNITY_EDITOR
            EditorApplication.delayCall += Schedule;
#endif

            void Schedule()
            {
                if (SUBSCRIPTIONS.TryGetValue(computeShader, out Subscription s))
                    s.Invoke(computeShader);
            }
        }

        private struct Subscription
        {
            internal event OnComputeShaderReload Event;

            internal void Invoke(ComputeShader shader) =>
                Event?.Invoke(shader);
        }
    }
}
