using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Global.Editor
{
    /*
     https://github.com/decentraland/sdk7-goerli-plaza/blob/main/cube-wave-16x16/src/index.ts
     export * from '@dcl/sdk'

    import { engine, Transform, MeshRenderer, TextShape, Billboard } from '@dcl/sdk/ecs'
    import { CircleHoverSystem } from './circularSystem'

    // My cube generator
    function createCube(x: number, y: number, z: number) {
        // Dynamic entity because we aren't loading static entities out of this scene code
        const myEntity = engine.addEntity(true)

        Transform.create(myEntity, {
        position: { x, y, z }
        })

        MeshRenderer.setBox(myEntity)

        return myEntity
    }

    for (let x = 0.5; x < 16; x += 1) {
        for (let y = 0.5; y < 16; y += 1) {
            createCube(x, 0, y)
        }
    }

    engine.addSystem(CircleHoverSystem)

    const sign = engine.addEntity(true)
    Transform.create(sign, {
    position: { x: 8, y: 6, z: 8 },
    scale: { x: 1.2, y: 1.2, z: 1.2 }
    })

    TextShape.create(sign, {
    text: 'Stress test SDK v7.0-EA\n16x16 cubes',
    fontAutoSize: false,
    fontSize: 5,
    height: 2,
    width: 4,
    outlineWidth: 0.1,
    outlineColor: { r: 0, g: 0, b: 1 },
    textColor: { r: 1, g: 0, b: 0, a: 1 }
    })

    Billboard.create(sign)*/

    [TestFixture]
    public class CubeWaveSceneShould
    {
        private SceneSharedContainer sceneSharedContainer;
        private ISceneFacade sceneFacade;
        private string path;

        [SetUp]
        public void SetUp()
        {
            sceneSharedContainer = EntryPoint.Install();

            path = $"file://{Application.dataPath + "/../TestResources/Scenes/CubeWave/cube_waves.js"}";
        }

        [Test]
        public async Task EmitECSComponents()
        {
            // It will switch to the background thread and assign SynchronizationContext
            sceneFacade = await sceneSharedContainer.SceneFactory.CreateScene(path, CancellationToken.None);

            // It will call `IEngineAPI.GetState()`
            await sceneFacade.StartScene();

            // The first tick should create all the components

            await sceneFacade.Tick(0);

            // after the tick we should wait for the next frame for the CommandBuffer to apply
            await UniTask.Yield(PlayerLoopTiming.Update);

            // 256 cubes

            var sceneFacadeImpl = (SceneFacade)sceneFacade;

            // Check ECS world

            var world = sceneFacadeImpl.ecsWorldFacade.EcsWorld;
            var updateTransformSystem = new UpdateTransformSystem(world);

            var cubes = new QueryDescription().WithAll<SDKTransform, PBMeshRenderer>(); // 256 cubes
            Assert.AreEqual(256, world.CountEntities(in cubes));

            var textShape = new QueryDescription().WithAll<SDKTransform, PBTextShape, PBBillboard>(); // Billboard
            Assert.AreEqual(1, world.CountEntities(in textShape));

            for (var i = 0; i < 1; i++)
            {
                await sceneFacade.Tick(0);
                await UniTask.Yield(PlayerLoopTiming.Update);
                world.Query(in new QueryDescription().WithAll<SDKTransform>(), (ref SDKTransform sdkTransform) => { Debug.Log("Before Update" + sdkTransform.IsDirty); });
                updateTransformSystem.Update(0);
                world.Query(in new QueryDescription().WithAll<SDKTransform>(), (ref SDKTransform sdkTransform) => { Debug.Log("After Update " + sdkTransform.IsDirty); });
            }
        }

        [TearDown]
        public void Dispose()
        {
            sceneFacade?.Dispose();
        }
    }
}
