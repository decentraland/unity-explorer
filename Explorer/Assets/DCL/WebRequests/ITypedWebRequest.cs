using Best.HTTP;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public interface ITypedWebRequest : IDisposable
    {
        /// <summary>
        ///     Controller assigned to execute the web request <br />
        ///     It can be null if the request is saved "for later" and not yet executed, or executed in a very particular manner
        /// </summary>
        IWebRequestController Controller { get; set; }

        /// <summary>
        /// Parameters associated with a request
        /// </summary>
        RequestEnvelope Envelope { get; }

        string ArgsToString();

        UnityWebRequest CreateUnityWebRequest() =>
            throw new NotSupportedException($"{nameof(CreateUnityWebRequest)} is not supported by {GetType().Name}");

        HTTPRequest CreateHttp2Request() =>
            throw new NotSupportedException($"{nameof(CreateHttp2Request)} is not supported by {GetType().Name}");
    }

    public interface ITypedWebRequest<out TArgs> : ITypedWebRequest where TArgs: struct
    {
        TArgs Args { get; }

        string ITypedWebRequest.ArgsToString() =>
            Args.ToString();
    }
}
