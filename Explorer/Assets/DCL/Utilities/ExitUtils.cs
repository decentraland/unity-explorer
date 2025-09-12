using System;

namespace DCL.ApplicationGuards
{
    public static class ExitUtils
    {
	    public static event Action BeforeApplicationQuitting;
	    
        public static void Exit()
        {
	        BeforeApplicationQuitting?.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

    }
}
