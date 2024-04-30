using Cysharp.Threading.Tasks;
using System;
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
        public static UniTask SendRequest<T>(this T typedWebRequest, CancellationToken token) where T : ITypedWebRequest =>
            typedWebRequest.UnityWebRequest.SendWebRequest()!.WithCancellation(token);
    }
}
