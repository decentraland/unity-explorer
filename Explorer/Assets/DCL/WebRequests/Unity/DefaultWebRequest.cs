using NSubstitute;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     The name "UnityWebRequest" is not used ot avoid confusion with <see cref="UnityWebRequest" />
    /// </summary>
    public class DefaultWebRequest : IWebRequest
    {
        internal readonly UnityWebRequest unityWebRequest;

        public string Url => unityWebRequest.url;

        public IWebRequestResponse Response { get; }

        public bool Redirected => unityWebRequest.result is UnityWebRequest.Result.ProtocolError or UnityWebRequest.Result.ConnectionError;

        public bool IsTimedOut => unityWebRequest is { error: "Request timeout" };
        public bool IsAborted => !Response.Received && unityWebRequest is { error: "Request aborted" or "User Aborted" };

        object IWebRequest.nativeRequest => unityWebRequest;

        public DefaultWebRequest(UnityWebRequest unityWebRequest)
        {
            this.unityWebRequest = unityWebRequest;
            Response = new DefaultWebRequestResponse(unityWebRequest);
        }

        public void Dispose()
        {
            unityWebRequest.Dispose();
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

        internal class DefaultWebRequestResponse : IWebRequestResponse
        {
            private readonly UnityWebRequest unityWebRequest;

            public bool Received => unityWebRequest is not { result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError };

            public string Text => unityWebRequest.downloadHandler.text;

            public string? Error => unityWebRequest.error;

            public byte[] Data => unityWebRequest.downloadHandler.data;

            public int StatusCode => (int)unityWebRequest.responseCode;

            public bool IsSuccess => unityWebRequest.result == UnityWebRequest.Result.Success;

            public ulong DataLength => (ulong)unityWebRequest.downloadHandler.nativeData.Length;

            public DefaultWebRequestResponse(UnityWebRequest unityWebRequest)
            {
                this.unityWebRequest = unityWebRequest;
            }

            public string? GetHeader(string headerName) =>
                unityWebRequest.GetResponseHeader(headerName);

            public Dictionary<string, string>? FlattenHeaders() =>
                unityWebRequest.GetResponseHeaders();
        }
    }
}
