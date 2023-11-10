using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.WebRequests
{
    public static class GenericDownloadHandlerUtils
    {
        public static async UniTask<T> OverwriteFromJson<T>(UnityWebRequest webRequest, T targetObject, WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread) where T: class
        {
            string text = webRequest.downloadHandler.text;

            // Finalize the request immediately
            webRequest.Dispose();

            await SwitchToThread(threadFlags);

            try
            {
                switch (jsonParser)
                {
                    case WRJsonParser.Unity:
                        JsonUtility.FromJsonOverwrite(text, targetObject);
                        return targetObject;
                    case WRJsonParser.Newtonsoft:
                        JsonConvert.PopulateObject(text, targetObject);
                        return targetObject;
                    case WRJsonParser.NewtonsoftInEditor:
                        if (Application.isEditor)
                            goto case WRJsonParser.Newtonsoft;

                        goto case WRJsonParser.Unity;
                    default: throw new ArgumentOutOfRangeException(nameof(jsonParser), jsonParser, null);
                }
            }
            finally { await SwitchToMainThread(threadFlags); }
        }

        public static async UniTask<T> CreateFromJson<T>(UnityWebRequest webRequest, WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread)
        {
            string text = webRequest.downloadHandler.text;

            // Finalize the request immediately
            webRequest.Dispose();

            await SwitchToThread(threadFlags);

            try
            {
                switch (jsonParser)
                {
                    case WRJsonParser.Unity:
                        return JsonUtility.FromJson<T>(text);
                    case WRJsonParser.Newtonsoft:
                        return JsonConvert.DeserializeObject<T>(text);
                    case WRJsonParser.NewtonsoftInEditor:
                        if (Application.isEditor)
                            goto case WRJsonParser.Newtonsoft;

                        goto case WRJsonParser.Unity;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(jsonParser), jsonParser, null);
                }
            }
            finally { await SwitchToMainThread(threadFlags); }
        }

        /// <summary>
        ///     Get data array from UnityWebRequest.downloadHandler.data without modifying the original array
        ///     and finalize the request
        /// </summary>
        /// <param name="unityWebRequest"></param>
        /// <returns></returns>
        public static byte[] GetDataCopy(UnityWebRequest unityWebRequest)
        {
            byte[] data = unityWebRequest.downloadHandler.data;
            unityWebRequest.Dispose();
            return data;
        }

        private static async UniTask SwitchToMainThread(WRThreadFlags flags)
        {
            if (EnumUtils.HasFlag(flags, WRThreadFlags.SwitchBackToMainThread))
                await UniTask.SwitchToMainThread();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async UniTask SwitchToThread(WRThreadFlags deserializationThreadFlags)
        {
            if (EnumUtils.HasFlag(deserializationThreadFlags, WRThreadFlags.SwitchToThreadPool))
                await UniTask.SwitchToThreadPool();
        }
    }
}
