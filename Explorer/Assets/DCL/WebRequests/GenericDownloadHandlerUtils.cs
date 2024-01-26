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
        public delegate Exception CreateExceptionOnParseFail(Exception exception, string text);

        public interface IGenericDownloadHandlerRequest { }

        public static async UniTask<T> OverwriteFromJsonAsync<TRequest, T>(this TRequest typedWebRequest, T targetObject, WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            CreateExceptionOnParseFail createCustomExceptionOnFailure = null)
            where T: class where TRequest: ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            UnityWebRequest webRequest = typedWebRequest.UnityWebRequest;

            string text = webRequest.downloadHandler.text;

            // Finalize the request immediately
            webRequest.Dispose();

            await SwitchToThreadAsync(threadFlags);

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
            catch (Exception e)
            {
                if (createCustomExceptionOnFailure != null)
                    throw createCustomExceptionOnFailure(e, text);
                else
                    throw;
            }
            finally { await SwitchToMainThreadAsync(threadFlags); }
        }

        public static async UniTask<T> CreateFromJsonAsync<TRequest, T>(this TRequest typedWebRequest, WRJsonParser jsonParser,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread, CreateExceptionOnParseFail? createCustomExceptionOnFailure = null)
            where TRequest: ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            UnityWebRequest webRequest = typedWebRequest.UnityWebRequest;
            string text = webRequest.downloadHandler.text;

            // Finalize the request immediately
            webRequest.Dispose();

            await SwitchToThreadAsync(threadFlags);

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
            catch (Exception e)
            {
                if (createCustomExceptionOnFailure != null)
                    throw createCustomExceptionOnFailure(e, text);
                else
                    throw;
            }
            finally { await SwitchToMainThreadAsync(threadFlags); }
        }

        public static async UniTask<T> CreateFromNewtonsoftJsonAsync<TRequest, T>(this TRequest typedWebRequest,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            CreateExceptionOnParseFail createCustomExceptionOnFailure = null,
            JsonSerializerSettings serializerSettings = null)
            where TRequest: ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            UnityWebRequest webRequest = typedWebRequest.UnityWebRequest;
            string text = webRequest.downloadHandler.text;

            // Finalize the request immediately
            webRequest.Dispose();

            await SwitchToThreadAsync(threadFlags);

            try { return JsonConvert.DeserializeObject<T>(text, serializerSettings); }
            catch (Exception e)
            {
                if (createCustomExceptionOnFailure != null)
                    throw createCustomExceptionOnFailure(e, text);
                else
                    throw;
            }
            finally { await SwitchToMainThreadAsync(threadFlags); }
        }

        /// <summary>
        ///     Get data array from UnityWebRequest.downloadHandler.data without modifying the original array
        ///     and finalize the request
        /// </summary>
        /// <returns></returns>
        public static byte[] GetDataCopy<TRequest>(this TRequest request) where TRequest: ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            UnityWebRequest unityWebRequest = request.UnityWebRequest;
            byte[] data = unityWebRequest.downloadHandler.data;
            unityWebRequest.Dispose();
            return data;
        }

        private static async UniTask SwitchToMainThreadAsync(WRThreadFlags flags)
        {
            if (EnumUtils.HasFlag(flags, WRThreadFlags.SwitchBackToMainThread))
                await UniTask.SwitchToMainThread();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async UniTask SwitchToThreadAsync(WRThreadFlags deserializationThreadFlags)
        {
            if (EnumUtils.HasFlag(deserializationThreadFlags, WRThreadFlags.SwitchToThreadPool))
                await UniTask.SwitchToThreadPool();
        }
    }
}
