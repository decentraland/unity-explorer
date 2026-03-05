using System.Runtime.InteropServices;

namespace Global
{
    /// <summary>
    ///     Runtime browser detection utilities for WebGL builds.
    /// </summary>
    public static class BrowserUtils
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int IsBrowserSafari();
#endif

        /// <summary>
        ///     Returns true when running inside a Safari browser on WebGL.
        ///     Always returns false in the Editor or on non-WebGL platforms.
        /// </summary>
        public static bool IsSafari()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return IsBrowserSafari() != 0;
#else
            return false;
#endif
        }
    }
}
