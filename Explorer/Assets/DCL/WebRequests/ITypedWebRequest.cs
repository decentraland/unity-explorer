using Best.HTTP;
using System;
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
        IWebRequestController Controller { get; set; }

        /// <summary>
        ///     Parameters associated with a request
        /// </summary>
        RequestEnvelope Envelope { get; }

        /// <summary>
        ///     If Http2 is enabled then the request will be sent via <see cref="HTTPRequest" />
        /// </summary>
        bool Http2Supported { get; }

        string ArgsToString();

        UnityWebRequest CreateUnityWebRequest();

        HTTPRequest CreateHttp2Request();
    }

    public interface ITypedWebRequest<out TArgs> : ITypedWebRequest where TArgs: struct
    {
        TArgs Args { get; }

        string ITypedWebRequest.ArgsToString() =>
            Args.ToString();
    }
}
