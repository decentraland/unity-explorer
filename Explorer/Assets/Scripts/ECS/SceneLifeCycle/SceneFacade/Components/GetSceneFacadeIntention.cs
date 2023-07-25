using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     An abstracted way of creating a scene facade
    /// </summary>
    public struct GetSceneFacadeIntention : ILoadingIntention, IEquatable<GetSceneFacadeIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly bool IsEmpty;
        public readonly IIpfsRealm IpfsRealm;
        public readonly IpfsTypes.IpfsPath IpfsPath;
        public readonly IpfsTypes.SceneEntityDefinition Definition;
        public readonly IReadOnlyList<Vector2Int> Parcels;

        internal GetSceneFacadeIntention(IIpfsRealm ipfsRealm, IpfsTypes.IpfsPath ipfsPath, IpfsTypes.SceneEntityDefinition definition, IReadOnlyList<Vector2Int> parcels, bool isEmpty)
        {
            IpfsPath = ipfsPath;
            Definition = definition;
            Parcels = parcels;
            IsEmpty = isEmpty;
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

        public override string ToString() =>
            $"Get Scene Facade: {Definition?.id}";
    }
}
