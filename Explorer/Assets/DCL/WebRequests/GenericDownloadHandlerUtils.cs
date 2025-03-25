using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Contains operation that are common for all generic requests
    /// </summary>
    public static class GenericDownloadHandlerUtils
    {
        public delegate Exception CreateExceptionOnParseFail(Exception exception, string text);

        public static async UniTask<TResult> ProcessAndDispose<TResult>(this ITypedWebRequest request, Func<IWebRequest, TResult> getResult, CancellationToken ct)
        {
            using IWebRequest? req = await request.SendAsync(ct);
            return getResult(req);
        }

        public static async UniTask<TResult> ProcessAndDispose<TResult>(this ITypedWebRequest request, Func<IWebRequest, UniTask<TResult>> getResult, CancellationToken ct)
        {
            using IWebRequest? req = await request.SendAsync(ct);
            return await getResult(req);
        }

        public static UniTask<string> StoreTextAsync(this ITypedWebRequest request, CancellationToken ct) =>
            request.ProcessAndDispose(static r => r.Response.Text, ct);

        public static UniTask<byte[]> GetDataCopyAsync(this ITypedWebRequest request, CancellationToken ct) =>
            request.ProcessAndDispose(static r => r.Response.Data, ct);

        public static async UniTask<string> GetResponseHeaderAsync(this ITypedWebRequest request, string headerName, CancellationToken ct) =>
            (await request.SendAsync(ct)).Response.GetHeader(headerName);

        public static async UniTask<T> OverwriteFromJsonAsync<T>(
            this ITypedWebRequest request,
            T target,
            WRJsonParser jsonParser,
            CancellationToken ct,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            CreateExceptionOnParseFail? createCustomExceptionOnFailure = null,
            JsonSerializerSettings? serializerSettings = null)
        {
            string text = (await request.SendAsync(ct)).Response.Text;

            await SwitchToThreadAsync(threadFlags);

            try
            {
                switch (jsonParser)
                {
                    case WRJsonParser.Unity:
                        JsonUtility.FromJsonOverwrite(text, target);
                        return target;
                    case WRJsonParser.Newtonsoft:
                        JsonConvert.PopulateObject(text, target!, serializerSettings);
                        return target;
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

        public static async UniTask<T> CreateFromJson<T>(this ITypedWebRequest request,
            WRJsonParser jsonParser,
            CancellationToken ct,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            JsonSerializerSettings? newtonsoftSettings = null,
            CreateExceptionOnParseFail? createCustomExceptionOnFailure = null)
        {
            string text = (await request.SendAsync(ct)).Response.Text;

            await SwitchToThreadAsync(threadFlags);

            try
            {
                switch (jsonParser)
                {
                    case WRJsonParser.Unity:
                        return JsonUtility.FromJson<T>(text);
                    case WRJsonParser.Newtonsoft:
                        return JsonConvert.DeserializeObject<T>(text, newtonsoftSettings);
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

        /// <summary>
        ///     Executes the web request and does nothing with the result
        /// </summary>
        public async UniTask<WebRequestUtils.NoOp<TRequest>> WithCustomExceptionAsync(Func<UnityWebRequestException, Exception> newExceptionFactoryMethod)
        {
            try
            {
                await SendAsync<WebRequestUtils.NoOp<TRequest>, WebRequestUtils.NoResult>(new WebRequestUtils.NoOp<TRequest>());
                return new WebRequestUtils.NoOp<TRequest>();
            }
            catch (UnityWebRequestException e) { throw newExceptionFactoryMethod(e); }
        }
    }
}
