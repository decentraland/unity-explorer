using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;
using Utility.Times;
using static DCL.WebRequests.WebRequestControllerExtensions;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Contains operation that are common for all generic requests
    /// </summary>
    public static class GenericDownloadHandlerUtils
    {
        public delegate Exception CreateExceptionOnParseFail(Exception exception, string text);

        public static Adapter<GenericPostRequest, GenericPostArguments> SignedFetchPostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            string jsonMetaData,
            CancellationToken ct
        )
        {
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            return new Adapter<GenericPostRequest, GenericPostArguments>(
                controller,
                commonArguments,
                GenericPostArguments.Empty,
                ct,
                ReportCategory.GENERIC_WEB_REQUEST,
                new WebRequestHeadersInfo().WithSign(jsonMetaData, unixTimestamp),
                WebRequestSignInfo.NewFromRaw(jsonMetaData, commonArguments.URL, unixTimestamp, "post"),
                null,
                POST_GENERIC
            );
        }

        public static Adapter<GenericGetRequest, GenericGetArguments> GetAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null,
            ISet<long>? ignoreErrorCodes = null
        ) =>
            new (controller, commonArguments, default(GenericGetArguments), ct, reportCategory, headersInfo, signInfo, ignoreErrorCodes, GET_GENERIC);

        public static Adapter<GenericPostRequest, GenericPostArguments> PostAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPostArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo, null, POST_GENERIC);

        public static Adapter<GenericPutRequest, GenericPutArguments> PutAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPutArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo, null, PUT_GENERIC);

        public static Adapter<GenericPatchRequest, GenericPatchArguments> PatchAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericPatchArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo, null, PATCH_GENERIC);

        public static Adapter<GenericHeadRequest, GenericHeadArguments> HeadAsync(
            this IWebRequestController controller,
            CommonArguments commonArguments,
            GenericHeadArguments arguments,
            CancellationToken ct,
            string reportCategory = ReportCategory.GENERIC_WEB_REQUEST,
            WebRequestHeadersInfo? headersInfo = null,
            WebRequestSignInfo? signInfo = null) =>
            new (controller, commonArguments, arguments, ct, reportCategory, headersInfo, signInfo, null, HEAD_GENERIC);

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
            private readonly InitializeRequest<TWebRequestArgs, TRequest> initializeRequest;
            private readonly string reportCategory;
            private readonly WebRequestSignInfo? signInfo;

            public Adapter(IWebRequestController controller, CommonArguments commonArguments, TWebRequestArgs args, CancellationToken ct, string reportCategory,
                WebRequestHeadersInfo? headersInfo, WebRequestSignInfo? signInfo, ISet<long>? ignoreErrorCodes, InitializeRequest<TWebRequestArgs, TRequest> initializeRequest)
            {
                this.commonArguments = commonArguments;
                this.args = args;
                this.ct = ct;
                this.reportCategory = reportCategory;
                this.headersInfo = headersInfo;
                this.signInfo = signInfo;
                this.ignoreErrorCodes = ignoreErrorCodes;
                this.initializeRequest = initializeRequest;
                this.controller = controller;
            }

            internal UniTask<TResult> SendAsync<TOp, TResult>(TOp op) where TOp: struct, IWebRequestOp<TRequest, TResult> =>
                controller.SendAsync<TRequest, TWebRequestArgs, TOp, TResult>(initializeRequest, commonArguments, args, op, ct, reportCategory, headersInfo, signInfo, ignoreErrorCodes);

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
                UnityWebRequest webRequest = request.UnityWebRequest;
                string text = webRequest.downloadHandler.text;

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
                UnityWebRequest webRequest = request.UnityWebRequest;

                string text = webRequest.downloadHandler.text;

                await SwitchToThreadAsync(threadFlags);

                try
                {
                    switch (jsonParser)
                    {
                        case WRJsonParser.Unity:
                            JsonUtility.FromJsonOverwrite(text, Target);
                            return Target;
                        case WRJsonParser.Newtonsoft:
                            JsonConvert.PopulateObject(text, Target!);
                            return Target;
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
        }

        public struct GetDataCopyOp<TRequest> : IWebRequestOp<TRequest, byte[]> where TRequest: struct, ITypedWebRequest, IGenericDownloadHandlerRequest
        {
            public UniTask<byte[]?> ExecuteAsync(TRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(webRequest.UnityWebRequest.downloadHandler.data)!;
        }
    }
}
