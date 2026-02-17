using CommunicationData.URLHelpers;
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

        public readonly URLDomain ContentBaseUrl;
        public readonly SceneDefinitionComponent DefinitionComponent;

        public GetSceneFacadeIntention(URLDomain contentBaseUrl, SceneDefinitionComponent definitionComponent)
        {
            ContentBaseUrl = contentBaseUrl;
            DefinitionComponent = definitionComponent;

            // URL = EntityId just for identification, it is used by LoadSystemBase, it won't be used as a URL
            CommonArguments = new CommonLoadingArguments(definitionComponent.IpfsPath.EntityId);
        }

        public bool Equals(GetSceneFacadeIntention other) =>
            Equals(ContentBaseUrl, other.ContentBaseUrl) && Equals(DefinitionComponent.Definition, other.DefinitionComponent.Definition);

        public override bool Equals(object obj) =>
            obj is GetSceneFacadeIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ContentBaseUrl, DefinitionComponent.Definition);

        public override string ToString() =>
            $"Get Scene Facade: {DefinitionComponent.Definition?.id}";
    }
}
