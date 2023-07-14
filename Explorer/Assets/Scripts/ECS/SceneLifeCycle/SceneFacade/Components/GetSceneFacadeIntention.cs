using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle
{
    /// <summary>
    ///     An abstracted way of creating a scene facade
    /// </summary>
    public struct GetSceneFacadeIntention : ILoadingIntention, IEquatable<GetSceneFacadeIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly IIpfsRealm IpfsRealm;
        public readonly IpfsTypes.IpfsPath IpfsPath;
        public readonly IpfsTypes.SceneEntityDefinition Definition;

        internal GetSceneFacadeIntention(IIpfsRealm ipfsRealm, IpfsTypes.IpfsPath ipfsPath, IpfsTypes.SceneEntityDefinition definition)
        {
            IpfsPath = ipfsPath;
            Definition = definition;
            IpfsRealm = ipfsRealm;

            // URL = EntityId just for identification, it is used by LoadSystemBase, it won't be used as a URL
            CommonArguments = new CommonLoadingArguments(ipfsPath.EntityId);
        }

        public bool Equals(GetSceneFacadeIntention other) =>
            Equals(IpfsRealm, other.IpfsRealm) && Equals(Definition, other.Definition);

        public override bool Equals(object obj) =>
            obj is GetSceneFacadeIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(IpfsRealm, Definition);
    }
}
