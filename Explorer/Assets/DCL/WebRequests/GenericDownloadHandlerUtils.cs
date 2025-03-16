using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.GenericDelete;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Contains operation that are common for all generic requests
    /// </summary>
    public static class GenericDownloadHandlerUtils
    {
        public delegate Exception CreateExceptionOnParseFail(Exception exception, string text);

        public static Adapter<GenericGetRequest, GenericGetArguments> GetAsync(this IWebRequestController controller, CommonArguments commonArguments, CancellationToken ct, ReportData reportData, WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null, ISet<long>? ignoreErrorCodes = null) =>
            new (controller, commonArguments, default(GenericGetArguments), ct, reportData, headersInfo, signInfo, ignoreErrorCodes);

        public static Adapter<GenericPostRequest, GenericPostArguments> PostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPostArguments arguments,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportData, headersInfo, signInfo, null);

        public static Adapter<GenericDeleteRequest, GenericDeleteArguments> DeleteAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericDeleteArguments arguments,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportData, headersInfo, signInfo, null);

        public static Adapter<GenericPutRequest, GenericPutArguments> PutAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPutArguments arguments,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportData, headersInfo, signInfo, null);

        public static Adapter<GenericPatchRequest, GenericPatchArguments> PatchAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPatchArguments arguments,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportData, headersInfo, signInfo, null);

        public static Adapter<GenericHeadRequest, GenericHeadArguments> HeadAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            CancellationToken ct,
            ReportData reportData,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, default(GenericHeadArguments), ct, reportData, headersInfo, signInfo, null);

        /// <summary>
        ///     Adapts existing calls to the required-op flow
        /// </summary>
        public readonly struct Adapter<TRequest, TWebRequestArgs>
            where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
            where TWebRequestArgs: struct
        {
            private readonly TWebRequestArgs args;
            private readonly CommonArguments commonArguments;
            private readonly IWebRequestController controller;
            private readonly CancellationToken ct;
            private readonly WebRequestHeadersInfo? headersInfo;
            private readonly ISet<long>? ignoreErrorCodes;
            private readonly ReportData reportData;
            private readonly WebRequestSignInfo? signInfo;

            public Adapter(
                IWebRequestController controller,
                CommonArguments commonArguments,
                TWebRequestArgs args,
                CancellationToken ct,
                ReportData reportData,
                WebRequestHeadersInfo? headersInfo,
                WebRequestSignInfo? signInfo,
                ISet<long>? ignoreErrorCodes
            )
            {
                this.commonArguments = commonArguments;
                this.args = args;
                this.ct = ct;
                this.reportData = reportData;
                this.headersInfo = headersInfo;
                this.signInfo = signInfo;
                this.ignoreErrorCodes = ignoreErrorCodes;
                this.controller = controller;
            }

            internal UniTask<TResult> SendAsync<TOp, TResult>(TOp op) where TOp: struct, IWebRequestOp<TRequest, TResult> =>
                controller.SendAsync<TRequest, TWebRequestArgs, TOp, TResult>(commonArguments, args, op, ct, reportData, headersInfo, signInfo, ignoreErrorCodes);

            public UniTask WithNoOpAsync() =>
                SendAsync<WebRequestUtils.NoOp<TRequest>, WebRequestUtils.NoResult>(new WebRequestUtils.NoOp<TRequest>());

            public UniTask<T> CreateFromJson<T>(WRJsonParser jsonParser,
                WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
                CreateExceptionOnParseFail? createCustomExceptionOnFailure = null) =>
                SendAsync<CreateFromJsonOp<T, TRequest>, T>(new CreateFromJsonOp<T, TRequest>(jsonParser, threadFlags, createCustomExceptionOnFailure));

            public UniTask<T> CreateFromNewtonsoftJsonAsync<T>(
                WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
                CreateExceptionOnParseFail? createCustomExceptionOnFailure = null,
                JsonSerializerSettings? serializerSettings = null) =>
                SendAsync<CreateFromJsonOp<T, TRequest>, T>(new CreateFromJsonOp<T, TRequest>(WRJsonParser.Newtonsoft, threadFlags, createCustomExceptionOnFailure, serializerSettings));

            public UniTask<string> StoreTextAsync() =>
                SendAsync<StoreTextOp<TRequest>, string>(new StoreTextOp<TRequest>());

            public UniTask<byte[]> GetDataCopyAsync() =>
                SendAsync<GetDataCopyOp<TRequest>, byte[]>(new GetDataCopyOp<TRequest>());

            public UniTask<string> GetResponseHeaderAsync(string headerName) =>
                SendAsync<GetResponseHeaderOp<TRequest>, string>(new GetResponseHeaderOp<TRequest>(headerName));

            /// <summary>
            ///     Exposes the download handler to the caller so it's the caller responsibility to dispose it later
            /// </summary>
            /// <returns></returns>
            public UniTask<DownloadHandler> ExposeDownloadHandlerAsync() =>
                SendAsync<ExposeDownloadHandler<TRequest>, DownloadHandler>(new ExposeDownloadHandler<TRequest>());

            public UniTask<int> StatusCodeAsync() =>
                SendAsync<StatusCodeOp<TRequest>, int>(new StatusCodeOp<TRequest>());

            public UniTask<T> OverwriteFromJsonAsync<T>(
                T targetObject,
                WRJsonParser jsonParser,
                WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
                CreateExceptionOnParseFail? createCustomExceptionOnFailure = null) =>
                SendAsync<OverwriteFromJsonAsyncOp<T, TRequest>, T>(new OverwriteFromJsonAsyncOp<T, TRequest>(targetObject, jsonParser, threadFlags, createCustomExceptionOnFailure));

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

        public interface IGenericDownloadHandlerRequest { }

        /// <summary>
        ///     Reads the text from the download handler and saves in the property
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        public struct StoreTextOp<TRequest> : IWebRequestOp<TRequest, string> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            public UniTask<string?> ExecuteAsync(TRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(webRequest.UnityWebRequest.downloadHandler.text)!;
        }

        public struct StatusCodeOp<TRequest> : IWebRequestOp<TRequest, int> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            public UniTask<int> ExecuteAsync(TRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult((int)webRequest.UnityWebRequest.responseCode);
        }

        public struct CreateFromJsonOp<T, TRequest> : IWebRequestOp<TRequest, T> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            private readonly CreateExceptionOnParseFail? createCustomExceptionOnFailure;
            private readonly WRJsonParser jsonParser;
            private readonly JsonSerializerSettings? newtonsoftSettings;
            private readonly WRThreadFlags threadFlags;

            public CreateFromJsonOp(WRJsonParser jsonParser, WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread, CreateExceptionOnParseFail? createCustomExceptionOnFailure = null, JsonSerializerSettings? newtonsoftSettings = null)
            {
                this.jsonParser = jsonParser;
                this.threadFlags = threadFlags;
                this.newtonsoftSettings = newtonsoftSettings;
                this.createCustomExceptionOnFailure = createCustomExceptionOnFailure;
            }

            public async UniTask<T?> ExecuteAsync(TRequest request, CancellationToken ct)
            {
                DownloadHandler downloadHandler = request.UnityWebRequest.downloadHandler;
                string text = null;

                try
                {
                    if (jsonParser == WRJsonParser.Unity
#if !UNITY_EDITOR
                        || jsonParser == WRJsonParser.NewtonsoftInEditor
#endif
                        )
                    {
                        text = downloadHandler.text;

                        if ((threadFlags & WRThreadFlags.SwitchToThreadPool) != 0)
                            await UniTask.SwitchToThreadPool();

                        return JsonUtility.FromJson<T>(text);
                    }
                    else
                    {
                        var nativeData = downloadHandler.nativeData;

                        if ((threadFlags & WRThreadFlags.SwitchToThreadPool) != 0)
                            await UniTask.SwitchToThreadPool();

                        var serializer = JsonSerializer.CreateDefault(newtonsoftSettings);

                        unsafe
                        {
                            var dataPtr = (byte*)nativeData.GetUnsafeReadOnlyPtr();

                            using var stream = new UnmanagedMemoryStream(dataPtr, nativeData.Length,
                                nativeData.Length, FileAccess.Read);

                            using var textReader = new StreamReader(stream, Encoding.UTF8);
                            using var jsonReader = new JsonTextReader(textReader);
                            return serializer.Deserialize<T>(jsonReader);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (createCustomExceptionOnFailure != null && text != null)
                        throw createCustomExceptionOnFailure(ex, text);
                    else
                        throw;
                }
                finally
                {
                    if (threadFlags == WRThreadFlags.SwitchToThreadPoolAndBack)
                        await UniTask.SwitchToMainThread();
                }
            }
        }

        public struct OverwriteFromJsonAsyncOp<T, TRequest> : IWebRequestOp<TRequest, T> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            private readonly CreateExceptionOnParseFail? createCustomExceptionOnFailure;
            private readonly WRJsonParser jsonParser;

            public readonly T Target;
            private readonly WRThreadFlags threadFlags;

            public OverwriteFromJsonAsyncOp(T target, WRJsonParser jsonParser, WRThreadFlags threadFlags, CreateExceptionOnParseFail? createCustomExceptionOnFailure)
            {
                Target = target;
                this.jsonParser = jsonParser;
                this.threadFlags = threadFlags;
                this.createCustomExceptionOnFailure = createCustomExceptionOnFailure;
            }

            public async UniTask<T?> ExecuteAsync(TRequest request, CancellationToken ct)
            {
                DownloadHandler downloadHandler = request.UnityWebRequest.downloadHandler;
                string text = null;

                try
                {
                    if (jsonParser == WRJsonParser.Unity
#if !UNITY_EDITOR
                        || jsonParser == WRJsonParser.NewtonsoftInEditor
#endif
                        )
                    {
                        text = downloadHandler.text;

                        if ((threadFlags & WRThreadFlags.SwitchToThreadPool) != 0)
                            await UniTask.SwitchToThreadPool();

                        JsonUtility.FromJsonOverwrite(text, Target);
                    }
                    else
                    {
                        var nativeData = downloadHandler.nativeData;

                        if ((threadFlags & WRThreadFlags.SwitchToThreadPool) != 0)
                            await UniTask.SwitchToThreadPool();

                        var serializer = JsonSerializer.CreateDefault();

                        unsafe
                        {
                            var dataPtr = (byte*)nativeData.GetUnsafeReadOnlyPtr();

                            using var stream = new UnmanagedMemoryStream(dataPtr, nativeData.Length,
                                nativeData.Length, FileAccess.Read);

                            using var textReader = new StreamReader(stream, Encoding.UTF8);
                            using var jsonReader = new JsonTextReader(textReader);
                            serializer.Populate(jsonReader, Target);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (createCustomExceptionOnFailure != null && text != null)
                        throw createCustomExceptionOnFailure(ex, text);
                    else
                        throw;
                }
                finally
                {
                    if (threadFlags == WRThreadFlags.SwitchToThreadPoolAndBack)
                        await UniTask.SwitchToMainThread();
                }

                return Target;
            }
        }

        public struct GetDataCopyOp<TRequest> : IWebRequestOp<TRequest, byte[]> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            public UniTask<byte[]?> ExecuteAsync(TRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(webRequest.UnityWebRequest.downloadHandler.data)!;
        }

        public struct GetResponseHeaderOp<TRequest> : IWebRequestOp<TRequest, string> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            private readonly string headerName;

            public GetResponseHeaderOp(string headerName)
            {
                this.headerName = headerName;
            }

            public UniTask<string?> ExecuteAsync(TRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(webRequest.UnityWebRequest.GetResponseHeader(headerName))!;
        }

        public struct ExposeDownloadHandler<TRequest> : IWebRequestOp<TRequest, DownloadHandler> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            public UniTask<DownloadHandler?> ExecuteAsync(TRequest webRequest, CancellationToken ct)
            {
                webRequest.UnityWebRequest.disposeDownloadHandlerOnDispose = false;
                return UniTask.FromResult(webRequest.UnityWebRequest.downloadHandler)!;
            }
        }
    }
}
