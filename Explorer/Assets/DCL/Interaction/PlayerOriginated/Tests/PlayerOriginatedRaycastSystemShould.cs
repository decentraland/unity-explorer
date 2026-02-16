using Arch.Core;
using CrdtEcsBridge.Physics;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Utility;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Interaction.PlayerOriginated.Tests
{
    public class PlayerOriginatedRaycastSystemShould : UnitySystemTestBase<PlayerOriginatedRaycastSystem>
    {
        private readonly InputTestFixture input = new ();
        private IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private PlayerInteractionEntity playerInteractionEntity;
        private Camera camera;
        private Entity cameraEntity;

        [SetUp]
        public void Setup()
        {
            input.Setup();

            Mouse mouse = InputSystem.AddDevice<Mouse>();
            var pointer = new InputAction("pointer", binding: "<Mouse>/position", type: InputActionType.PassThrough);
            pointer.Enable();

            entityCollidersGlobalCache = Substitute.For<IEntityCollidersGlobalCache>();

            Entity playerEntity = world.Create();
            world.Add(playerEntity, Vector3.zero);

            system = new PlayerOriginatedRaycastSystem(world, pointer, entityCollidersGlobalCache,
                playerInteractionEntity = new PlayerInteractionEntity(world.Create(new PlayerOriginRaycastResultForSceneEntities(), new PlayerOriginRaycastResultForGlobalEntities()), world, playerEntity), 1000);

            var cameraGo = new GameObject("Camera GO");
            cameraGo.transform.ResetLocalTRS();

            // Z oriented
            cameraEntity = world.Create(new CameraComponent(camera = cameraGo.AddComponent<Camera>()), new CursorComponent());
            input.Set(mouse.position, new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f));
        }

        protected override void OnTearDown()
        {
            input.TearDown();
            UnityObjectUtils.SafeDestroyGameObject(camera);
            camera = null;
        }

        [Test]
        public void FindValidEntityUnderPointer()
        {
            var colliderGo = new GameObject(nameof(PlayerOriginatedRaycastSystemShould));
            colliderGo.transform.ResetLocalTRS();
            colliderGo.transform.localPosition = new Vector3(0, 0, 10);
            colliderGo.layer = PhysicsLayers.DEFAULT_LAYER;
            BoxCollider collider = colliderGo.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            entityCollidersGlobalCache.TryGetSceneEntity(collider, out Arg.Any<GlobalColliderSceneEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderSceneEntityInfo();
                                           return true;
                                       });

            // Set the cursor position to match the mouse input position as the UpdateCursorInputSystem is not running here
            ref CursorComponent cursorComponent = ref world.Get<CursorComponent>(cameraEntity);
            cursorComponent.Position = new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f);

            system.Update(0);

            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            Assert.That(raycastResultForSceneEntities.IsValidHit, Is.True);
            Assert.That(raycastResultForSceneEntities.Collider, Is.EqualTo(collider));
        }

        [Test]
        public void FindValidEntityInCameraCenter()
        {
            var colliderGo = new GameObject(nameof(PlayerOriginatedRaycastSystemShould));
            colliderGo.transform.ResetLocalTRS();
            colliderGo.transform.localPosition = new Vector3(0, 0, 10);
            colliderGo.layer = PhysicsLayers.DEFAULT_LAYER;
            BoxCollider collider = colliderGo.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            entityCollidersGlobalCache.TryGetSceneEntity(collider, out Arg.Any<GlobalColliderSceneEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderSceneEntityInfo();
                                           return true;
                                       });

            ref CursorComponent cc = ref world.Get<CursorComponent>(cameraEntity);
            cc.CursorState = CursorState.Locked;

            system.Update(0);

            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            Assert.That(raycastResultForSceneEntities.IsValidHit, Is.True);
            Assert.That(raycastResultForSceneEntities.Collider, Is.EqualTo(collider));
        }

        [Test]
        public void IgnoreColliderWithWrongLayer()
        {
            var colliderGo = new GameObject(nameof(PlayerOriginatedRaycastSystemShould));
            colliderGo.transform.ResetLocalTRS();
            colliderGo.transform.localPosition = new Vector3(0, 0, 10);
            colliderGo.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            BoxCollider collider = colliderGo.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            entityCollidersGlobalCache.TryGetSceneEntity(collider, out Arg.Any<GlobalColliderSceneEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderSceneEntityInfo();
                                           return true;
                                       });

            system.Update(0);

            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            Assert.That(raycastResultForSceneEntities.IsValidHit, Is.False);
            Assert.That(raycastResultForSceneEntities.EntityInfo, Is.Null);
        }

        [Test]
        public void RespectMaxRaycastDistance()
        {
            var colliderGo = new GameObject(nameof(PlayerOriginatedRaycastSystemShould));
            colliderGo.transform.ResetLocalTRS();
            colliderGo.transform.localPosition = new Vector3(0, 0, 1500);
            colliderGo.layer = PhysicsLayers.DEFAULT_LAYER;
            BoxCollider collider = colliderGo.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            entityCollidersGlobalCache.TryGetSceneEntity(collider, out Arg.Any<GlobalColliderSceneEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderSceneEntityInfo();
                                           return true;
                                       });

            system.Update(0);

            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            Assert.That(raycastResultForSceneEntities.IsValidHit, Is.False);
            Assert.That(raycastResultForSceneEntities.Collider, Is.Null);
        }

        [Test]
        public void IgnoreNotRegisteredCollider()
        {
            var colliderGo = new GameObject(nameof(PlayerOriginatedRaycastSystemShould));
            colliderGo.transform.ResetLocalTRS();
            colliderGo.transform.localPosition = new Vector3(0, 0, 10);
            colliderGo.layer = PhysicsLayers.DEFAULT_LAYER;
            BoxCollider collider = colliderGo.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            system.Update(0);

            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            Assert.That(raycastResultForSceneEntities.IsValidHit, Is.False);
            Assert.That(raycastResultForSceneEntities.EntityInfo, Is.Null);
        }

        [Test]
        public void DoNotRaycastWhenPanningCamera()
        {
            var colliderGo = new GameObject(nameof(PlayerOriginatedRaycastSystemShould));
            colliderGo.transform.ResetLocalTRS();
            colliderGo.transform.localPosition = new Vector3(0, 0, 10);
            colliderGo.layer = PhysicsLayers.DEFAULT_LAYER;
            BoxCollider collider = colliderGo.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            entityCollidersGlobalCache.TryGetSceneEntity(collider, out Arg.Any<GlobalColliderSceneEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderSceneEntityInfo();
                                           return true;
                                       });

            ref CursorComponent cc = ref world.Get<CursorComponent>(cameraEntity);
            cc.CursorState = CursorState.Panning;

            system.Update(0);

            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            Assert.That(raycastResultForSceneEntities.IsValidHit, Is.False);
            Assert.That(raycastResultForSceneEntities.Collider, Is.Null);
        }

        [Test]
        public void AddHoveredComponentToGlobalEntityWhenRaycastHits()
        {
            // Arrange
            var colliderGo = new GameObject(nameof(PlayerOriginatedRaycastSystemShould));
            colliderGo.transform.ResetLocalTRS();
            colliderGo.transform.localPosition = new Vector3(0, 0, 10);
            colliderGo.layer = PhysicsLayers.DEFAULT_LAYER;
            BoxCollider collider = colliderGo.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            Entity globalEntity = world.Create();
            var globalEntityInfo = new GlobalColliderGlobalEntityInfo (globalEntity);

            entityCollidersGlobalCache.TryGetSceneEntity(collider, out Arg.Any<GlobalColliderSceneEntityInfo>()).Returns(x =>
                                       {
                                           x[1] = new GlobalColliderSceneEntityInfo(); // Out param
                                           return true;
                                       });

            entityCollidersGlobalCache.TryGetGlobalEntity(collider, out Arg.Any<GlobalColliderGlobalEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = globalEntityInfo; // Out param
                                           return true;
                                       });

            ref CursorComponent cursorComponent = ref world.Get<CursorComponent>(cameraEntity);
            cursorComponent.Position = new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<HoveredComponent>(globalEntity), Is.True);
        }

        [Test]
        public void RemoveHoveredComponentWhenRaycastMovesToDifferentEntity()
        {
            // Arrange
            //  Set cursor in the middle of the screen
            ref CursorComponent cursorComponent = ref world.Get<CursorComponent>(cameraEntity);
            cursorComponent.Position = new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f);

            // Create entity 1 with collider and trigger a hit
            BoxCollider collider1 = CreateObjectWithCollider(nameof(PlayerOriginatedRaycastSystemShould) + "1", new Vector3(0, 0, 10));
            Entity globalEntity1 = world.Create();
            var globalEntityInfo1 = new GlobalColliderGlobalEntityInfo(globalEntity1);
            entityCollidersGlobalCache.TryGetGlobalEntity(collider1, out Arg.Any<GlobalColliderGlobalEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = globalEntityInfo1;  // Out param
                                           return true;
                                       });
            system.Update(0);
            Assert.That(world.Has<HoveredComponent>(globalEntity1), Is.True);

            // Disable entity 1 collider, create entity 2 with collider and trigger a hit
            collider1.enabled = false;
            BoxCollider collider2 = CreateObjectWithCollider(nameof(PlayerOriginatedRaycastSystemShould) + "2", new Vector3(0, 0, 5));
            Entity globalEntity2 = world.Create();
            var globalEntityInfo2 = new GlobalColliderGlobalEntityInfo(globalEntity2);
            entityCollidersGlobalCache.TryGetGlobalEntity(collider2, out Arg.Any<GlobalColliderGlobalEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = globalEntityInfo2;  // Out param
                                           return true;
                                       });

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<HoveredComponent>(globalEntity1), Is.False, "Hover should be removed from entity 1");
            Assert.That(world.Has<HoveredComponent>(globalEntity2), Is.True, "Hover should be added to entity 2");

        }

        [Test]
        public void RemoveHoveredComponentWhenRaycastMisses()
        {
            // Arrange
            ref CursorComponent cursorComponent = ref world.Get<CursorComponent>(cameraEntity);
            cursorComponent.Position = new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f);

            // Force a raycast hit
            BoxCollider collider = CreateObjectWithCollider(nameof(PlayerOriginatedRaycastSystemShould), new Vector3(0, 0, 10));
            Entity globalEntity = world.Create();
            var globalEntityInfo = new GlobalColliderGlobalEntityInfo(globalEntity);
            entityCollidersGlobalCache.TryGetGlobalEntity(collider, out Arg.Any<GlobalColliderGlobalEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = globalEntityInfo;  // Out param
                                           return true;
                                       });
            system.Update(0);
            Assert.That(world.Has<HoveredComponent>(globalEntity), Is.True);

            // Disable collider to make raycast miss
            collider.enabled = false;
            entityCollidersGlobalCache.TryGetGlobalEntity(collider, out Arg.Any<GlobalColliderGlobalEntityInfo>()).Returns(false);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<HoveredComponent>(globalEntity), Is.False, "Hover should be removed when raycast misses");
        }

        private BoxCollider CreateObjectWithCollider(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.ResetLocalTRS();
            go.transform.localPosition = pos;
            go.layer = PhysicsLayers.DEFAULT_LAYER;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            return collider;
        }
    }
}
