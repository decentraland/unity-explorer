using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Threading;
using UnityEngine;

namespace Global.Dynamic
{
    public interface IRealmController
    {
        GlobalWorld GlobalWorld { set; }

        /// <summary>
        ///     Unload the current realm and load the new one
        /// </summary>
        UniTask SetRealmAsync(URLDomain realm, Vector2Int playerStartPosition, AsyncLoadProcessReport? loadReport, CancellationToken ct);

        /// <summary>
        ///     Gracefully unload the current realm
        /// </summary>
        UniTask UnloadCurrentRealmAsync();

        /// <summary>
        ///     Dispose everything on application quit
        /// </summary>
        UniTask DisposeGlobalWorldAsync();
    }
}
