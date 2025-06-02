using Best.HTTP;
using Best.HTTP.Response;
using Best.HTTP.Shared.PlatformSupport.Memory;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Contains operation that are common for all generic requests
    /// </summary>
    public static class GenericDownloadHandlerUtils
    {
        public delegate Exception CreateExceptionOnParseFail(Exception exception, string text);

        public delegate void TransformChunk<in T>(T context, NativeArray<byte>.ReadOnly chunk, ulong chunkIndex);
        public delegate T PrepareContext<out T>(ulong dataLength);

        public static async UniTask<TResult> ProcessAndDisposeAsync<TResult>(this ITypedWebRequest request, Func<IWebRequest, CancellationToken, UniTask<TResult>> getResult, CancellationToken ct)
        {
            using IWebRequest? req = await request.SendAsync(ct);
            return await getResult(req, ct);
        }

        public static UniTask<string> StoreTextAsync(this ITypedWebRequest request, CancellationToken ct) =>
            request.ProcessAndDisposeAsync<string>(static (r, ct) => r.Response.GetTextAsync(ct), ct);

        public static UniTask<byte[]> GetDataCopyAsync(this ITypedWebRequest request, CancellationToken ct) =>
            request.ProcessAndDisposeAsync<byte[]>(static (r, ct) => r.Response.GetDataAsync(ct), ct);

        public static async UniTask<string?> GetResponseHeaderAsync(this ITypedWebRequest request, string headerName, CancellationToken ct)
        {
            using IWebRequest req = await request.SendAsync(ct);
            return req.Response.GetHeader(headerName);
        }

        public static async UniTask<T> OverwriteFromJsonAsync<T>(
            this ITypedWebRequest request,
            T target,
            WRJsonParser jsonParser,
            CancellationToken ct,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            CreateExceptionOnParseFail? createCustomExceptionOnFailure = null,
            JsonSerializerSettings? serializerSettings = null)
        {
            using IWebRequest? createdRequest = await request.SendAsync(ct);

            // If it is Unity API we must first switch to the main thread to read the response

            if (createdRequest.nativeRequest is UnityWebRequest)
                await UniTask.SwitchToMainThread();

            string text = string.Empty;

            try
            {
                switch (jsonParser)
                {
                    case WRJsonParser.Unity:
#if !UNITY_EDITOR
                    case WRJsonParser.NewtonsoftInEditor:
#endif
                        text = await createdRequest.Response.GetTextAsync(ct);
                        await SwitchToThreadAsync(threadFlags);
                        JsonUtility.FromJsonOverwrite(text, target);
                        break;
                    default:
                    {
                        using Stream stream = await createdRequest.Response.GetCompleteStreamAsync(ct);

                        await SwitchToThreadAsync(threadFlags);

                        var serializer = JsonSerializer.CreateDefault(serializerSettings);

                        using var textReader = new StreamReader(stream, Encoding.UTF8);
                        using var jsonReader = new JsonTextReader(textReader);
                        serializer.Populate(jsonReader, target!);
                        break;
                    }
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

            return target;
        }

        public static UniTask<T> CreateFromNewtonsoftJsonAsync<T>(
            this ITypedWebRequest request,
            CancellationToken ct,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            CreateExceptionOnParseFail? createCustomExceptionOnFailure = null,
            JsonSerializerSettings? serializerSettings = null) =>
            request.CreateFromJsonAsync<T>(WRJsonParser.Newtonsoft, ct, threadFlags, serializerSettings, createCustomExceptionOnFailure);

        public static async UniTask<T> CreateFromJsonAsync<T>(this ITypedWebRequest request,
            WRJsonParser jsonParser,
            CancellationToken ct,
            WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread,
            JsonSerializerSettings? newtonsoftSettings = null,
            CreateExceptionOnParseFail? createCustomExceptionOnFailure = null)
        {
            using IWebRequest? createdRequest = await request.SendAsync(ct);

            // If it is Unity API we must first switch to the main thread to read the response

            if (createdRequest.nativeRequest is UnityWebRequest)
                await UniTask.SwitchToMainThread();

            string text = string.Empty;

            try
            {
                switch (jsonParser)
                {
                    case WRJsonParser.Unity:
#if !UNITY_EDITOR
                    case WRJsonParser.NewtonsoftInEditor:
#endif
                        text = await createdRequest.Response.GetTextAsync(ct);
                        await SwitchToThreadAsync(threadFlags);
                        return JsonUtility.FromJson<T>(text);
                    default:
                    {
                        using Stream stream = await createdRequest.Response.GetCompleteStreamAsync(ct);

                        await SwitchToThreadAsync(threadFlags);

                        var serializer = JsonSerializer.CreateDefault(newtonsoftSettings);

                        using var textReader = new StreamReader(stream, Encoding.UTF8);
                        using var jsonReader = new JsonTextReader(textReader);
                        return serializer.Deserialize<T>(jsonReader)!;
                    }
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

        /// <summary>
        ///     Executes the request, transforms the output data and disposes of the request <br />
        ///     It's an efficient method to process data without allocations
        ///     <remarks>
        ///         <list type="bullet">
        ///             <item> The underlying downloaded data will be disposed upon completion </item>
        ///         </list>
        ///     </remarks>
        /// </summary>
        public static async UniTask<T> TransformDataAsync<T>(this ITypedWebRequest request, PrepareContext<T> prepareContext, TransformChunk<T> transformChunk, CancellationToken ct, WRThreadFlags threadFlags = WRThreadFlags.SwitchToThreadPool | WRThreadFlags.SwitchBackToMainThread)
        {
            using IWebRequest? sentRequest = await request.SendAsync(ct);

            await SwitchToThreadAsync(threadFlags);

            T context = prepareContext(sentRequest.Response.DataLength);

            switch (sentRequest.nativeRequest)
            {
                case UnityWebRequest uwr:
                    NativeArray<byte>.ReadOnly nativeData = uwr.downloadHandler.nativeData;

                    transformChunk(context, nativeData, 0);
                    break;
                case HTTPRequest http2Req:
                    await using (http2Req.Response.DownStream)
                        ProcessSegments(http2Req.Response.DownStream);

                    break;
            }

            return context;

            unsafe void ProcessSegments(DownloadContentStream stream)
            {
                ulong offsetFromStart = 0;

                while (stream.TryTake(out BufferSegment segment))
                {
                    // Convert segment to native array
                    fixed (byte* segmentPtr = segment.Data)
                    {
                        void* ptr = segmentPtr + segment.Offset;
                        NativeArray<byte> nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(ptr, segment.Count, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(
                            ref nativeArray,
                            AtomicSafetyHandle.Create()
                        );
#endif

                        transformChunk(context, nativeArray.AsReadOnly(), offsetFromStart);
                    }

                    offsetFromStart += (ulong)segment.Count;
                }
            }
        }

        internal static async UniTask SwitchToMainThreadAsync(WRThreadFlags flags)
        {
            if (EnumUtils.HasFlag(flags, WRThreadFlags.SwitchBackToMainThread))
                await UniTask.SwitchToMainThread();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async UniTask SwitchToThreadAsync(WRThreadFlags deserializationThreadFlags)
        {
            if (EnumUtils.HasFlag(deserializationThreadFlags, WRThreadFlags.SwitchToThreadPool))
                await UniTask.SwitchToThreadPool();
        }

        /// <summary>
        ///     Executes the web request and does nothing with the result <br />
        ///     On Exception: Throws a new exception created by the provided factory method
        /// </summary>
        public static async UniTask WithCustomExceptionAsync(this ITypedWebRequest webRequest, Func<WebRequestException, Exception> newExceptionFactoryMethod, CancellationToken ct)
        {
            try
            {
                await webRequest.SendAndForgetAsync(ct);
            }
            catch (WebRequestException e) { throw newExceptionFactoryMethod(e); }
        }
    }
}
