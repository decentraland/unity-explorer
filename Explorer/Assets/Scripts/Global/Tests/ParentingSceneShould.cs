using Arch.Core;
using Cysharp.Threading.Tasks;
using Global;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.Unity.Systems.Tests
{
    /*
     Using this scene to test parenting. It instantiates 50 cubes, then starts switches the parent on each frame.

    export * from '@dcl/sdk'
    import { engine, Transform } from '@dcl/sdk/ecs'

    let currentParent = 0;
    let totalCubes = 50

    function createCube(x: number, y: number, z: number) {
      const myEntity = engine.addEntity(true)

      Transform.create(myEntity, {
        position: { x, y, z },
      })

      return myEntity
    }

    function DoParenting(dt: number) {
      let entitiesWithTransform = engine.getEntitiesWith(Transform)
      const entityArray = Array.from(entitiesWithTransform, ([entity]) => entity);

      Transform.getMutable(entityArray[currentParent]).parent = undefined

      var counter = 0
      entitiesWithTransform = engine.getEntitiesWith(Transform)
      for (const [entity] of entitiesWithTransform) {
        if (counter != currentParent) {
          Transform.getMutable(entity).parent = entityArray[currentParent]
        }
        counter++
      }
      currentParent = (currentParent + 1) % totalCubes
    }

    for (let x = 0; x < totalCubes; x += 1) {
        createCube(x, 0, 0)
    }

    engine.addSystem(DoParenting)


    */

    [TestFixture]
    public class ParentingSceneShould
    {
        private SceneSharedContainer sceneSharedContainer;
        private ISceneFacade sceneFacade;
        private string path;

        [SetUp]
        public void SetUp()
        {
            sceneSharedContainer = EntryPoint.Install();

            path = $"file://{Application.dataPath + "/../TestResources/Scenes/Parenting/parenting.js"}";
        }

        [Test]
        public async Task ParentObjects()
        {
            sceneFacade = await sceneSharedContainer.SceneFactory.CreateScene(path, CancellationToken.None);

            await sceneFacade.StartScene();

            //We tick once to create all objects
            await sceneFacade.Tick(0);
            await UniTask.Yield(PlayerLoopTiming.Update);

            //We tick a second time to parent objects
            await sceneFacade.Tick(0);
            await UniTask.Yield(PlayerLoopTiming.Update);

            var sceneFacadeImpl = (SceneFacade)sceneFacade;
            World world = sceneFacadeImpl.ecsWorldFacade.EcsWorld;

            Transform rootSceneTransform = null;

            world.Query(new QueryDescription().WithAny<Transform>(), (in Entity e, ref Transform transform) =>
            {
                if (transform.name.Equals("SCENE_ROOT")) { rootSceneTransform = transform; }
            });

            //Check that the first child is parent of the rest
            Assert.AreEqual(1, rootSceneTransform.childCount);
            Assert.AreEqual(49, rootSceneTransform.GetChild(0).childCount);

            await sceneFacade.Tick(0);
            await UniTask.Yield(PlayerLoopTiming.Update);

            //Check the parent has changed
            Assert.AreEqual(1, rootSceneTransform.childCount);
            Assert.AreEqual(49, rootSceneTransform.GetChild(0).childCount);
            Assert.AreNotEqual(rootSceneTransform, rootSceneTransform.GetChild(0));
        }

        [TearDown]
        public void Dispose()
        {
            sceneFacade?.DisposeAsync();
        }
    }
}
