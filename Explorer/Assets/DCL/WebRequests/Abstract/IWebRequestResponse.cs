using System.Collections.Generic;

namespace DCL.WebRequests
{
    public interface IWebRequestResponse
    {
        /// <summary>
        ///     If an exception occurred during reading of the response stream or can't connect to the server, this will be false
        /// </summary>
        bool Received { get; }

        /// <summary>
        ///     Creates a string object from the response.
        ///     <remarks>
        ///         <list type="bullet">
        ///             <item>This method can allocate heavily and must be used only if there are no other alternatives left</item>
        ///             <item>Will return an empty string if the response is not available or the stream is not a valid stream of characters</item>
        ///             <item>Disposes of the underlying handler</item>
        ///         </list>
        ///     </remarks>
        /// </summary>
        string Text { get; }

        /// <summary>
        ///     A human-readable string describing any system errors encountered by this UnityWebRequest object while handling HTTP requests or responses.
        ///     The default value is string.Empty is set if no response is received or the request has successfully finished.
        /// </summary>
        string Error { get; }

        /// <summary>
        ///     Creates a new array from the response
        ///     <remarks>
        ///         This method can allocate heavily and must be used only if there are no other alternatives left
        ///     </remarks>
        /// </summary>
        byte[] Data { get; }

        int StatusCode { get; }

        bool IsSuccess { get; }

        ulong DataLength { get; }

        /// <summary>
        ///     Gets the first value of the given header
        /// </summary>
        /// <returns></returns>
        string? GetHeader(string headerName);

        /// <summary>
        ///     <list type="bullet"></list>
        ///     <item>Allocates a new instance of <see cref="Dictionary{TKey,TValue}" /> and fills it with the headers of the response.</item>
        ///     <item>Returns `null` if response was not received at all.</item>
        /// </summary>
        Dictionary<string, string>? FlattenHeaders();
    }
}
