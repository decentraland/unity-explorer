using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     The name "UnityWebRequest" is not used ot avoid confusion with <see cref="UnityWebRequest" />
    /// </summary>
    public class DefaultWebRequest : WebRequestBase, IWebRequest
    {
        internal readonly UnityWebRequest unityWebRequest;

        private bool downloadStarted;

        public IWebRequestResponse Response { get; }

        public bool Redirected => unityWebRequest.result is UnityWebRequest.Result.ProtocolError or UnityWebRequest.Result.ConnectionError;

        public bool IsTimedOut => unityWebRequest is { error: "Request timeout" };
        public bool IsAborted => !Response.Received && unityWebRequest is { error: "Request aborted" or "User Aborted" };
        public DateTime CreationTime { get; }

        public ulong DownloadedBytes => unityWebRequest.downloadedBytes;
        public ulong UploadedBytes => unityWebRequest.uploadedBytes;

        object IWebRequest.nativeRequest => unityWebRequest;

        public event Action<IWebRequest>? OnDownloadStarted;

        public DefaultWebRequest(UnityWebRequest unityWebRequest, ITypedWebRequest createdFrom) : base(createdFrom)
        {
            this.unityWebRequest = unityWebRequest;
            Response = new DefaultWebRequestResponse(unityWebRequest);
            CreationTime = DateTime.Now;
        }

        public void Update()
        {
            if (DownloadedBytes > 0 && !downloadStarted)
            {
                OnDownloadStarted?.Invoke(this);
                downloadStarted = true;
            }
        }

        public void Abort()
        {
            unityWebRequest.Abort();
        }

        public void SetTimeout(int timeout)
        {
            unityWebRequest.timeout = timeout;
        }

        public void SetRequestHeader(string name, string value)
        {
            unityWebRequest.SetRequestHeader(name, value);
        }

        protected override void OnDispose()
        {
            unityWebRequest.Dispose();
        }

        internal class DefaultWebRequestResponse : IWebRequestResponse
        {
            private readonly UnityWebRequest unityWebRequest;

            public bool Received => unityWebRequest is not { result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError };

            public string Text => unityWebRequest.downloadHandler.text;

            public string Error => unityWebRequest.error ?? string.Empty;

            public byte[] Data => unityWebRequest.downloadHandler.data;

            public int StatusCode => (int)unityWebRequest.responseCode;

            public bool IsSuccess => unityWebRequest.result == UnityWebRequest.Result.Success;

            public ulong DataLength => (ulong)unityWebRequest.downloadHandler.nativeData.Length;

            public DefaultWebRequestResponse(UnityWebRequest unityWebRequest)
            {
                this.unityWebRequest = unityWebRequest;
            }

            public Stream GetCompleteStream()
            {
                NativeArray<byte>.ReadOnly nativeData = unityWebRequest.downloadHandler.nativeData;

                unsafe
                {
                    var dataPtr = (byte*)nativeData.GetUnsafeReadOnlyPtr();
                    return new UnmanagedMemoryStream(dataPtr, nativeData.Length, nativeData.Length, FileAccess.Read);
                }
            }

            public string? GetHeader(string headerName) =>
                unityWebRequest.GetResponseHeader(headerName);

            public Dictionary<string, string>? FlattenHeaders() =>
                unityWebRequest.GetResponseHeaders();
        }
    }
}
