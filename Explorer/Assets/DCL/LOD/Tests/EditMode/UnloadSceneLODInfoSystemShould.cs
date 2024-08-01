using Arch.Core;
using Arch.System;
using DCL.Ipfs;
using DCL.LOD.Components;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NSubstitute;
using NSubstitute.Exceptions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.Utilities;
using Assert = UnityEngine.Assertions.Assert;

namespace DCL.LOD.Tests
{
    public class UnloadSceneLODInfoSystemShould : UnitySystemTestBase<UnloadSceneLODSystem>
    {
        private SceneLODInfo SceneLODInfo;
        private ILODCache lodCache;
        private IScenesCache scenesCache;

        private SceneDefinitionComponent sceneDefinitionComponent;

        private const string CachedSceneID = "CachedSceneID";

        private const int LOD_PREWARM_VALUE = 5;
        
        [SetUp]
        public void Setup()
        {
            scenesCache = Substitute.For<IScenesCache>();
            lodCache = new LODCache();
            lodCache.PrewarmLODGroupPool(2, LOD_PREWARM_VALUE);

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedBase = new Vector2Int(0, 0), DecodedParcels = new Vector2Int[]
                        {
                            new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1)
                        }
                    }
                }
            };
            sceneDefinitionComponent = new SceneDefinitionComponent(sceneEntityDefinition, new IpfsPath());


            system = new UnloadSceneLODSystem(world, scenesCache, lodCache);
        }

        [Test]
        public void LODCached()
        {
            //Arrange

            var sceneLODInfo = SceneLODInfo.Create();
            sceneLODInfo.id = CachedSceneID;
            sceneLODInfo.metadata = lodCache.Get("fake", 0);
            sceneLODInfo.metadata.SuccessfullLODs = SceneLODInfoUtils.SetLODResult(sceneLODInfo.metadata.SuccessfullLODs, 0);

            var createdEntity = world.Create(sceneDefinitionComponent, sceneLODInfo, new VisualSceneState());
            //One empty update to allow creation
            system.Update(0);


            //Act
            world.Add<DeleteEntityIntention>(createdEntity);
            system.Update(0);

            //Assert
            Assert.IsFalse(world.Has<SceneLODInfo, DeleteEntityIntention, VisualSceneState>(createdEntity));
            Assert.AreEqual(((LODCache)lodCache).lodCache.Count, 1);
            Assert.AreEqual(((LODCache)lodCache).lodsGroupPool.CountInactive, LOD_PREWARM_VALUE - 1);
        }

        [Test]
        public void LODDismissed()
        {
            //Arrange
            var sceneLODInfo = SceneLODInfo.Create();
            sceneLODInfo.id = CachedSceneID;
            sceneLODInfo.metadata = lodCache.Get("fake", 0);

            var createdEntity = world.Create(sceneDefinitionComponent, sceneLODInfo, new VisualSceneState());
            //One empty update to allow creation
            system.Update(0);


            //Act
            world.Add<DeleteEntityIntention>(createdEntity);
            system.Update(0);

            //Assert
            Assert.IsFalse(world.Has<SceneLODInfo, DeleteEntityIntention, VisualSceneState>(createdEntity));
            Assert.AreEqual(((LODCache)lodCache).lodCache.Count, 0);
            Assert.AreEqual(((LODCache)lodCache).lodsGroupPool.CountInactive, LOD_PREWARM_VALUE);
        }
    }
}