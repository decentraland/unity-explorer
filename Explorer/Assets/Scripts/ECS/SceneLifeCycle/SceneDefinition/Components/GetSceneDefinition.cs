using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a single scene definition from URN
    /// </summary>
    public struct GetSceneDefinition : ILoadingIntention, IEquatable<GetSceneDefinition>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly IpfsPath IpfsPath;

        public GetSceneDefinition(CommonLoadingArguments commonArguments, IpfsPath ipfsPath)
        {
            CommonArguments = commonArguments;
            IpfsPath = ipfsPath;
        }

        public bool Equals(GetSceneDefinition other) =>
            IpfsPath.EntityId == other.IpfsPath.EntityId;

        public override bool Equals(object obj) =>
            obj is GetSceneDefinition other && Equals(other);

        public override int GetHashCode() =>
            IpfsPath.EntityId.GetHashCode();

        public override string ToString() =>
            $"Get Scene Definition: {IpfsPath.ToString()}, {CommonArguments.URL}";
    }
}
