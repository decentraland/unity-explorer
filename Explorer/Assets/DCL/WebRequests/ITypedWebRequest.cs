using Best.HTTP;
using System;
using System.Net.Http;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Defines a set of parameters from which a request can be created
    /// </summary>
    public interface ITypedWebRequest : IDisposable
    {
        /// <summary>
        ///     Controller assigned to execute the web request <br />
        ///     It can be null if the request is saved "for later" and not yet executed, or executed in a very particular manner
        /// </summary>
        IWebRequestController Controller { get; }

        /// <summary>
        ///     Parameters associated with a request
        /// </summary>
        ref readonly RequestEnvelope Envelope { get; }

        /// <summary>
        ///     If Http2 is enabled then the request will be sent via <see cref="HTTPRequest" />
        /// </summary>
        bool Http2Supported { get; }

        /// <summary>
        ///     Maximum Size of the download buffer in bytes, used if <see cref="Http2Supported" /> is enabled.
        ///     <remarks>
        ///         If <see cref="StreamingSupported" /> is false (reading from the output stream on-the-go is not supported), the request will fail if the size of the content is higher than this value.
        ///     </remarks>
        /// </summary>
        long DownloadBufferMaxSize { get; }

        /// <summary>
        ///     If streaming is supported download data is being read gradually
        /// </summary>
        bool StreamingSupported { get; }

        string ArgsToString();

        UnityWebRequest CreateUnityWebRequest();

        HTTPRequest CreateHttp2Request();

        HttpRequestMessage CreateYetAnotherHttpRequest();
    }

    public interface ITypedWebRequest<out TArgs> : ITypedWebRequest where TArgs: struct
    {
        TArgs Args { get; }

        string ITypedWebRequest.ArgsToString() =>
            Args.ToString();
    }
}
