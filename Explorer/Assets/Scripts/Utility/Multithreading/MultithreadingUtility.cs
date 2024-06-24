using Cysharp.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Utility.Multithreading
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class MultithreadingUtility
    {
        private static bool isPaused;

        private static FrameCounter frameCounter;

        /// <summary>
        ///     Thread-safe frame count
        /// </summary>
        public static long FrameCount =>

            // In Tests frameCounter is null
            frameCounter != null ? Interlocked.Read(ref frameCounter.frameCount) : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SaveFrameCount()
        {
            PlayerLoopHelper.AddAction(PlayerLoopTiming.Initialization, frameCounter = new FrameCounter());
        }

        /// <summary>
        ///     Freezes the background thread while the Editor App is paused
        /// </summary>
        public static void WaitWhileOnPause()
        {
            // If it is called from the tests then we can't spin
            if (PlayerLoopHelper.IsMainThread)
                return;

            while (Volatile.Read(ref isPaused))
                Thread.Sleep(10);
        }

        /// <summary>
        ///     Must ensure that the execution does not jump between different threads
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEBUG")]
        public static void AssertMainThread(string funcName, bool isMainThread = false)
        {
            if (PlayerLoopHelper.IsMainThread != isMainThread)
                throw new ThreadStateException($"Execution after calling {funcName} must be {(isMainThread ? "on" : "off")} the main thread");
        }

        private class FrameCounter : IPlayerLoopItem
        {
            public long frameCount;

            public bool MoveNext()
            {
                frameCount = Time.frameCount;
                return true;
            }
        }

#if UNITY_EDITOR
        static MultithreadingUtility()
        {
            EditorApplication.pauseStateChanged += OnPauseStateChanged;
        }

        private static void OnPauseStateChanged(PauseState state)
        {
            isPaused = state == PauseState.Paused;
        }

#endif
    }
}
