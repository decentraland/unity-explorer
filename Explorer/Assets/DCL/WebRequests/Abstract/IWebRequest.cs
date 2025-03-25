using Best.HTTP;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     This abstraction allows to switch easily between <see cref="UnityWebRequest" /> and <see cref="HTTPRequest" />
    ///     <remarks>
    ///         The APIs of <see cref="HTTPRequest" /> and <see cref="UnityWebRequest" /> have very little in common, this abstraction aims to provide very basics.
    ///         In other cases individual code in the respective implementation of <see cref="ITypedWebRequest" /> should be used
    ///     </remarks>
    /// </summary>
    public partial interface IWebRequest : IDisposable
    {
        string Url { get; }

        /// <summary>
        ///     Response will be null if the request is not yet completed
        /// </summary>
        IResponse Response { get; }

        bool Redirected { get; }

        /// <summary>
        ///     Either <see cref="UnityWebRequest" /> or <see cref="HTTPRequest" />
        /// </summary>
        internal object nativeRequest { get; }

        public interface IResponse
        {
            /// <summary>
            ///     Creates a string object from the response
            ///     <remarks>
            ///         This method can allocate heavily and must be used only if there are no other alternatives left
            ///     </remarks>
            /// </summary>
            string Text { get; }

            /// <summary>
            ///     Creates a new array from the response
            ///     <remarks>
            ///         This method can allocate heavily and must be used only if there are no other alternatives left
            ///     </remarks>
            /// </summary>
            byte[] Data { get; }

            int StatusCode { get; }

            bool IsSuccess { get; }

            bool IsTimedOut { get; }

            /// <summary>
            ///     Gets the first value of the given header
            /// </summary>
            /// <returns></returns>
            string GetHeader(string headerName);

            Dictionary<string, string> FlattenHeaders();
        }
    }
}
