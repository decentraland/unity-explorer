using Arch.Core;
using Cinemachine;
using DCL.Audio;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.CharacterCamera.Systems;
using DCL.Input;
using DCL.Input.Component;
using ECS.Abstract;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using ControlCinemachineVirtualCameraSystem = DCL.Character.CharacterCamera.Systems.ControlCinemachineVirtualCameraSystem;

namespace DCL.CharacterCamera.Tests
{
    public class CinemachineVirtualCameraSystemShould : UnitySystemTestBase<ControlCinemachineVirtualCameraSystem>
    {
        private const float ZOOM_SENSITIVITY = 0.05f;

        private Camera camera;
        private GameObject cinemachineObj;
        private ICinemachinePreset cinemachinePreset;
        private ICinemachineCameraAudioSettings cinemachineCameraAudioSettings;
        private Entity entity;
        private ICinemachineFirstPersonCameraData firstPersonCameraData;
        private ICinemachineFreeCameraData freeCameraData;

        private SingleInstanceEntity inputMap;
        private ICinemachineThirdPersonCameraData thirdPersonCameraData;
        private ICinemachineThirdPersonCameraData droneViewData;

        [SetUp]
        public void CreateCameraSetup()
        {
            camera = new GameObject("Camera Test").AddComponent<Camera>();
            cinemachineObj = new GameObject("Cinemachine");

            CinemachineVirtualCamera firstPersonCamera = new GameObject("First Person Camera").AddComponent<CinemachineVirtualCamera>();
            firstPersonCamera.transform.SetParent(cinemachineObj.transform);
            firstPersonCamera.AddCinemachineComponent<CinemachineTransposer>();
            CinemachinePOV pov = firstPersonCamera.AddCinemachineComponent<CinemachinePOV>();
            firstPersonCameraData = Substitute.For<ICinemachineFirstPersonCameraData>();
            firstPersonCameraData.Camera.Returns(firstPersonCamera);
            firstPersonCameraData.POV.Returns(pov);

            CinemachineFreeLook thirdPersonCamera = new GameObject("Third Person Camera").AddComponent<CinemachineFreeLook>();
            thirdPersonCamera.transform.SetParent(cinemachineObj.transform);
            thirdPersonCameraData = Substitute.For<ICinemachineThirdPersonCameraData>();
            thirdPersonCameraData.Camera.Returns(thirdPersonCamera);
            thirdPersonCameraData.CameraOffset.Returns(thirdPersonCamera.gameObject.AddComponent<CinemachineCameraOffset>());

            CinemachineFreeLook droneView = new GameObject("Third Person Camera Drone").AddComponent<CinemachineFreeLook>();
            droneView.transform.SetParent(cinemachineObj.transform);
            droneViewData = Substitute.For<ICinemachineThirdPersonCameraData>();
            droneViewData.Camera.Returns(droneView);
            droneViewData.CameraOffset.Returns(droneView.gameObject.AddComponent<CinemachineCameraOffset>());

            CinemachineVirtualCamera freeCamera = new GameObject("Free Camera").AddComponent<CinemachineVirtualCamera>();
            freeCamera.transform.SetParent(cinemachineObj.transform);
            CinemachinePOV freeCamPov = freeCamera.AddCinemachineComponent<CinemachinePOV>();
            freeCameraData = Substitute.For<ICinemachineFreeCameraData>();
            freeCameraData.Camera.Returns(freeCamera);
            freeCameraData.POV.Returns(freeCamPov);

            CinemachineBrain brain = cinemachineObj.AddComponent<CinemachineBrain>();
            cinemachinePreset = Substitute.For<ICinemachinePreset>();
            cinemachinePreset.Brain.Returns(brain);
            cinemachinePreset.FirstPersonCameraData.Returns(firstPersonCameraData);
            cinemachinePreset.FreeCameraData.Returns(freeCameraData);
            cinemachinePreset.ThirdPersonCameraData.Returns(thirdPersonCameraData);
            cinemachinePreset.DroneViewCameraData.Returns(droneViewData);
            cinemachinePreset.DefaultCameraMode.Returns(CameraMode.ThirdPerson);
            cinemachineCameraAudioSettings = Substitute.For<ICinemachineCameraAudioSettings>();
            system = new ControlCinemachineVirtualCameraSystem(world, cinemachineCameraAudioSettings);
            world.Create(new InputMapComponent());

            entity = world.Create(cinemachinePreset, new CameraComponent(camera), new CinemachineCameraState(), new CameraInput(), new CursorComponent());

            system.Initialize();
        }

