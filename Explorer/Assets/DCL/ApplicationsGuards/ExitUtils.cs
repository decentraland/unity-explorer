namespace DCL.ApplicationGuards
{
    public static class ExitUtils
    {
        public static void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

    }
}
