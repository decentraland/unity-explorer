using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS;
using System.Collections.Generic;
using System.Threading;

namespace Global.Dynamic
{
    public interface IPortableExperiencesController
    {
        GlobalWorld GlobalWorld { get; set; }
        List<Entity> RealmEntities { get; }

        UniTask CreatePortableExperienceAsync(URLDomain portableExperiencePath, CancellationToken ct);

        UniTask<bool> IsReachableAsync(URLDomain realm, CancellationToken ct);

        IRealmData GetRealm();

        /// <summary>
        ///     Gracefully unload the current realm
        /// </summary>
        UniTask UnloadCurrentRealmAsync();

        /// <summary>
        ///     Dispose everything on application quit
        /// </summary>
        void DisposeGlobalWorld();
    }
}
