using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using NSubstitute;
using NUnit.Framework;
using SceneRunner;
using SceneRunner.Scene;
using System.Collections.Generic;
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
        private const string PATH = "cube-wave-16x16";

        [SetUp]
        public void SetUp()
        {
            IReportsHandlingSettings reportSettings = Substitute.For<IReportsHandlingSettings>();

            reportSettings.GetMatrix(Arg.Any<ReportHandler>())
                          .Returns(_ =>
                           {
                               ICategorySeverityMatrix matrix = Substitute.For<ICategorySeverityMatrix>();
                               matrix.IsEnabled(Arg.Any<string>(), Arg.Any<LogType>()).Returns(false);
                               return matrix;
                           });

            sceneSharedContainer = StaticSceneLauncher.Install(reportSettings);
        }

        [Test]
        public async Task EmitECSComponents()
        {
            // It will switch to the background thread and assign SynchronizationContext
            sceneFacade = await sceneSharedContainer.SceneFactory.CreateSceneFromStreamableDirectory(PATH, CancellationToken.None);

            // It will call `IEngineAPI.GetState()`
            await sceneFacade.StartScene();

            // The first tick should create all the components

            await sceneFacade.Tick(0);

            // after the tick we should wait for the next frame for the CommandBuffer to apply
            await UniTask.Yield(PlayerLoopTiming.Update);

            var sceneFacadeImpl = (SceneFacade)sceneFacade;

            // Check ECS world

            var world = sceneFacadeImpl.ecsWorldFacade.EcsWorld;

            var cubes = new QueryDescription().WithAll<SDKTransform, PBMeshRenderer>(); // 256 cubes
            Assert.AreEqual(256, world.CountEntities(in cubes));

            // save positions
            var positions = new Dictionary<Entity, Vector3>(256);

            world.Query(in cubes, (in Entity e, ref SDKTransform transform) => { positions[e] = transform.Position; });

            var textShape = new QueryDescription().WithAll<SDKTransform, PBTextShape, PBBillboard>(); // Billboard
            Assert.AreEqual(1, world.CountEntities(in textShape));

            await UniTask.SwitchToThreadPool();

            await sceneFacade.Tick(0.2f);

            // after the tick we should wait for the next frame for the CommandBuffer to apply
            await UniTask.Yield(PlayerLoopTiming.Update);

            // all positions must change

            Assert.AreEqual(256, world.CountEntities(in cubes));
            world.Query(in cubes, (in Entity e, ref SDKTransform transform) => { Assert.AreNotEqual(positions[e], transform.Position); });
        }

        [TearDown]
        public async Task Dispose()
        {
            if (sceneFacade != null)
                await sceneFacade.DisposeAsync();
        }
    }
}
