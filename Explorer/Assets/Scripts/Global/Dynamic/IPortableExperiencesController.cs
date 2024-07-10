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
        Dictionary<string, Entity> PortableExperienceEntities { get; }

        UniTask CreatePortableExperienceAsync(URLDomain portableExperiencePath, CancellationToken ct);

        /// <summary>
        ///     Gracefully unload the current realm
        /// </summary>
        UniTask UnloadPortableExperienceAsync(string portableExperiencePath, CancellationToken ct);
    }
}
