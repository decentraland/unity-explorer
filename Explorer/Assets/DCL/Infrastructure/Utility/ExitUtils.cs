using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Utility
{
    public static class ExitUtils
    {
	    public static event Action BeforeApplicationQuitting;

        public static void Exit()
        {
	        BeforeApplicationQuitting?.Invoke();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

    }
}
