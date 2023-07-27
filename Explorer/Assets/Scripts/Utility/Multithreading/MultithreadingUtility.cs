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

        private static FrameCounter frameCounter;

        /// <summary>
        ///     Thread-safe frame count
        /// </summary>
        public static long FrameCount
        {
            get
            {
                return
#if UNITY_EDITOR
                    !PlayerLoopHelper.IsMainThread || !Application.isPlaying
                        ? 0
                        :
#endif
                        Interlocked.Read(ref frameCounter.frameCount);
            }
        }

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
