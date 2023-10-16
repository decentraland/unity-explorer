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
        UniTask SetRealm(GlobalWorld globalWorld, URLDomain realm, CancellationToken ct);

        /// <summary>
        ///     Gracefully unload the current realm
        /// </summary>
        UniTask UnloadCurrentRealm(GlobalWorld globalWorld);

        /// <summary>
        ///     Dispose everything on application quit
        /// </summary>
        UniTask DisposeGlobalWorld(GlobalWorld globalWorld);
    }
}
