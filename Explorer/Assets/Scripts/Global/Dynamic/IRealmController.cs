using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Global.Dynamic
{
    public interface IRealmController
    {
        /// <summary>
        ///     Setup the GlobalWorld
        /// </summary>
        void SetupWorld(GlobalWorld world);

        /// <summary>
        ///     Unload the current realm and load the new one
        /// </summary>
        UniTask SetRealmAsync(URLDomain realm, CancellationToken ct);

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
