using System;
using UnityEditor;

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
