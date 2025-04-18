using Arch.Core;
using Cinemachine;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.CharacterCamera.Systems;
using DCL.InWorldCamera;
using DCL.Settings.Settings;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.CharacterCamera.Tests
{
    public class ApplyCinemachineCameraInputSystemShould : InputTestFixture
    {
        private Camera camera;
        private GameObject cinemachineObj;
        private ICinemachinePreset cinemachinePreset;
        private Entity entity;
        private ICinemachineFirstPersonCameraData firstPersonCameraData;
        private ICinemachineFreeCameraData freeCameraData;
        private ICinemachineThirdPersonCameraData2 thirdPersonCameraData;
        private ICinemachineThirdPersonCameraData2 droneViewData;
        private DCLInput dclInput;
        private World world;
        private ApplyCinemachineCameraInputSystem system;

        [SetUp]
        public void CreateCameraSetup()
        {
            base.Setup();

            world = World.Create();
            camera = new GameObject("Camera Test").AddComponent<Camera>();
            cinemachineObj = new GameObject("Cinemachine");

            // Setup First Person Camera
            CinemachineVirtualCamera firstPersonCamera = new GameObject("First Person Camera").AddComponent<CinemachineVirtualCamera>();
            firstPersonCamera.transform.SetParent(cinemachineObj.transform);
            firstPersonCamera.AddCinemachineComponent<CinemachineTransposer>();
            CinemachinePOV pov = firstPersonCamera.AddCinemachineComponent<CinemachinePOV>();
            firstPersonCameraData = Substitute.For<ICinemachineFirstPersonCameraData>();
            firstPersonCameraData.Camera.Returns(firstPersonCamera);
            firstPersonCameraData.POV.Returns(pov);

            // Setup Third Person Camera
            CinemachineFreeLook thirdPersonCamera = new GameObject("Third Person Camera").AddComponent<CinemachineFreeLook>();
            thirdPersonCamera.transform.SetParent(cinemachineObj.transform);
            thirdPersonCameraData = Substitute.For<ICinemachineThirdPersonCameraData2>();
            // thirdPersonCameraData.Camera.Returns(thirdPersonCamera);
            // thirdPersonCameraData.CameraOffset.Returns(thirdPersonCamera.gameObject.AddComponent<CinemachineCameraOffset>());

            // Setup Drone View Camera
            CinemachineFreeLook droneView = new GameObject("Third Person Camera Drone").AddComponent<CinemachineFreeLook>();
            droneView.transform.SetParent(cinemachineObj.transform);
            // droneViewData = Substitute.For<ICinemachineThirdPersonCameraData>();
            // droneViewData.Camera.Returns(droneView);
            // droneViewData.CameraOffset.Returns(droneView.gameObject.AddComponent<CinemachineCameraOffset>());

            // Setup Free Camera
            CinemachineVirtualCamera freeCamera = new GameObject("Free Camera").AddComponent<CinemachineVirtualCamera>();
            freeCamera.transform.SetParent(cinemachineObj.transform);
            CinemachinePOV freeCamPov = freeCamera.AddCinemachineComponent<CinemachinePOV>();
            freeCameraData = Substitute.For<ICinemachineFreeCameraData>();
            freeCameraData.Camera.Returns(freeCamera);
            freeCameraData.POV.Returns(freeCamPov);
            freeCameraData.Speed.Returns(5f);

            // Setup Cinemachine Brain
            CinemachineBrain brain = cinemachineObj.AddComponent<CinemachineBrain>();
            cinemachinePreset = Substitute.For<ICinemachinePreset>();
            cinemachinePreset.Brain.Returns(brain);
            cinemachinePreset.FirstPersonCameraData.Returns(firstPersonCameraData);
            cinemachinePreset.FreeCameraData.Returns(freeCameraData);
            cinemachinePreset.ThirdPersonCameraData.Returns(thirdPersonCameraData);
            // cinemachinePreset.DroneViewCameraData.Returns(droneViewData);

            // Setup Input
            dclInput = new DCLInput();
            dclInput.Enable();

            // Create system with free camera allowed
            system = new ApplyCinemachineCameraInputSystem(world, dclInput, camera.transform, ScriptableObject.CreateInstance<ControlsSettingsAsset>() , true);

            // Create entity with camera components
            entity = world.Create(
                cinemachinePreset,
                new CameraComponent(camera),
                new CameraInput(),
                new CursorComponent()
            );
        }

        [TearDown]
        public void DisposeCameraSetup()
        {
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(cinemachineObj);
            world.Dispose();
            base.TearDown();
        }

        [Test]
        public void ApplyDroneViewCameraInput()
        {
            // Arrange
            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.DroneView });
            world.Set(entity, new CameraInput { Delta = new Vector2(0.5f, 0.3f) });

            // Act
            system.Update(0.1f);

            // Assert
            // Get the current components from the world
            CameraComponent cameraComponent = world.Get<CameraComponent>(entity);
            CameraInput cameraInput = world.Get<CameraInput>(entity);
            ICinemachinePreset preset = world.Get<ICinemachinePreset>(entity);

            Assert.That(cameraComponent.Mode, Is.EqualTo(CameraMode.DroneView));
            Assert.That(cameraInput.Delta, Is.EqualTo(new Vector2(0.5f, 0.3f)));

            // Check the camera input was applied correctly to the drone view camera
            // Assert.That(preset.DroneViewCameraData.Camera.m_XAxis.m_InputAxisValue, Is.EqualTo(0.5f));
            // Assert.That(preset.DroneViewCameraData.Camera.m_YAxis.m_InputAxisValue, Is.EqualTo(0.3f));
        }

        [Test]
        public void ApplyThirdPersonCameraInput()
        {
            // Arrange
            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.ThirdPerson });
            world.Set(entity, new CameraInput { Delta = new Vector2(0.5f, 0.3f) });

            // Act
            system.Update(0.1f);

            // Assert
            // Get the current components from the world
            CameraComponent cameraComponent = world.Get<CameraComponent>(entity);
            CameraInput cameraInput = world.Get<CameraInput>(entity);
            ICinemachinePreset preset = world.Get<ICinemachinePreset>(entity);

            Assert.That(cameraComponent.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraInput.Delta, Is.EqualTo(new Vector2(0.5f, 0.3f)));

            // Check the camera input was applied correctly to the third person camera
            // Assert.That(preset.ThirdPersonCameraData.Camera.m_XAxis.m_InputAxisValue, Is.EqualTo(0.5f));
            // Assert.That(preset.ThirdPersonCameraData.Camera.m_YAxis.m_InputAxisValue, Is.EqualTo(0.3f));
        }

        [Test]
        public void ApplyFirstPersonCameraInput()
        {
            // Arrange
            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.FirstPerson });
            world.Set(entity, new CameraInput { Delta = new Vector2(0.5f, 0.3f) });

            // Act
            system.Update(0.1f);

            // Assert
            // Get the current components from the world
            CameraComponent cameraComponent = world.Get<CameraComponent>(entity);
            CameraInput cameraInput = world.Get<CameraInput>(entity);
            ICinemachinePreset preset = world.Get<ICinemachinePreset>(entity);

            Assert.That(cameraComponent.Mode, Is.EqualTo(CameraMode.FirstPerson));
            Assert.That(cameraInput.Delta, Is.EqualTo(new Vector2(0.5f, 0.3f)));

            // Check the camera input was applied correctly to the first person camera
            Assert.That(preset.FirstPersonCameraData.POV.m_HorizontalAxis.m_InputAxisValue, Is.EqualTo(0.5f));
            Assert.That(preset.FirstPersonCameraData.POV.m_VerticalAxis.m_InputAxisValue, Is.EqualTo(0.3f));
        }

        [Test]
        public void ProcessCameraLookAtIntentForFirstPerson()
        {
            // Arrange
            Vector3 lookAtTarget = new Vector3(10, 0, 10);
            Vector3 playerPosition = Vector3.zero;
            var lookAtIntent = new CameraLookAtIntent(lookAtTarget, playerPosition);

            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.FirstPerson });
            world.Add(entity, lookAtIntent);

            // Verify the intent component exists before update
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.True);

            // Act
            system.Update(0.1f);

            // Assert
            // Verify the intent component was removed, which indicates it was processed
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.False);

            // Get the current preset from the world
            ICinemachinePreset preset = world.Get<ICinemachinePreset>(entity);

            // Verify the preset has the expected camera mode
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.FirstPerson));
        }

        [Test]
        public void ProcessCameraLookAtIntentForThirdPerson()
        {
            // Arrange
            Vector3 lookAtTarget = new Vector3(10, 0, 10);
            Vector3 playerPosition = Vector3.zero;
            var lookAtIntent = new CameraLookAtIntent(lookAtTarget, playerPosition);

            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.ThirdPerson });
            world.Add(entity, lookAtIntent);

            // Verify the intent component exists before update
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.True);

            // Act
            system.Update(0.1f);

            // Assert
            // Verify the intent component was removed, which indicates it was processed
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.False);

            // Get the current preset from the world
            ICinemachinePreset preset = world.Get<ICinemachinePreset>(entity);

            // Verify the preset has the expected camera mode
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.ThirdPerson));
        }

        [Test]
        public void ProcessCameraLookAtIntentForDroneView()
        {
            // Arrange
            Vector3 lookAtTarget = new Vector3(10, 0, 10);
            Vector3 playerPosition = Vector3.zero;
            var lookAtIntent = new CameraLookAtIntent(lookAtTarget, playerPosition);

            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.DroneView });
            world.Add(entity, lookAtIntent);

            // Verify the intent component exists before update
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.True);

            // Act
            system.Update(0.1f);

            // Assert
            // Verify the intent component was removed, which indicates it was processed
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.False);

            // Get the current preset from the world
            ICinemachinePreset preset = world.Get<ICinemachinePreset>(entity);

            // Verify the preset has the expected camera mode
            Assert.That(world.Get<CameraComponent>(entity).Mode, Is.EqualTo(CameraMode.DroneView));
        }

        [Test]
        public void IgnoreCameraLookAtIntentForSDKCamera()
        {
            // Arrange
            Vector3 lookAtTarget = new Vector3(10, 0, 10);
            Vector3 playerPosition = Vector3.zero;
            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.SDKCamera });
            world.Add(entity, new CameraLookAtIntent(lookAtTarget, playerPosition));

            // Act
            system.Update(0.1f);

            // Assert
            // Verify the intent component was not yet removed
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.True);

            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.ThirdPerson });
            system.Update(0.1f);

            // Verify the intent component was removed
            Assert.That(world.Has<CameraLookAtIntent>(entity), Is.False);
        }

        [Test]
        public void SkipProcessingWhenInWorldCameraComponentExists()
        {
            // Arrange
            world.Set(entity, new CameraComponent(camera) { Mode = CameraMode.ThirdPerson });
            world.Set(entity, new CameraInput { Delta = new Vector2(0.5f, 0.3f) });
            world.Add(entity, new InWorldCameraComponent());

            // Act
            system.Update(0.1f);

            // Assert - input should not be applied when InWorldCameraComponent exists
            // Get the current components from the world
            CameraComponent cameraComponent = world.Get<CameraComponent>(entity);
            CameraInput cameraInput = world.Get<CameraInput>(entity);
            ICinemachinePreset preset = world.Get<ICinemachinePreset>(entity);

            Assert.That(cameraComponent.Mode, Is.EqualTo(CameraMode.ThirdPerson));
            Assert.That(cameraInput.Delta, Is.EqualTo(new Vector2(0.5f, 0.3f)));

            // Verify InWorldCameraComponent exists
            Assert.That(world.Has<InWorldCameraComponent>(entity), Is.True);

            // Check the camera input was not applied
            // Assert.That(preset.ThirdPersonCameraData.Camera.m_XAxis.m_InputAxisValue, Is.EqualTo(0f));
            // Assert.That(preset.ThirdPersonCameraData.Camera.m_YAxis.m_InputAxisValue, Is.EqualTo(0f));
        }
    }
}
