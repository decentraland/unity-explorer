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
            ///     Creates a string object from the response.
            ///     <remarks>
            ///         <list type="bullet">
            ///             <item>This method can allocate heavily and must be used only if there are no other alternatives left</item>
            ///             <item>Will return an empty string if the response is not available or the stream is not a valid stream of characters</item>
            ///         </list>
            ///     </remarks>
            /// </summary>
            string Text { get; }

            /// <summary>
            ///     A human-readable string describing any system errors encountered by this UnityWebRequest object while handling HTTP requests or responses.
            ///     The default value is null is set if no response is received or the request has successfully finished.
            /// </summary>
            string? Error { get; }

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

            /// <summary>
            ///     <list type="bullet"></list>
            ///     <item>Allocates a new instance of <see cref="Dictionary{TKey,TValue}" /> and fills it with the headers of the response.</item>
            ///     <item>Returns `null` if response was not received at all.</item>
            /// </summary>
            Dictionary<string, string>? FlattenHeaders();
        }
    }
}
