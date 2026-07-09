using CommunicationData.URLHelpers;
using DCL.Ipfs;
using DCL.SceneRunner.Scene;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
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

        public readonly SceneDefinitionComponent DefinitionComponent;

        /// <summary>
        ///     ISSDescriptor for this scene, captured by reference at intention-creation time. The radius
        ///     system gates SHOWING_SCENE transitions on descriptor resolution, so by the time this intention
        ///     is constructed the descriptor is guaranteed to be in a resolved state.
        /// </summary>
        public readonly ISSDescriptor ISSDescriptor;

        /// <summary>
        ///     Set only for authoritative-multiplayer Portable Experiences: the PX world's realm data, used by the
        ///     scene loading flow to establish the scene-comms room. Null for ordinary scenes and non-authoritative PX.
        /// </summary>
        public readonly IRealmData? PortableExperienceRealm;

        public GetSceneFacadeIntention(SceneDefinitionComponent definitionComponent, ISSDescriptor issDescriptor, IRealmData? portableExperienceRealm = null)
        {
            DefinitionComponent = definitionComponent;
            ISSDescriptor = issDescriptor;
            PortableExperienceRealm = portableExperienceRealm;

            // URL = EntityId just for identification, it is used by LoadSystemBase, it won't be used as a URL
            CommonArguments = new CommonLoadingArguments(definitionComponent.IpfsPath.EntityId);
        }

        public bool Equals(GetSceneFacadeIntention other) =>
            Equals(DefinitionComponent.Definition, other.DefinitionComponent.Definition);

        public override bool Equals(object obj) =>
            obj is GetSceneFacadeIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(DefinitionComponent.Definition);

        public override string ToString() =>
            $"Get Scene Facade: {DefinitionComponent.Definition?.id}";
    }
}
