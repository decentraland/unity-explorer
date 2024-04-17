using Arch.Core;
using Cinemachine;
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

namespace DCL.CharacterCamera.Tests
{
    public class CinemachineVirtualCameraSystemShould : UnitySystemTestBase<ControlCinemachineVirtualCameraSystem>
    {
        private const float ZOOM_SENSITIVITY = 0.05f;

        private Camera camera;
        private GameObject cinemachineObj;
        private ICinemachinePreset cinemachinePreset;
        private Entity entity;
        private ICinemachineFirstPersonCameraData firstPersonCameraData;
        private ICinemachineFreeCameraData freeCameraData;

        private SingleInstanceEntity inputMap;
        private ICinemachineThirdPersonCameraData thirdPersonCameraData;

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
            thirdPersonCameraData.ZoomSensitivity.Returns(ZOOM_SENSITIVITY);
            thirdPersonCameraData.ZoomInOrbitThreshold.Returns(new CinemachineFreeLook.Orbit[3]);
            thirdPersonCameraData.ZoomOutOrbitThreshold.Returns(new CinemachineFreeLook.Orbit[3]);

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
            cinemachinePreset.DefaultCameraMode.Returns(CameraMode.ThirdPerson);

            DCLInput dclInput = Substitute.For<DCLInput>();
            system = new ControlCinemachineVirtualCameraSystem(world, dclInput);
            world.Create(new InputMapComponent());

            inputMap = world.CacheInputMap();

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

        [Test]
        public void ZoomOutInThirdPerson()
        {
            world.Set(entity, new CameraInput { ZoomOut = true });
            system.Update(1);

            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));
            Assert.That(cameraState.ThirdPersonZoomValue, Is.EqualTo(ZOOM_SENSITIVITY));
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
        public void SwitchFromThirdPersonToFree()
        {
            world.Set(entity, new CameraInput { ZoomOut = true }, new CinemachineCameraState
            {
                CurrentCamera = thirdPersonCameraData.Camera,
                ThirdPersonZoomValue = 1f,
            });

            system.Update(1);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(freeCameraData.Camera));
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.Free));
        }

        [Test]
        public void IgnoreCameraModeInputIfDisabled()
        {
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            world.Set(entity, new CameraInput { ZoomIn = true });

            // lock camera mode input
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.AddCameraInputLock();
            world.Set(entity, component);

            system.Update(1);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            // unlock camera mode input
            component.RemoveCameraInputLock();
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
                component.AddCameraInputLock();
            }

            world.Set(entity, component);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            system.Update(1);

            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(freeCameraData.Camera));
        }
    }
}