        [TearDown]
        public void DisposeCameraSetup()
        {
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void InitInputMapComponent()
        {
            // third person
            Assert.That(inputMap.GetInputMapComponent(world).Active, Is.EqualTo(InputMapComponent.Kind.Player));
            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));
        }

        [Test]
        public void SwitchFromThirdToFirstPerson()
        {
            world.Set(entity, new CameraInput { ZoomIn = true });
            system.Update(1);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(firstPersonCameraData.Camera));
        }

        [TestCase(CameraMode.FirstPerson, 1, CameraMode.ThirdPerson)]
        [TestCase(CameraMode.ThirdPerson, 1, CameraMode.DroneView)]
        [TestCase(CameraMode.DroneView, 1, CameraMode.DroneView)]
        [TestCase(CameraMode.DroneView, -1, CameraMode.ThirdPerson)]
        [TestCase(CameraMode.ThirdPerson, -1, CameraMode.FirstPerson)]
        [TestCase(CameraMode.FirstPerson, -1, CameraMode.FirstPerson)]
        public void ZoomChangesStates(CameraMode currentState, int zoomDirection, CameraMode expectedState)
        {
            world.Set(entity, new CameraInput { ZoomOut = zoomDirection > 0, ZoomIn = zoomDirection < 0 });
            world.Set(entity, new CameraComponent { Mode = currentState });
            system.Update(1);

            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(expectedState));
        }

        [Test]
        public void ChangeShouldersWhenOnThirdPersonCamera()
        {
            world.Set(entity, new CursorComponent
                { CursorState = CursorState.Locked });
            world.Set(entity, new CameraInput { ChangeShoulder = true });
            world.Set(entity, new CameraComponent { Mode = CameraMode.ThirdPerson, Shoulder = ThirdPersonCameraShoulder.Right });

            system.Update(1);
            Assert.That(world.Get<CameraComponent>(entity).Shoulder, Is.EqualTo(ThirdPersonCameraShoulder.Left));

            system.Update(1);
            Assert.That(world.Get<CameraComponent>(entity).Shoulder, Is.EqualTo(ThirdPersonCameraShoulder.Right));
        }

        [Test]
        public void ZoomOutInFirstPersonIsCancelledWhenCursorIsOverUI()
        {
            world.Set(entity, new CameraInput { ZoomOut = true });
            world.Set(entity, new CinemachineCameraState { CurrentCamera = firstPersonCameraData.Camera });

            world.Set(entity, new CursorComponent
                { IsOverUI = true });

            system.Update(1);

            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(cameraState.CurrentCamera, Is.EqualTo(firstPersonCameraData.Camera));
            Assert.That(cameraState.ThirdPersonZoomValue, Is.EqualTo(0));
        }

        [Test]
        public void SwitchStatePingPong()
        {
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));
            world.Set(entity, new CameraInput { SwitchState = true });

            system.Update(1);
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.DroneView));

            system.Update(1);
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));

            system.Update(1);
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.FirstPerson));

            system.Update(1);
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));

            world.Set(entity, new CameraInput { SwitchState = false });

            system.Update(1);
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));
        }

        [Test]
        public void IgnoreCameraModeInputIfDisabled()
        {
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            world.Set(entity, new CameraInput { ZoomIn = true });

            // lock camera mode input
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.AddCameraInputLock(system);
            world.Set(entity, component);

            system.Update(1);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            // unlock camera mode input
            component.RemoveCameraInputLock(system);
            world.Set(entity, component);

            system.Update(1);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(firstPersonCameraData.Camera));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void AdaptToCameraModeFromComponent(bool lockInput)
        {
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));

            CameraComponent component = world.Get<CameraComponent>(entity);
            component.Mode = CameraMode.Free;

            if (lockInput)
            {
                // Input that would take it to 'First Person'
                world.Set(entity, new CameraInput { ZoomIn = true });
                component.AddCameraInputLock(system);
            }

            world.Set(entity, component);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            system.Update(1);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(freeCameraData.Camera));
        }
    }
}
