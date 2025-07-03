namespace DCL.ApplicationGuards
{
    public static class GuardUtils
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
