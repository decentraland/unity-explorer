using Arch.Core;
using Cinemachine;
using DCL.Audio;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
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
        private GameObject cameraFocus;
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
            cameraFocus = new GameObject("Camera Focus");
            cameraFocus.transform.SetParent(camera.transform);
            cinemachineObj = new GameObject("Cinemachine");

            CinemachineVirtualCamera firstPersonCamera = new GameObject("First Person Camera").AddComponent<CinemachineVirtualCamera>();
            firstPersonCamera.transform.SetParent(cinemachineObj.transform);
            firstPersonCamera.AddCinemachineComponent<CinemachineTransposer>();
            CinemachinePOV pov = firstPersonCamera.AddCinemachineComponent<CinemachinePOV>();
            firstPersonCameraData = Substitute.For<ICinemachineFirstPersonCameraData>();
            firstPersonCameraData.POV.Returns(pov);
            firstPersonCameraData.Camera.Returns(firstPersonCamera);

            CinemachineVirtualCamera thirdPersonCamera = new GameObject("Third Person Camera").AddComponent<CinemachineVirtualCamera>();
            thirdPersonCamera.transform.SetParent(cinemachineObj.transform);
            var thirdPersonFollow = thirdPersonCamera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
            thirdPersonCameraData = Substitute.For<ICinemachineThirdPersonCameraData>();
            thirdPersonCameraData.ThirdPersonFollow.Returns(thirdPersonFollow);
            thirdPersonCameraData.Camera.Returns(thirdPersonCamera);

            CinemachineVirtualCamera droneView = new GameObject("Third Person Camera Drone").AddComponent<CinemachineVirtualCamera>();
            droneView.transform.SetParent(cinemachineObj.transform);
            var droneViewFollow = droneView.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
            droneViewData = Substitute.For<ICinemachineThirdPersonCameraData>();
            droneViewData.ThirdPersonFollow.Returns(droneViewFollow);
            droneViewData.Camera.Returns(droneView);

            CinemachineVirtualCamera freeCamera = new GameObject("Free Camera").AddComponent<CinemachineVirtualCamera>();
            freeCamera.transform.SetParent(cinemachineObj.transform);
            CinemachinePOV freeCamPov = freeCamera.AddCinemachineComponent<CinemachinePOV>();
            freeCameraData = Substitute.For<ICinemachineFreeCameraData>();
            freeCameraData.POV.Returns(freeCamPov);
            freeCameraData.Camera.Returns(freeCamera);

            CinemachineBrain brain = cinemachineObj.AddComponent<CinemachineBrain>();
            cinemachinePreset = Substitute.For<ICinemachinePreset>();
            cinemachinePreset.Brain.Returns(brain);
            cinemachinePreset.FirstPersonCameraData.Returns(firstPersonCameraData);
            cinemachinePreset.FreeCameraData.Returns(freeCameraData);
            cinemachinePreset.ThirdPersonCameraData.Returns(thirdPersonCameraData);
            cinemachinePreset.DroneViewCameraData.Returns(droneViewData);
            cinemachinePreset.DefaultCameraMode.Returns(CameraMode.ThirdPerson);
            cinemachineCameraAudioSettings = Substitute.For<ICinemachineCameraAudioSettings>();
            system = new ControlCinemachineVirtualCameraSystem(world, cameraFocus.transform, cinemachineCameraAudioSettings);
            world.Create(new InputMapComponent(InputMapComponent.Kind.PLAYER | InputMapComponent.Kind.CAMERA | InputMapComponent.Kind.SHORTCUTS));

            inputMap = world.CacheInputMap();

            entity = world.Create(cinemachinePreset, new CameraComponent(camera), new CinemachineCameraState(), new CameraInput(), new CursorComponent());

            system.Initialize();
        }

        [TearDown]
        public void DisposeCameraSetup()
        {
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(cameraFocus);
            Object.DestroyImmediate(cinemachineObj);

            world.Dispose();
        }

        [Test]
        public void InitInputMapComponent()
        {
            Assert.That(inputMap.GetInputMapComponent(world).Active, Is.EqualTo(InputMapComponent.Kind.PLAYER | InputMapComponent.Kind.CAMERA | InputMapComponent.Kind.SHORTCUTS));
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
            world.Set(entity, new CursorComponent { CursorState = CursorState.Locked });
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

        [Test]
        public void SwitchFromSDKCameraToThirdPerson()
        {
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.Mode = CameraMode.SDKCamera;
            world.Set(entity, component);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.SDKCamera));
            Assert.That(world.Get<CinemachineCameraState>(entity).CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            component.Mode = CameraMode.ThirdPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));
            Assert.That(thirdPersonCameraData.Camera.m_Transitions.m_InheritPosition, Is.False,
                "When coming from SDKCamera, the ThirdPerson camera should not inherit position");
        }

        [Test]
        public void InheritPositionWhenSwitchingFromThirdPersonToFirstPerson()
        {
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.Mode = CameraMode.ThirdPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            component.Mode = CameraMode.FirstPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.FirstPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(firstPersonCameraData.Camera));
            Assert.That(firstPersonCameraData.Camera.m_Transitions.m_InheritPosition, Is.True,
                "When coming from ThirdPerson, the FirstPerson camera should inherit position");
        }

        [Test]
        public void InheritPositionWhenSwitchingFromThirdPersonToDroneView()
        {
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.Mode = CameraMode.ThirdPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            component.Mode = CameraMode.DroneView;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.DroneView));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(droneViewData.Camera));
            Assert.That(droneViewData.Camera.m_Transitions.m_InheritPosition, Is.True,
                "When coming from ThirdPerson, the DroneView camera should inherit position");
        }

        [Test]
        public void DoNotInheritPositionWhenSwitchingFromFirstPersonToThirdPerson()
        {
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.Mode = CameraMode.FirstPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.FirstPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(firstPersonCameraData.Camera));

            component.Mode = CameraMode.ThirdPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));
            Assert.That(thirdPersonCameraData.Camera.m_Transitions.m_InheritPosition, Is.False,
                "When coming from FirstPerson, the ThirdPerson camera should not inherit position");
            Assert.That(cameraFocus.transform.rotation.eulerAngles.y, Is.EqualTo(firstPersonCameraData.POV.m_HorizontalAxis.Value),
                "CameraFocus for ThirdPerson camera should copy the horizontal axis value from FirstPerson");
            Assert.That(cameraFocus.transform.rotation.eulerAngles.x, Is.EqualTo(firstPersonCameraData.POV.m_VerticalAxis.Value),
                "CameraFocus for ThirdPerson camera should copy the Vecrtical axis value from FirstPerson");
        }

        [Test]
        public void PreservePreviousModeWhenSwitchingCameras()
        {
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.Mode = CameraMode.FirstPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.FirstPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(firstPersonCameraData.Camera));

            component.Mode = CameraMode.ThirdPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(component.PreviousMode, Is.EqualTo(CameraMode.FirstPerson),
                "PreviousMode should be set to FirstPerson after switching from FirstPerson to ThirdPerson");
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));
        }

        [Test]
        public void SkipProcessingWhenInWorldCameraComponentExists()
        {
            CameraComponent component = world.Get<CameraComponent>(entity);
            component.Mode = CameraMode.ThirdPerson;
            world.Set(entity, component);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            CinemachineCameraState cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            world.Add(entity, new DCL.InWorldCamera.InWorldCameraComponent());
            world.Set(entity, new CameraInput { ZoomIn = true });

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(thirdPersonCameraData.Camera));

            world.Remove<DCL.InWorldCamera.InWorldCameraComponent>(entity);

            system.Update(1);

            component = world.Get<CameraComponent>(entity);
            cameraState = world.Get<CinemachineCameraState>(entity);

            Assert.That(component.Mode, Is.EqualTo(CameraMode.FirstPerson));
            Assert.That(cameraState.CurrentCamera, Is.EqualTo(firstPersonCameraData.Camera));
        }
    }
}
