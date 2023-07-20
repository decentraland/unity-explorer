using Arch.Core;
using Arch.Core.Extensions;
using Cysharp.Threading.Tasks;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using Ipfs;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.SceneLifeCycle.Tests
{
    public class LoadFixedPointersShould : UnitySystemTestBase<LoadFixedPointersSystem>
    {
        private static readonly string[] URNs =
        {
            "urn:decentraland:entity:bafkreibjkvobh26w7quie46edcwgpngs2lctfgvq26twinfh4aepeehno4?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/",
            "urn:decentraland:entity:bafkreihh3b5zjpb252blfa6b2n5lpr63pl5tdwhcxjidxx6vpytjjhbxou?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/",
            "urn:decentraland:entity:bafkreidnrsziglqgwwdsvtyrdfltiobpymk3png56xieemixlprqbw5gru?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/",
            "urn:decentraland:entity:bafkreibk3kvp2mtujcfciothfbft6hf3kaveenzlef7xlwvqcr5udtk3be?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/",
            "urn:decentraland:entity:bafkreidvur64pwmywtjobkfdr6xl6chgh2tdgbutclkyb4hcvwtk2lizii?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/",
            "urn:decentraland:entity:bafkreiff5m4wv2pm6n4muiyy7p6yrohsqqnaggwhvcb2lzbbwiogpgkl2i?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/",
        };

        [SetUp]
        public void SetUp()
        {
            system = new LoadFixedPointersSystem(world);
        }

        [Test]
        public void CreatePromises()
        {
            Entity e = world.Create(new RealmComponent(new TestIpfsRealm(URNs)));

            system.Update(0);

            // all promises should exist

            Assert.That(world.TryGet(e, out FixedScenePointers fixedPointers), Is.True);

            Assert.That(fixedPointers.Promises.Length, Is.EqualTo(URNs.Length));
            Assert.That(fixedPointers.Promises.Any(p => p.LoadingIntention.IpfsPath.EntityId == "bafkreibjkvobh26w7quie46edcwgpngs2lctfgvq26twinfh4aepeehno4"), Is.True);
            Assert.That(fixedPointers.Promises.Any(p => p.LoadingIntention.IpfsPath.EntityId == "bafkreihh3b5zjpb252blfa6b2n5lpr63pl5tdwhcxjidxx6vpytjjhbxou"), Is.True);
            Assert.That(fixedPointers.Promises.Any(p => p.LoadingIntention.IpfsPath.EntityId == "bafkreidnrsziglqgwwdsvtyrdfltiobpymk3png56xieemixlprqbw5gru"), Is.True);
            Assert.That(fixedPointers.Promises.Any(p => p.LoadingIntention.IpfsPath.EntityId == "bafkreibk3kvp2mtujcfciothfbft6hf3kaveenzlef7xlwvqcr5udtk3be"), Is.True);
            Assert.That(fixedPointers.Promises.Any(p => p.LoadingIntention.IpfsPath.EntityId == "bafkreidvur64pwmywtjobkfdr6xl6chgh2tdgbutclkyb4hcvwtk2lizii"), Is.True);
            Assert.That(fixedPointers.Promises.Any(p => p.LoadingIntention.IpfsPath.EntityId == "bafkreiff5m4wv2pm6n4muiyy7p6yrohsqqnaggwhvcb2lzbbwiogpgkl2i"), Is.True);
        }

        [Test]
        public async Task CreateSceneEntityFromLoadedPromises()
        {
            var ipfsRealm = new TestIpfsRealm();

            // create promises
            UniTask<AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>>[] promises = URNs.Take(2)
                                                                                                        .Select(async urn =>
                                                                                                         {
                                                                                                             IpfsTypes.IpfsPath path = IpfsHelper.ParseUrn(urn);

                                                                                                             var promise = AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>
                                                                                                                .Create(world, new GetSceneDefinition(new CommonLoadingArguments(), path), PartitionComponent.TOP_PRIORITY);

                                                                                                             // resolve it
                                                                                                             UnityWebRequestAsyncOperation request = UnityWebRequest.Get(ipfsRealm.ContentBaseUrl + path.EntityId).SendWebRequest();
                                                                                                             await request;

                                                                                                             world.Add(promise.Entity, new StreamableLoadingResult<IpfsTypes.SceneEntityDefinition>(
                                                                                                                 JsonUtility.FromJson<IpfsTypes.SceneEntityDefinition>(request.webRequest.downloadHandler.text)));

                                                                                                             return promise;
                                                                                                         })
                                                                                                        .ToArray();

            AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>[] results = await promises;

            // Create realm + fixed pointers

            Entity realm = world.Create(new RealmComponent(ipfsRealm), new FixedScenePointers(results));

            system.Update(0);

            QueryDescription q = new QueryDescription().WithAll<SceneDefinitionComponent>();
            var entities = new List<Entity>();
            world.GetEntities(in q, entities);

            Assert.That(entities.Count, Is.EqualTo(2));

            var definitions = entities.Select(e => world.Get<SceneDefinitionComponent>(e)).ToList();
            Assert.That(definitions.Any(d => d.IpfsPath.EntityId == "bafkreibjkvobh26w7quie46edcwgpngs2lctfgvq26twinfh4aepeehno4"), Is.True);
            Assert.That(definitions.Any(d => d.IpfsPath.EntityId == "bafkreihh3b5zjpb252blfa6b2n5lpr63pl5tdwhcxjidxx6vpytjjhbxou"), Is.True);
        }
    }
}
