using System.Runtime.InteropServices;

namespace Plugins.NativeWindowManager
{
    public static class NativeWindowManager
    {
        [DllImport("WindowResizeConstraint")]
        private static extern void WindowConstraint_Init();

        [DllImport("WindowResizeConstraint")]
        private static extern void WindowConstraint_Set(int enabled, float minAspect, float maxAspect, int minWidth, int minHeight);

        private static bool initialized;

        public static void ApplyConstraints(bool enabled, float minAspectRatio = 0, float maxAspectRatio = 0, int minWidth = 0, int minHeight = 0)
        {
#if !UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN)
            if (!initialized)
            {
                WindowConstraint_Init();
                initialized = true;
            }

            WindowConstraint_Set(enabled ? 1 : 0, minAspectRatio, maxAspectRatio, minWidth, minHeight);
#endif
        }
    }
}
