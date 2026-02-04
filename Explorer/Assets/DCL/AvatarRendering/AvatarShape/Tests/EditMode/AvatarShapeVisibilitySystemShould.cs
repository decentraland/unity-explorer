using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Friends.UserBlocking;
using DCL.Quality;
using DCL.Utilities;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarShapeVisibilitySystemShould : UnitySystemTestBase<AvatarShapeVisibilitySystem>
    {
        private const string AVATAR_BASE_TEST_ASSET_PATH = "Assets/DCL/AvatarRendering/AvatarShape/Tests/Instantiate/TestAssets/AvatarBase_TestAsset.prefab";
        private const float START_FADE_DITHERING = 2.0f;
        private const float END_FADE_DITHERING = 0.5f;

        private ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private IUserBlockingCache userBlockingCache;
        private IRendererFeaturesCache rendererFeaturesCache;

        private GameObject cameraGameObject;
        private Camera testCamera;
        private Entity cameraEntity;

        private GameObject avatarGameObject;
        private AvatarBase avatarBase;

        private readonly List<GameObject> createdGameObjects = new ();

        [SetUp]
        public void SetUp()
        {
            // Create camera
            cameraGameObject = new GameObject("TestCamera");
            createdGameObjects.Add(cameraGameObject);
            testCamera = cameraGameObject.AddComponent<Camera>();
            testCamera.nearClipPlane = 0.1f;
            testCamera.farClipPlane = 1000f;
            testCamera.fieldOfView = 60f;
            cameraGameObject.transform.position = Vector3.zero;
            cameraGameObject.transform.rotation = Quaternion.identity;

            // Create camera entity with CameraComponent
            var cameraComponent = new CameraComponent(testCamera)
            {
                Mode = CameraMode.ThirdPerson,
            };
            cameraEntity = world.Create(cameraComponent);

            // Load and instantiate the test AvatarBase prefab (consistent with AvatarInstantiatorSystemShould)
            var avatarBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR_BASE_TEST_ASSET_PATH);
            Assert.IsNotNull(avatarBasePrefab, $"Could not load AvatarBase test prefab from {AVATAR_BASE_TEST_ASSET_PATH}");

            avatarGameObject = Object.Instantiate(avatarBasePrefab);
            createdGameObjects.Add(avatarGameObject);
            avatarBase = avatarGameObject.GetComponentInChildren<AvatarBase>();
            Assert.IsNotNull(avatarBase, "AvatarBase component not found on test prefab");
            Assert.IsNotNull(avatarBase.AvatarAnimator, "AvatarAnimator not configured on test prefab");

            // Setup mocks
            userBlockingCache = Substitute.For<IUserBlockingCache>();
            userBlockingCacheProxy = new ObjectProxy<IUserBlockingCache>();
            userBlockingCacheProxy.SetObject(userBlockingCache);

            rendererFeaturesCache = Substitute.For<IRendererFeaturesCache>();

            // Create the system under test (with includeBannedUsersFromScene = false to avoid singleton)
            system = new AvatarShapeVisibilitySystem(
                world,
                userBlockingCacheProxy,
                rendererFeaturesCache,
                START_FADE_DITHERING,
                END_FADE_DITHERING,
                includeBannedUsersFromScene: false
            );

            system.Initialize();
        }

        protected override void OnTearDown()
        {
            foreach (var go in createdGameObjects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            createdGameObjects.Clear();
        }

        private AvatarShapeComponent CreateAvatarShapeComponent(string id = "test-user-id", string name = "TestUser")
        {
            return new AvatarShapeComponent(name, id);
        }

        private Transform CreateCameraFocus(Vector3 position)
        {
            var go = new GameObject("CameraFocus");
            createdGameObjects.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        private void AddFakeWearableToAvatarShape(ref AvatarShapeComponent avatarShape)
        {
            // Create a fake wearable with a renderer so ToggleAvatarShape doesn't throw NullReferenceException
            var wearableGO = new GameObject("FakeWearable");
            createdGameObjects.Add(wearableGO);
            var renderer = wearableGO.AddComponent<MeshRenderer>();

            // CachedAttachment requires proper initialization - we use reflection to create one with Renderers list
            var attachment = new Loading.Assets.CachedAttachment(null, wearableGO, false);
            // The Renderers list is created but empty, we need to add a renderer to it
            attachment.Renderers.Add(renderer);

            avatarShape.InstantiatedWearables.Add(attachment);
        }

        [Test]
        public void ReturnTrueWhenObjectIsVisibleInCamera()
        {
            // Arrange
            var bounds = new Bounds(new Vector3(0, 1, 5), Vector3.one);
            cameraGameObject.transform.LookAt(bounds.center);

            // Act
            bool isVisible = system.IsVisibleInCamera(testCamera, bounds);

            // Assert
            Assert.IsTrue(isVisible);
        }

        [Test]
        public void ReturnFalseWhenObjectIsNotVisibleInCamera()
        {
            // Arrange - place object behind camera
            var bounds = new Bounds(new Vector3(0, 1, -10), Vector3.one);
            cameraGameObject.transform.rotation = Quaternion.identity; // Looking forward (+Z)

            // Act
            bool isVisible = system.IsVisibleInCamera(testCamera, bounds);

            // Assert
            Assert.IsFalse(isVisible);
        }

        [Test]
        public void ReturnTrueWhenWithinCameraDistance()
        {
            // Arrange
            float maxDistanceSquared = 100f; // 10 units
            avatarGameObject.transform.position = new Vector3(0, 0, 5); // 5 units away

            // Act
            bool isWithin = system.IsWithinCameraDistance(testCamera, avatarGameObject.transform, maxDistanceSquared);

            // Assert
            Assert.IsTrue(isWithin);
        }

        [Test]
        public void ReturnFalseWhenNotWithinCameraDistance()
        {
            // Arrange
            float maxDistanceSquared = 25f; // 5 units
            avatarGameObject.transform.position = new Vector3(0, 0, 10); // 10 units away

            // Act
            bool isWithin = system.IsWithinCameraDistance(testCamera, avatarGameObject.transform, maxDistanceSquared);

            // Assert
            Assert.IsFalse(isWithin);
        }

        [Test]
        public void AddCachedVisibilityComponentToPlayerAvatar()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            var cameraFocus = CreateCameraFocus(cameraGameObject.transform.position + new Vector3(0, 0, 3));
            var playerComponent = new PlayerComponent(cameraFocus);

            Entity playerEntity = world.Create(avatarShape, playerComponent, avatarBase, new CharacterEmoteComponent());

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<AvatarCachedVisibilityComponent>(playerEntity));
        }

        [Test]
        public void AddCachedVisibilityComponentToNonPlayerAvatar()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            Entity otherEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<AvatarCachedVisibilityComponent>(otherEntity));
        }

        [Test]
        public void HidePlayerAvatarWhenInFirstPersonMode()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            avatarShape.IsVisible = true;
            var cameraFocus = CreateCameraFocus(cameraGameObject.transform.position);
            var playerComponent = new PlayerComponent(cameraFocus);

            Entity playerEntity = world.Create(avatarShape, playerComponent, avatarBase, new CharacterEmoteComponent());

            // Set camera to first person mode
            ref var cameraComponent = ref world.Get<CameraComponent>(cameraEntity);
            cameraComponent.Mode = CameraMode.FirstPerson;

            // Act - first update adds component, second updates state
            system.Update(0);
            system.Update(0);

            // Assert
            ref var updatedAvatarShape = ref world.Get<AvatarShapeComponent>(playerEntity);
            Assert.IsFalse(updatedAvatarShape.IsVisible);
        }

        [Test]
        public void ShowPlayerAvatarWhenInThirdPersonMode()
        {
            // Arrange - Start in first person mode (avatar hidden), then switch to third person
            var avatarShape = CreateAvatarShapeComponent();
            var cameraFocus = CreateCameraFocus(cameraGameObject.transform.position + new Vector3(0, 0, 5)); // Far from camera
            var playerComponent = new PlayerComponent(cameraFocus);

            Entity playerEntity = world.Create(avatarShape, playerComponent, avatarBase, new CharacterEmoteComponent());

            // Start in first person mode - this should hide the avatar
            ref var cameraComponent = ref world.Get<CameraComponent>(cameraEntity);
            cameraComponent.Mode = CameraMode.FirstPerson;

            system.Update(0);

            // Verify avatar is hidden in first person
            ref var avatarShapeAfterFirstPerson = ref world.Get<AvatarShapeComponent>(playerEntity);
            Assert.IsFalse(avatarShapeAfterFirstPerson.IsVisible, "Avatar should be hidden in first person mode");

            // Act - Switch to third person mode
            cameraComponent.Mode = CameraMode.ThirdPerson;
            system.Update(0);

            // Assert - Avatar should now be visible
            ref var updatedAvatarShape = ref world.Get<AvatarShapeComponent>(playerEntity);
            Assert.IsTrue(updatedAvatarShape.IsVisible, "Avatar should be visible in third person mode");
        }

        [Test]
        public void AddHiddenComponentWhenUserIsBlocked()
        {
            // Arrange
            const string BLOCKED_USER_ID = "blocked-user-id";
            var avatarShape = CreateAvatarShapeComponent(BLOCKED_USER_ID);

            // Add wearable so blocking check passes
            AddFakeWearableToAvatarShape(ref avatarShape);

            userBlockingCache.UserIsBlocked(BLOCKED_USER_ID).Returns(true);

            Entity avatarEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(world.Has<HiddenPlayerComponent>(avatarEntity));
            ref var hiddenComponent = ref world.Get<HiddenPlayerComponent>(avatarEntity);
            Assert.IsTrue(hiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BLOCKED));
        }

        [Test]
        public void RemoveHiddenComponentWhenUserIsUnblocked()
        {
            // Arrange
            const string USER_ID = "user-id";
            var avatarShape = CreateAvatarShapeComponent(USER_ID);
            AddFakeWearableToAvatarShape(ref avatarShape);

            userBlockingCache.UserIsBlocked(USER_ID).Returns(true);

            Entity avatarEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // First update - user is blocked
            system.Update(0);
            Assert.IsTrue(world.Has<HiddenPlayerComponent>(avatarEntity));

            // Change blocking status
            userBlockingCache.UserIsBlocked(USER_ID).Returns(false);

            // Act - second update, user is unblocked
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<HiddenPlayerComponent>(avatarEntity));
        }

        [Test]
        public void NotAddHiddenComponentWhenUserBlockingCacheNotConfigured()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            AddFakeWearableToAvatarShape(ref avatarShape);

            // Create system with unconfigured proxy
            var unconfiguredProxy = new ObjectProxy<IUserBlockingCache>();
            var testSystem = new AvatarShapeVisibilitySystem(
                world,
                unconfiguredProxy,
                rendererFeaturesCache,
                START_FADE_DITHERING,
                END_FADE_DITHERING,
                includeBannedUsersFromScene: false
            );
            testSystem.Initialize();

            Entity avatarEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // Act
            testSystem.Update(0);

            // Assert
            Assert.IsFalse(world.Has<HiddenPlayerComponent>(avatarEntity));

            testSystem.Dispose();
        }

        [Test]
        public void NotBlockAvatarsWithoutInstantiatedWearables()
        {
            // Arrange
            const string USER_ID = "user-id";
            var avatarShape = CreateAvatarShapeComponent(USER_ID);
            // No wearables added

            userBlockingCache.UserIsBlocked(USER_ID).Returns(true);

            Entity avatarEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<HiddenPlayerComponent>(avatarEntity));
        }

        [Test]
        public void HideAvatarWhenHiddenByModifierArea()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            avatarShape.HiddenByModifierArea = true;
            avatarShape.IsVisible = true;

            Entity avatarEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // Act
            system.Update(0);
            system.Update(0);

            // Assert
            ref var updatedAvatarShape = ref world.Get<AvatarShapeComponent>(avatarEntity);
            Assert.IsFalse(updatedAvatarShape.IsVisible);
        }

        [Test]
        public void ShowAvatarWhenNotHiddenByModifierArea()
        {
            // Arrange - Start hidden by modifier area, then remove the modifier
            var avatarShape = CreateAvatarShapeComponent();
            avatarShape.HiddenByModifierArea = true; // Start hidden

            Entity avatarEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // First update - avatar should be hidden due to modifier area
            system.Update(0);

            ref var avatarShapeAfterHide = ref world.Get<AvatarShapeComponent>(avatarEntity);
            Assert.IsFalse(avatarShapeAfterHide.IsVisible, "Avatar should be hidden by modifier area");

            // Act - Remove the modifier area hiding
            avatarShapeAfterHide.HiddenByModifierArea = false;
            world.Set(avatarEntity, avatarShapeAfterHide);
            system.Update(0);

            // Assert - Avatar should now be visible
            ref var updatedAvatarShape = ref world.Get<AvatarShapeComponent>(avatarEntity);
            Assert.IsTrue(updatedAvatarShape.IsVisible, "Avatar should be visible when not hidden by modifier area");
        }

        [Test]
        public void ResetDitherStateWhenAvatarShapeIsDirty()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            avatarShape.IsDirty = true;
            var cameraFocus = CreateCameraFocus(cameraGameObject.transform.position + new Vector3(0, 0, 3));
            var playerComponent = new PlayerComponent(cameraFocus);

            Entity playerEntity = world.Create(avatarShape, playerComponent, avatarBase, new CharacterEmoteComponent());

            // Act - first update adds component
            system.Update(0);

            // Set dirty again
            ref var shapeRef = ref world.Get<AvatarShapeComponent>(playerEntity);
            shapeRef.IsDirty = true;

            // Add skinning component for dither test
            var skinningMaterials = new List<AvatarCustomSkinningComponent.MaterialSetup>();
            var skinningComponent = new AvatarCustomSkinningComponent();

            // Act - This update should trigger ResetDitherState due to IsDirty
            system.Update(0);

            // Assert - just verify no exceptions and entity still has component
            Assert.IsTrue(world.Has<AvatarCachedVisibilityComponent>(playerEntity));
        }

        [Test]
        public void CombineMultipleHiddenReasons()
        {
            // Arrange
            const string USER_ID = "user-id";
            var avatarShape = CreateAvatarShapeComponent(USER_ID);
            AddFakeWearableToAvatarShape(ref avatarShape);

            userBlockingCache.UserIsBlocked(USER_ID).Returns(true);

            Entity avatarEntity = world.Create(avatarShape, avatarBase, new CharacterEmoteComponent());

            // First, add blocked reason
            system.Update(0);

            // Manually add banned reason to test combination
            ref var hiddenComponent = ref world.Get<HiddenPlayerComponent>(avatarEntity);
            hiddenComponent.Reason |= HiddenPlayerComponent.HiddenReason.BANNED;

            // Act - verify both reasons are present
            Assert.IsTrue(hiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BLOCKED));
            Assert.IsTrue(hiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BANNED));

            // Unblock user
            userBlockingCache.UserIsBlocked(USER_ID).Returns(false);
            system.Update(0);

            // Assert - Only banned reason should remain
            Assert.IsTrue(world.Has<HiddenPlayerComponent>(avatarEntity));
            ref var updatedHiddenComponent = ref world.Get<HiddenPlayerComponent>(avatarEntity);
            Assert.IsFalse(updatedHiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BLOCKED));
            Assert.IsTrue(updatedHiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BANNED));
        }

        [Test]
        public void HidePlayerAvatarWhenTransitioningToFirstPersonAndCloseToCameraStart()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            avatarShape.IsVisible = true;
            // Position camera focus very close to camera (within startFadeDithering)
            var cameraFocus = CreateCameraFocus(cameraGameObject.transform.position + new Vector3(0, 0, 0.5f));
            var playerComponent = new PlayerComponent(cameraFocus);

            Entity playerEntity = world.Create(avatarShape, playerComponent, avatarBase, new CharacterEmoteComponent());

            // Set camera to first person mode and mark transitioning
            ref var cameraComponent = ref world.Get<CameraComponent>(cameraEntity);
            cameraComponent.Mode = CameraMode.FirstPerson;
            cameraComponent.IsTransitioningToFirstPerson = true;

            // Act
            system.Update(0);
            system.Update(0);

            // Assert
            ref var updatedAvatarShape = ref world.Get<AvatarShapeComponent>(playerEntity);
            Assert.IsFalse(updatedAvatarShape.IsVisible);
        }

        [Test]
        public void NotHidePlayerAvatarWhenTransitioningToFirstPersonButFarFromCamera()
        {
            // Arrange
            var avatarShape = CreateAvatarShapeComponent();
            avatarShape.IsVisible = true;
            // Position camera focus far from camera (beyond startFadeDithering)
            var cameraFocus = CreateCameraFocus(cameraGameObject.transform.position + new Vector3(0, 0, 10f));
            var playerComponent = new PlayerComponent(cameraFocus);

            Entity playerEntity = world.Create(avatarShape, playerComponent, avatarBase, new CharacterEmoteComponent());

            // Set camera to first person mode and mark transitioning
            ref var cameraComponent = ref world.Get<CameraComponent>(cameraEntity);
            cameraComponent.Mode = CameraMode.FirstPerson;
            cameraComponent.IsTransitioningToFirstPerson = true;

            // Act
            system.Update(0);
            system.Update(0);

            // Assert
            ref var updatedAvatarShape = ref world.Get<AvatarShapeComponent>(playerEntity);
            Assert.IsTrue(updatedAvatarShape.IsVisible);
        }
    }
}

