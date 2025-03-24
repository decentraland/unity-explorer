using Best.HTTP;

namespace DCL.WebRequests
{
    /// <summary>
    /// A common interface acts as a generic constraint and should not be used as the interface itself
    /// </summary>
    public interface ITypedWebRequest<out TArgs> where TArgs: struct
    {
        RequestEnvelope<GetTextureArguments> Envelope { get; }

        /// <summary>
        ///     Web Request is assigned when the <see cref="IWebRequestController" /> executes the operation
        ///     <remarks>
        ///         <list type="bullet">
        ///             Depending on the implementation behaviour can vary:
        ///             <item><see cref="UnityWebRequest" /> instance is re-assigned to this property every time a new attempt is executed</item>
        ///             <item><see cref="HTTPRequest" /> instance is assigned only once (as it contains a built-in repetition mechanism)</item>
        ///         </list>
        ///     </remarks>
        /// </summary>
        IWebRequest? UnityWebRequest { get; set; }
    }
}
