using DCL.Ipfs;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using NUnit.Framework;
using UnityEngine;

namespace ECS.SceneLifeCycle.Tests
{
    [TestFixture(WebRequestsMode.HTTP2)]
    [TestFixture(WebRequestsMode.UNITY)]
    [TestFixture(WebRequestsMode.YET_ANOTHER)]
    public class LoadSceneDefinitionSystemShould : LoadSystemBaseShould<LoadSceneDefinitionSystem, SceneEntityDefinition, GetSceneDefinition>
    {
        public LoadSceneDefinitionSystemShould(WebRequestsMode webRequestsMode) : base(webRequestsMode) { }

        private string successPath => $"file://{Application.dataPath + "/../TestResources/Content/bafkreibjkvobh26w7quie46edcwgpngs2lctfgvq26twinfh4aepeehno4"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Content/non_existing"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetSceneDefinition CreateSuccessIntention() =>
            new (new CommonLoadingArguments(successPath), new IpfsPath());

        protected override GetSceneDefinition CreateNotFoundIntention() =>
            new (new CommonLoadingArguments(failPath), new IpfsPath());

        protected override GetSceneDefinition CreateWrongTypeIntention() =>
            new (new CommonLoadingArguments(wrongTypePath), new IpfsPath());

        protected override LoadSceneDefinitionSystem CreateSystem(IWebRequestController webRequestController) =>
            new (world, webRequestController, cache);

        protected override void AssertSuccess(SceneEntityDefinition asset)
        {
            Assert.That(asset.metadata.scene.DecodedParcels, Is.EquivalentTo(new Vector2Int[]
            {
                new (78, -1),
                new (78, 0),
                new (79, -1),
                new (79, 0),
            }));
        }
    }
}
