using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.SDKComponents.TriggerArea.Systems;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Interaction.Utility;
using DCL.SDKComponents.TriggerArea.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.SDKComponents.TriggerArea.Tests
{
    public class TriggerAreaHandlerSystemShould : UnitySystemTestBase<TriggerAreaHandlerSystem>
    {
        private World globalWorld;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private IEntityCollidersSceneCache collidersSceneCache;
        private ISceneData sceneData;
        private readonly List<PBTriggerAreaResult> capturedResults = new ();
        private PBTriggerAreaResult capturedResult;
        private Entity entity;
        private CRDTEntity crdtEntity;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            collidersSceneCache = Substitute.For<IEntityCollidersSceneCache>();

            sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);

            system = new TriggerAreaHandlerSystem(
                world,
                globalWorld,
                ecsToCRDTWriter,
                collidersSceneCache,
                sceneData);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            crdtEntity = new CRDTEntity(999);
            world.Add(entity, crdtEntity);
            AddTransformToEntity(entity);
        }

        [Test]
        public void SetupSDKEntityTriggerAreaComponentCorrectly()
        {
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtSphere,
                CollisionMask = (uint)ColliderLayer.ClCustom3,
            };

            world.Add(entity, pbTriggerArea);

            system.Update(0);

            Assert.IsTrue(world.Has<TriggerAreaComponent>(entity));
            Assert.IsTrue(world.TryGet(entity, out SDKEntityTriggerAreaComponent triggerAreaComponent));
            Assert.AreEqual(SDKEntityTriggerAreaMeshType.SPHERE, triggerAreaComponent.MeshType);
            Assert.AreEqual(ColliderLayer.ClCustom3, triggerAreaComponent.LayerMask);
        }

        [Test]
        public void HandleComponentRemoveCorrectly()
        {
            // Create component and ensure it is set
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtBox,
                CollisionMask = (uint)ColliderLayer.ClCustom2,
            };
            world.Add(entity, pbTriggerArea);

            system.Update(0);
            Assert.IsTrue(world.Has<TriggerAreaComponent>(entity));

            // Remove PBTriggerArea and expect SDKEntityTriggerAreaComponent removed
            world.Remove<PBTriggerArea>(entity);
            system.Update(0);

            Assert.IsFalse(world.Has<TriggerAreaComponent>(entity));
        }

        [Test]
        public void PropagateOnTriggerEnter()
        {
            SetupAreaWithSDKLayer(ColliderLayer.ClCustom1);

            // Provide collider info for a foreign SDK entity
            var colliderGO = new GameObject("SDKEntityCollider_Enter");
            colliderGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            var box = colliderGO.AddComponent<BoxCollider>();

            // Create an SDK entity with a transform to be returned by the cache
            var sdkEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(sdkEntity);

            collidersSceneCache.TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>())
                                .Returns(ci =>
                                {
                                    ci[1] = new ColliderSceneEntityInfo(sdkEntity, new CRDTEntity(25), ColliderLayer.ClCustom1);
                                    return true;
                                });

            ClearCapturedResult();
            SetupCRDTWriterCapture();

            // Emulate an enter
            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.TryClear();
            comp.monoBehaviour!.OnTriggerEnter(box);

            system.Update(0);

            Assert.NotNull(capturedResult);
            Assert.AreEqual((uint)crdtEntity.Id, capturedResult.TriggeredEntity);
            Assert.AreEqual(TriggerAreaEventType.TaetEnter, capturedResult.EventType);
            Assert.AreEqual((uint)ColliderLayer.ClCustom1, capturedResult.Trigger.Layers);

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void DoesNotEmitStayMessagesOverMultipleUpdates()
        {
            // After a single OnTriggerEnter, repeated Update() calls must NOT produce
            // STAY messages. The SDK runtime is now responsible for synthesizing
            // per-tick OnStay callbacks; the Explorer only emits ENTER and EXIT.
            SetupAreaWithSDKLayer(ColliderLayer.ClCustom2);

            var colliderGO = new GameObject("SDKEntityCollider_NoStay");
            colliderGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            var box = colliderGO.AddComponent<BoxCollider>();

            var sdkEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(sdkEntity);

            collidersSceneCache.TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>())
                                .Returns(ci =>
                                {
                                    ci[1] = new ColliderSceneEntityInfo(sdkEntity, new CRDTEntity(26), ColliderLayer.ClCustom2);
                                    return true;
                                });

            ClearCapturedResult();
            SetupCRDTWriterCapture(firstCallOnly: false);

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.monoBehaviour!.OnTriggerEnter(box);

            for (int i = 0; i < 5; i++)
                system.Update(0);

            // Exactly one captured message (the ENTER); zero STAY messages.
            Assert.AreEqual(1, capturedResults.Count, "Expected exactly one CRDT message after enter + multiple updates.");
            Assert.AreEqual(TriggerAreaEventType.TaetEnter, capturedResults[0].EventType);
            Assert.IsFalse(capturedResults.Any(r => r.EventType == TriggerAreaEventType.TaetStay),
                "No STAY messages must be emitted on the wire.");

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void EmitsEnterAndExitOnly()
        {
            SetupAreaWithSDKLayer(ColliderLayer.ClCustom2);

            var colliderGO = new GameObject("SDKEntityCollider_EnterExitOnly");
            colliderGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            var box = colliderGO.AddComponent<BoxCollider>();

            var sdkEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(sdkEntity);

            collidersSceneCache.TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>())
                                .Returns(ci =>
                                {
                                    ci[1] = new ColliderSceneEntityInfo(sdkEntity, new CRDTEntity(28), ColliderLayer.ClCustom2);
                                    return true;
                                });

            ClearCapturedResult();
            SetupCRDTWriterCapture(firstCallOnly: false);

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);

            // Enter -> expect [ENTER]
            comp.monoBehaviour!.OnTriggerEnter(box);
            system.Update(0);
            Assert.AreEqual(1, capturedResults.Count);
            Assert.AreEqual(TriggerAreaEventType.TaetEnter, capturedResults[0].EventType);

            // Multiple updates while inside -> still just [ENTER]
            for (int i = 0; i < 3; i++) system.Update(0);
            Assert.AreEqual(1, capturedResults.Count, "No STAY messages should be emitted while inside.");

            // Exit -> expect [ENTER, EXIT]
            comp.monoBehaviour!.OnTriggerExit(box);
            system.Update(0);
            Assert.AreEqual(2, capturedResults.Count);
            Assert.AreEqual(TriggerAreaEventType.TaetExit, capturedResults[1].EventType);

            // Multiple updates after exit -> still just [ENTER, EXIT]
            for (int i = 0; i < 3; i++) system.Update(0);
            Assert.AreEqual(2, capturedResults.Count, "No additional messages should be emitted after exit.");

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void PropagateOnTriggerExit()
        {
            SetupAreaWithSDKLayer(ColliderLayer.ClCustom3);

            var colliderGO = new GameObject("SDKEntityCollider_Exit");
            colliderGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            var box = colliderGO.AddComponent<BoxCollider>();

            var sdkEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(sdkEntity);

            collidersSceneCache.TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>())
                                .Returns(ci =>
                                {
                                    ci[1] = new ColliderSceneEntityInfo(sdkEntity, new CRDTEntity(27), ColliderLayer.ClCustom3);
                                    return true;
                                });

            ClearCapturedResult();
            SetupCRDTWriterCapture();

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.monoBehaviour!.OnTriggerEnter(box);
            comp.monoBehaviour!.OnTriggerExit(box);

            system.Update(0);

            Assert.NotNull(capturedResult);
            Assert.AreEqual(TriggerAreaEventType.TaetExit, capturedResult.EventType);
            Assert.AreEqual((uint)ColliderLayer.ClCustom3, capturedResult.Trigger.Layers);

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void MainPlayerMask_FiresAgainstCharacterLayer()
        {
            // Arrange: trigger area with CL_MAIN_PLAYER only
            SetupAreaWithSDKLayer(ColliderLayer.ClMainPlayer);

            var colliderGO = new GameObject("MainPlayerCollider");
            colliderGO.layer = PhysicsLayers.CHARACTER_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            ClearCapturedResult();
            SetupCRDTWriterCapture();

            // No global avatar entity in the fixture, so FindAvatarUtils returns false and no
            // result is emitted — verifies the mask passed and the avatar lookup short-circuited.
            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.TryClear();
            comp.monoBehaviour!.OnTriggerEnter(box);

            system.Update(0);

            // collidersSceneCache must not be consulted for an avatar-layer collider —
            // proves the mask passed the avatar gate and reached TryGetAvatarEntity.
            collidersSceneCache.DidNotReceive().TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>());

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void MainPlayerMask_DoesNotFireAgainstOtherAvatarsLayer()
        {
            // Arrange: trigger area with CL_MAIN_PLAYER only
            SetupAreaWithSDKLayer(ColliderLayer.ClMainPlayer);

            var colliderGO = new GameObject("RemoteAvatarCollider");
            colliderGO.layer = PhysicsLayers.OTHER_AVATARS_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            ClearCapturedResult();
            SetupCRDTWriterCapture();

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.TryClear();
            comp.monoBehaviour!.OnTriggerEnter(box);

            system.Update(0);

            // CL_MAIN_PLAYER must not match remote avatars: the handler must return early
            // before TryGetAvatarEntity is called, so the scene-cache lookup is skipped.
            collidersSceneCache.DidNotReceive().TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>());
            Assert.IsNull(capturedResult);

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void MainPlayer_DoesNotFireOnPhysicsMask()
        {
            // CL_PHYSICS is not a player-qualifying bit — main player must not fire the trigger.
            SetupAreaWithSDKLayer(ColliderLayer.ClPhysics);

            var colliderGO = new GameObject("MainPlayerCollider_Physics");
            colliderGO.layer = PhysicsLayers.CHARACTER_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            ClearCapturedResult();
            SetupCRDTWriterCapture();

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.TryClear();
            comp.monoBehaviour!.OnTriggerEnter(box);

            system.Update(0);

            // The handler must return early in the avatar-overlap branch before the scene-cache
            // lookup, so TryGetEntity is not consulted and no result is dispatched.
            collidersSceneCache.DidNotReceive().TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>());
            Assert.IsNull(capturedResult);

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void RemoteAvatar_DoesNotFireOnPhysicsMask()
        {
            // Arrange: trigger area with CL_PHYSICS only. Remote avatars (OTHER_AVATARS_LAYER) have
            // no client-side physics body, so CL_PHYSICS must NOT match them.
            SetupAreaWithSDKLayer(ColliderLayer.ClPhysics);

            var colliderGO = new GameObject("RemoteAvatarCollider_Physics");
            colliderGO.layer = PhysicsLayers.OTHER_AVATARS_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            ClearCapturedResult();
            SetupCRDTWriterCapture();

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.TryClear();
            comp.monoBehaviour!.OnTriggerEnter(box);

            system.Update(0);

            // CL_PHYSICS must not match remote avatars: the handler must return early
            // before TryGetAvatarEntity is called, so the scene-cache lookup is skipped.
            collidersSceneCache.DidNotReceive().TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>());
            Assert.IsNull(capturedResult);

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void MainPlayerOnlyMask_SetsTargetOnlyMainPlayer()
        {
            // Arrange: a CL_MAIN_PLAYER-only mask should set targetOnlyMainPlayer = true.
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtBox,
                CollisionMask = (uint)ColliderLayer.ClMainPlayer,
            };

            world.Add(entity, pbTriggerArea);
            system.Update(0);

            // Run TryAssignArea: monoBehaviour.TargetTransform should be the main player transform.
            var pool = Substitute.For<DCL.Optimization.Pools.IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>>();
            var area = CreateAndAttachAreaMonoBehaviour(entity);
            pool.Get().Returns(area);

            var mainPlayerGO = new GameObject("MainPlayerProxy");
            try
            {
                var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
                var transformComp = world.Get<ECS.Unity.Transforms.Components.TransformComponent>(entity);
                comp.TryAssignArea(pool, mainPlayerGO.transform, transformComp);

                Assert.AreSame(mainPlayerGO.transform, area.TargetTransform,
                    "Expected TargetTransform to be assigned for a CL_MAIN_PLAYER-only mask.");
            }
            finally
            {
                Object.DestroyImmediate(mainPlayerGO);
            }
        }

        [Test]
        public void PlayerMask_DoesNotSetTargetOnlyMainPlayer()
        {
            // CL_PLAYER alone must NOT enable targetOnlyMainPlayer — would reject remote avatars.
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtBox,
                CollisionMask = (uint)ColliderLayer.ClPlayer,
            };

            world.Add(entity, pbTriggerArea);
            system.Update(0);

            var pool = Substitute.For<DCL.Optimization.Pools.IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>>();
            var area = CreateAndAttachAreaMonoBehaviour(entity);
            pool.Get().Returns(area);

            var mainPlayerGO = new GameObject("MainPlayerProxy_PlayerMask");
            try
            {
                var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
                var transformComp = world.Get<ECS.Unity.Transforms.Components.TransformComponent>(entity);
                comp.TryAssignArea(pool, mainPlayerGO.transform, transformComp);

                Assert.IsNull(area.TargetTransform,
                    "Expected TargetTransform to remain null for a CL_PLAYER mask.");
            }
            finally
            {
                Object.DestroyImmediate(mainPlayerGO);
            }
        }

        [Test]
        public void PlayerAndMainPlayerMask_DoesNotSetTargetOnlyMainPlayer()
        {
            // Arrange: CL_PLAYER | CL_MAIN_PLAYER must NOT route through targetOnlyMainPlayer —
            // it must accept remote avatars (via CL_PLAYER) as well.
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtBox,
                CollisionMask = (uint)(ColliderLayer.ClPlayer | ColliderLayer.ClMainPlayer),
            };

            world.Add(entity, pbTriggerArea);
            system.Update(0);

            var pool = Substitute.For<DCL.Optimization.Pools.IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>>();
            var area = CreateAndAttachAreaMonoBehaviour(entity);
            pool.Get().Returns(area);

            var mainPlayerGO = new GameObject("MainPlayerProxy_CombinedMask");
            try
            {
                var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
                var transformComp = world.Get<ECS.Unity.Transforms.Components.TransformComponent>(entity);
                comp.TryAssignArea(pool, mainPlayerGO.transform, transformComp);

                Assert.IsNull(area.TargetTransform,
                    "Expected TargetTransform to remain null for a CL_PLAYER|CL_MAIN_PLAYER mask.");
            }
            finally
            {
                Object.DestroyImmediate(mainPlayerGO);
            }
        }

        [Test]
        public void PlayerMask_DoesNotFireAgainstCharacterLayerAvatarLookupFails()
        {
            // Arrange: trigger area with CL_PLAYER -- should accept main + remote
            SetupAreaWithSDKLayer(ColliderLayer.ClPlayer);

            var colliderGO = new GameObject("MainAvatarColliderForPlayerMask");
            colliderGO.layer = PhysicsLayers.OTHER_AVATARS_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            ClearCapturedResult();
            SetupCRDTWriterCapture();

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.TryClear();
            comp.monoBehaviour!.OnTriggerEnter(box);

            system.Update(0);

            // CL_PLAYER must accept remote avatars: it reaches TryGetAvatarEntity and
            // returns without consulting the scene cache (avatar branch).
            collidersSceneCache.DidNotReceive().TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>());

            Object.DestroyImmediate(colliderGO);
        }

        private void SetupAreaWithSDKLayer(ColliderLayer layer)
        {
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtBox,
                CollisionMask = (uint)layer,
            };

            world.Add(entity, pbTriggerArea);
            system.Update(0);
        }

        private SDKEntityTriggerArea.SDKEntityTriggerArea CreateAndAttachAreaMonoBehaviour(Entity e)
        {
            var go = new GameObject("SDKEntityTriggerArea");
            var area = go.AddComponent<SDKEntityTriggerArea.SDKEntityTriggerArea>();
            go.AddComponent<BoxCollider>().isTrigger = true;
            area.BoxCollider = go.GetComponent<BoxCollider>();
            area.SphereCollider = go.AddComponent<SphereCollider>();
            area.SphereCollider.enabled = false;

            // Attach under entity's transform
            var transformComponent = world.Get<ECS.Unity.Transforms.Components.TransformComponent>(e);
            go.transform.SetParent(transformComponent.Transform, false);
            return area;
        }

        private void ClearCapturedResult()
        {
            capturedResult = null;
            capturedResults.Clear();
        }

        private void SetupCRDTWriterCapture(bool firstCallOnly = true)
        {
            ecsToCRDTWriter
               .AppendMessage<PBTriggerAreaResult, TriggerAreaHandlerSystem.ResultData>(
                   Arg.Any<System.Action<PBTriggerAreaResult, TriggerAreaHandlerSystem.ResultData>>(),
                   Arg.Any<CRDTEntity>(), Arg.Any<int>(),
                   Arg.Any<TriggerAreaHandlerSystem.ResultData>())
               .Returns(ci =>
               {
                   var prepare = ci.Arg<System.Action<PBTriggerAreaResult, TriggerAreaHandlerSystem.ResultData>>();
                   var res = new PBTriggerAreaResult();

                   if (firstCallOnly && capturedResult != null) return res;

                   var data = ci.ArgAt<TriggerAreaHandlerSystem.ResultData>(3);
                   prepare(res, data);
                   capturedResult = res;
                   capturedResults.Add(res);
                   return res;
               });
        }

    }
}


