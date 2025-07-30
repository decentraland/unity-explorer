namespace DCL.ApplicationGuards
{
    public static class ExitUtils
    {
        public static void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            UnityEngine.Debug.Log("JUST FOR COMPILATION");
#else
            UnityEngine.Application.Quit();
#endif
        }

    }
}
