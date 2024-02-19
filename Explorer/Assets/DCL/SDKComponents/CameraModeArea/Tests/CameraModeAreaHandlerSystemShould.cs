using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.SDKComponents.CameraModeArea.Systems;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;

namespace DCL.SDKComponents.CameraModeArea.Tests
{
    public class CameraModeAreaHandlerSystemShould : UnitySystemTestBase<CameraModeAreaHandlerSystem>
    {
        private Entity entity;
        private World globalWorld;

        // GameObject fakeCameraGO;

        [SetUp]
        public void Setup()
        {
            // fakeCameraGO = new GameObject();

            globalWorld = World.Create();
            var globalWorldProxy = new WorldProxy();
            globalWorldProxy.SetWorld(globalWorld);

            /*Entity cameraEntity = globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                new CameraComponent(cinemachinePreset.Brain.OutputCamera),
                new CursorComponent(),
                new CameraFieldOfViewComponent(),
                exposedCameraData,
                cinemachinePreset,
                new CinemachineCameraState(),
                cameraSamplingData,
                realmSamplingData
            );*/
            Entity cameraEntity = globalWorld.Create(
                new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY),
                new CameraComponent()
            );

            var cameraEntityProxy = new EntityProxy();
            cameraEntityProxy.SetEntity(cameraEntity);

            system = new CameraModeAreaHandlerSystem(world, globalWorldProxy, cameraEntityProxy);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);

            // entityTransformComponent = AddTransformToEntity(entity);
        }
    }
}
