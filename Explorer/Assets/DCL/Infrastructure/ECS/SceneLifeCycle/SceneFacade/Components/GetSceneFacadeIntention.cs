using DCL.Ipfs;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using Ipfs;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     An abstracted way of creating a scene facade
    /// </summary>
    public struct GetSceneFacadeIntention : ILoadingIntention, IEquatable<GetSceneFacadeIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly IIpfsRealm IpfsRealm;
        public readonly SceneDefinitionComponent DefinitionComponent;

        public GetSceneFacadeIntention(IIpfsRealm ipfsRealm, SceneDefinitionComponent definitionComponent)
        {
            IpfsRealm = ipfsRealm;
            DefinitionComponent = definitionComponent;

            CommonArguments = new CommonLoadingArguments(null!);
        }

        public bool Equals(GetSceneFacadeIntention other) =>
            Equals(IpfsRealm, other.IpfsRealm) && Equals(DefinitionComponent.Definition, other.DefinitionComponent.Definition);

        public override bool Equals(object obj) =>
            obj is GetSceneFacadeIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(IpfsRealm, DefinitionComponent.Definition);

        public override string ToString() =>
            $"Get Scene Facade: {DefinitionComponent.Definition?.id}";
    }
}
