using Cysharp.Threading.Tasks;
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

        private class FrameCounter : IPlayerLoopItem
        {
            public long frameCount;

            public bool MoveNext()
            {
                frameCount = Time.frameCount;
                return true;
            }
        }

        private static FrameCounter frameCounter;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SaveFrameCount()
        {
            PlayerLoopHelper.AddAction(PlayerLoopTiming.Initialization, frameCounter = new FrameCounter());
        }

        /// <summary>
        ///     Thread-safe frame count
        /// </summary>
        public static long FrameCount => Application.isPlaying ? Interlocked.Read(ref frameCounter.frameCount) : 0;

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
    }
}
