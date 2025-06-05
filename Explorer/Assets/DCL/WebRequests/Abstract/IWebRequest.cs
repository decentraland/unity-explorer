using Best.HTTP;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Represents an instance of the executed web request. <br />
    ///     This abstraction allows to switch easily between <see cref="UnityWebRequest" /> and <see cref="HTTPRequest" />
    ///     <remarks>
    ///         The APIs of <see cref="HTTPRequest" /> and <see cref="UnityWebRequest" /> have very little in common, this abstraction aims to provide very basics.
    ///         In other cases individual code in the respective implementation of <see cref="ITypedWebRequest" /> should be used
    ///     </remarks>
    /// </summary>
    public partial interface IWebRequest : IDisposable
    {
        Uri Url { get; }

        /// <summary>
        ///     The envelope this request was created from
        ///     <remarks>
        ///         It disposes along with the request itself
        ///     </remarks>
        /// </summary>
        ITypedWebRequest CreatedFrom { get; }

        IWebRequestResponse Response { get; }

        bool Redirected { get; }

        bool IsTimedOut { get; }

        bool IsAborted { get; }

        /// <summary>
        ///     Either <see cref="UnityWebRequest" /> or <see cref="HTTPRequest" />
        /// </summary>
        internal object nativeRequest { get; }

        event Action<IWebRequest> OnDownloadStarted;

        /// <summary>
        ///     In case the request is executed outside <see cref="IWebRequestController" /> it can be aborted manually
        /// </summary>
        void Abort();
    }
}
