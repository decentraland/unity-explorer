using Arch.Core;
using CrdtEcsBridge.Physics;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
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

            system = new PlayerOriginatedRaycastSystem(world, pointer, entityCollidersGlobalCache,
                playerInteractionEntity = new PlayerInteractionEntity(world.Create(new PlayerOriginRaycastResult()), world), 1000);

            var cameraGo = new GameObject("Camera GO");
            cameraGo.transform.ResetLocalTRS();

            // Z oriented
            cameraEntity = world.Create(new CameraComponent(camera = cameraGo.AddComponent<Camera>()), new CursorComponent());
            input.Set(mouse.position, new Vector2(camera.pixelWidth / 2f, camera.pixelHeight / 2f));
        }

        [TearDown]
        public void TearDown()
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

            entityCollidersGlobalCache.TryGetEntity(collider, out Arg.Any<GlobalColliderEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderEntityInfo();
                                           return true;
                                       });

            system.Update(0);

            ref PlayerOriginRaycastResult raycastResult = ref playerInteractionEntity.PlayerOriginRaycastResult;
            Assert.That(raycastResult.IsValidHit, Is.True);
            Assert.That(raycastResult.UnityRaycastHit.collider, Is.EqualTo(collider));
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

            entityCollidersGlobalCache.TryGetEntity(collider, out Arg.Any<GlobalColliderEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderEntityInfo();
                                           return true;
                                       });

            ref CursorComponent cc = ref world.Get<CursorComponent>(cameraEntity);
            cc.CursorState = CursorState.Locked;

            system.Update(0);

            ref PlayerOriginRaycastResult raycastResult = ref playerInteractionEntity.PlayerOriginRaycastResult;
            Assert.That(raycastResult.IsValidHit, Is.True);
            Assert.That(raycastResult.UnityRaycastHit.collider, Is.EqualTo(collider));
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

            entityCollidersGlobalCache.TryGetEntity(collider, out Arg.Any<GlobalColliderEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderEntityInfo();
                                           return true;
                                       });

            system.Update(0);

            ref PlayerOriginRaycastResult raycastResult = ref playerInteractionEntity.PlayerOriginRaycastResult;
            Assert.That(raycastResult.IsValidHit, Is.False);
            Assert.That(raycastResult.EntityInfo, Is.Null);
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

            entityCollidersGlobalCache.TryGetEntity(collider, out Arg.Any<GlobalColliderEntityInfo>())
                                      .Returns(x =>
                                       {
                                           x[1] = new GlobalColliderEntityInfo();
                                           return true;
                                       });

            system.Update(0);

            ref PlayerOriginRaycastResult raycastResult = ref playerInteractionEntity.PlayerOriginRaycastResult;
            Assert.That(raycastResult.IsValidHit, Is.False);
            Assert.That(raycastResult.EntityInfo, Is.Null);
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

            ref PlayerOriginRaycastResult raycastResult = ref playerInteractionEntity.PlayerOriginRaycastResult;
            Assert.That(raycastResult.IsValidHit, Is.False);
            Assert.That(raycastResult.EntityInfo, Is.Null);
        }
    }
}
