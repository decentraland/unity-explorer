using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     This interface is used as a constraint for generics and should not be referenced directly
    /// </summary>
    public interface ITypedWebRequest
    {
        UnityWebRequest UnityWebRequest { get; }
    }

    public static class TypedWebRequestExtensions
    {
        public static UniTask SendRequest(this ITypedWebRequest typedWebRequest, CancellationToken token) =>
            typedWebRequest.UnityWebRequest.SendWebRequest()!.WithCancellation(token);
    }
}
