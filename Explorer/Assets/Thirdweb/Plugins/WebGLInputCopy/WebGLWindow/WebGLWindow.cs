using System;
using System.Runtime.InteropServices; // for DllImport
using AOT;
using UnityEngine;

namespace WebGLSupport
{
    internal static class WebGLWindowPlugin
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        public static extern void WebGLWindowInit();

        [DllImport("__Internal")]
        public static extern void WebGLWindowOnFocus(Action cb);

        [DllImport("__Internal")]
        public static extern void WebGLWindowOnBlur(Action cb);

        [DllImport("__Internal")]
        public static extern void WebGLWindowOnResize(Action cb);

        [DllImport("__Internal")]
        public static extern void WebGLWindowInjectFullscreen();

        [DllImport("__Internal")]
        public static extern string WebGLWindowGetCanvasName();

        [DllImport("__Internal")]
        public static extern void MakeFullscreen(string str);

        [DllImport("__Internal")]
        public static extern void ExitFullscreen();

        [DllImport("__Internal")]
        public static extern bool IsFullscreen();
#else
        public static void WebGLWindowInit() { }

        public static void WebGLWindowOnFocus(Action cb) { }

        public static void WebGLWindowOnBlur(Action cb) { }

        public static void WebGLWindowOnResize(Action cb) { }

        public static void WebGLWindowInjectFullscreen() { }

        public static string WebGLWindowGetCanvasName() =>
            "";

        public static void MakeFullscreen(string str) { }

        public static void ExitFullscreen() { }

        public static bool IsFullscreen() =>
            false;
#endif
    }

    public static class WebGLWindow
    {
        static WebGLWindow()
        {
            WebGLWindowPlugin.WebGLWindowInit();
        }

        public static bool Focus { get; private set; }
        public static event Action OnFocusEvent = () => { };
        public static event Action OnBlurEvent = () => { };
        public static event Action OnResizeEvent = () => { };

        private static string ViewportContent;

        private static void Init()
        {
            Focus = true;
            WebGLWindowPlugin.WebGLWindowOnFocus(OnWindowFocus);
            WebGLWindowPlugin.WebGLWindowOnBlur(OnWindowBlur);
            WebGLWindowPlugin.WebGLWindowOnResize(OnWindowResize);
            WebGLWindowPlugin.WebGLWindowInjectFullscreen();
        }

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnWindowFocus()
        {
            Focus = true;
            OnFocusEvent();
        }

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnWindowBlur()
        {
            Focus = false;
            OnBlurEvent();
        }

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnWindowResize()
        {
            OnResizeEvent();
        }

        [RuntimeInitializeOnLoadMethod]
        private static void RuntimeInitializeOnLoadMethod()
        {
            Init();
        }

        public static string GetCanvasName() =>
            WebGLWindowPlugin.WebGLWindowGetCanvasName();

        public static void MakeFullscreen(string fullscreenElementName = null)
        {
            WebGLWindowPlugin.MakeFullscreen(fullscreenElementName ?? GetCanvasName());
        }

        public static void ExitFullscreen()
        {
            WebGLWindowPlugin.ExitFullscreen();
        }

        public static bool IsFullscreen() =>
            WebGLWindowPlugin.IsFullscreen();

        public static void SwitchFullscreen()
        {
            if (IsFullscreen()) { ExitFullscreen(); }
            else { MakeFullscreen(); }
        }
    }
}
