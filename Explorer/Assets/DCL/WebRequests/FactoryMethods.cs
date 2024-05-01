using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Creates/Initialize Web Request based on common and specific arguments
    /// </summary>
    public delegate TWebRequest InitializeRequest<in T, out TWebRequest>(in CommonArguments commonArguments, T specificArguments)
        where T: struct
        where TWebRequest: struct, ITypedWebRequest;
}
