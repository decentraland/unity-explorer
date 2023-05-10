using Cysharp.Threading.Tasks;
using System.Threading;
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
