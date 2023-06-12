using Arch.Core;
using CrdtEcsBridge.Components.Special;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Tests
{
    public class ResolveScenesStateSystemShould : UnitySystemTestBase<ResolveScenesStateSystem>
    {
        private SceneLifeCycleState state;

        private static readonly QueryDescription SCENE_LOADING_COMPONENTS = new QueryDescription().WithAll<SceneLoadingComponent>();

        [SetUp]
        public void SetUp()
        {
            Entity playerEntity = world.Create(new PlayerComponent());
            AddTransformToEntity(playerEntity);

            system = new ResolveScenesStateSystem(world, state = new SceneLifeCycleState
            {
                SceneLoadRadius = 2,
                PlayerEntity = playerEntity,
            });
        }

        [Test]
        public void WaitForAssetBundleManifestResolution()
        {
            var scenePointer = new ScenePointer(new IpfsTypes.SceneEntityDefinition
            {
                id = "scene1",
                content = new List<IpfsTypes.ContentDefinition>(),
                metadata = new IpfsTypes.SceneMetadata(),
                pointers = new List<string>(),
            }, AssetPromise<SceneAssetBundleManifest, GetAssetBundleManifestIntention>.Create(world, new GetAssetBundleManifestIntention("scene1")));

            state.ScenePointers.Add(new Vector2Int(0, 0), scenePointer);

            // the promise is unresolved
            system.Update(0);

            // there should be no Live scenes
            Assert.That(world.CountEntities(in SCENE_LOADING_COMPONENTS), Is.EqualTo(0));

            world.Add(scenePointer.ManifestPromise.Entity, new StreamableLoadingResult<SceneAssetBundleManifest>(SceneAssetBundleManifest.NULL));

            // the promise is resolved

            system.Update(0);

            Assert.That(world.CountEntities(in SCENE_LOADING_COMPONENTS), Is.EqualTo(1));
        }
    }
}
