using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Tests;
using Ipfs;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.Tests
{
    [TestFixture]
    public class LoadSceneDefinitionSystemShould : LoadSystemBaseShould<LoadSceneDefinitionSystem, IpfsTypes.SceneEntityDefinition, GetSceneDefinition>
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Content/bafkreibjkvobh26w7quie46edcwgpngs2lctfgvq26twinfh4aepeehno4"}";
        private string failPath => $"file://{Application.dataPath + "/../TestResources/Content/non_existing"}";
        private string wrongTypePath => $"file://{Application.dataPath + "/../TestResources/CRDT/arraybuffer.test"}";

        protected override GetSceneDefinition CreateSuccessIntention() =>
            new (new CommonLoadingArguments(successPath), new IpfsTypes.IpfsPath());

        protected override GetSceneDefinition CreateNotFoundIntention() =>
            new (new CommonLoadingArguments(failPath), new IpfsTypes.IpfsPath());

        protected override GetSceneDefinition CreateWrongTypeIntention() =>
            new (new CommonLoadingArguments(wrongTypePath), new IpfsTypes.IpfsPath());

        protected override LoadSceneDefinitionSystem CreateSystem() =>
            new (world, new WebRequestController(Substitute.For<IWebRequestsAnalyticsContainer>()), cache, new MutexSync());

        protected override void AssertSuccess(IpfsTypes.SceneEntityDefinition asset)
        {
            Assert.That(asset.metadata.scene.parcels, Is.EquivalentTo(new[]
            {
                "78,-1",
                "78,0",
                "79,-1",
                "79,0",
            }));
        }
    }
}
