using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices; // for DllImport
using AOT;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WebGLSupport
{
    internal class WebGLInputMobilePlugin
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        public static extern int WebGLInputMobileRegister(Action<int> OnTouchEnd);

        [DllImport("__Internal")]
        public static extern void WebGLInputMobileOnFocusOut(int id, Action<int> OnFocusOut);
#else
        /// <summary>
        ///     ID を割り振り
        /// </summary>
        /// <returns></returns>
        public static int WebGLInputMobileRegister(Action<int> OnTouchEnd) =>
            0;

        public static void WebGLInputMobileOnFocusOut(int id, Action<int> OnFocusOut) { }
#endif
    }

    public class WebGLInputMobile : MonoBehaviour, IPointerDownHandler
    {
        private static readonly Dictionary<int, WebGLInputMobile> instances = new ();

        private int id = -1;

        private void Awake()
        {
#if !(UNITY_WEBGL && !UNITY_EDITOR)

            // WebGL 以外、更新メソッドは動作しないようにします
            enabled = false;
#endif
        }

        /// <summary>
        ///     押されたら、touchend イベントを登録する
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (id != -1)
                return;

            id = WebGLInputMobilePlugin.WebGLInputMobileRegister(OnTouchEnd);
            instances[id] = this;
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnTouchEnd(int id)
        {
            WebGLInputMobile @this = instances[id];
            @this.GetComponent<WebGLInput>().OnSelect();
            @this.StartCoroutine(RegisterOnFocusOut(id));
        }

        private static IEnumerator RegisterOnFocusOut(int id)
        {
            yield return null; // wait one frame.
            WebGLInputMobilePlugin.WebGLInputMobileOnFocusOut(id, OnFocusOut);
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnFocusOut(int id)
        {
            WebGLInputMobile @this = instances[id];
            @this.StartCoroutine(ExecFocusOut(id));
        }

        private static IEnumerator ExecFocusOut(int id)
        {
            yield return null; // wait one frame.
            WebGLInputMobile @this = instances[id];
            @this.GetComponent<WebGLInput>().DeactivateInputField();

            // release
            @this.id = -1;
            instances.Remove(id);
        }
    }
}
