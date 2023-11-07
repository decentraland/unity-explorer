using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Global.Dynamic
{
    public interface IRealmController
    {
        /// <summary>
        ///     Unload the current realm and load the new one
        /// </summary>
        UniTask SetRealmAsync(GlobalWorld globalWorld, URLDomain realm, CancellationToken ct);

        /// <summary>
        ///     Gracefully unload the current realm
        /// </summary>
        UniTask UnloadCurrentRealmAsync(GlobalWorld globalWorld);

        /// <summary>
        ///     Dispose everything on application quit
        /// </summary>
        UniTask DisposeGlobalWorldAsync(GlobalWorld globalWorld);
    }
}
