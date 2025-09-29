using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.SDKComponents.TriggerArea.Systems;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TriggerArea.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.TriggerArea.Tests
{
    public class TriggerAreaHandlerSystemShould : UnitySystemTestBase<TriggerAreaHandlerSystem>
    {
        private World globalWorld;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private IEntityCollidersSceneCache collidersSceneCache;
        private ISceneStateProvider sceneStateProvider;
        private IComponentPool<PBTriggerAreaResult> triggerAreaResultPool;
        private IComponentPool<PBTriggerAreaResult.Types.Trigger> triggerAreaResultTriggerPool;
        private ISceneData sceneData;
        private PBTriggerAreaResult capturedResult;
        private Entity entity;
        private CRDTEntity crdtEntity;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            collidersSceneCache = Substitute.For<IEntityCollidersSceneCache>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(123u);

            triggerAreaResultPool = Substitute.For<IComponentPool<PBTriggerAreaResult>>();
            triggerAreaResultPool.Get().Returns(_ => new PBTriggerAreaResult());

            triggerAreaResultTriggerPool = Substitute.For<IComponentPool<PBTriggerAreaResult.Types.Trigger>>();
            triggerAreaResultTriggerPool.Get().Returns(_ => new PBTriggerAreaResult.Types.Trigger());

            sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);

            system = new TriggerAreaHandlerSystem(
                world,
                globalWorld,
                ecsToCRDTWriter,
                triggerAreaResultPool,
                triggerAreaResultTriggerPool,
                sceneStateProvider,
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
        public void PropagateOnTriggerStay()
        {
            SetupAreaWithSDKLayer(ColliderLayer.ClCustom2);

            var colliderGO = new GameObject("SDKEntityCollider_Stay");
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

            SetupCRDTWriterCapture(firstCallOnly: false);

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.monoBehaviour!.OnTriggerEnter(box);
            comp.monoBehaviour!.OnTriggerEnter(box); // remain inside

            system.Update(0);

            Assert.NotNull(capturedResult);
            Assert.AreEqual(TriggerAreaEventType.TaetStay, capturedResult.EventType);
            Assert.AreEqual((uint)ColliderLayer.ClCustom2, capturedResult.Trigger.Layers);

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

        private void ClearCapturedResult() => capturedResult = null;
        private void SetupCRDTWriterCapture(bool firstCallOnly = true)
        {
            ecsToCRDTWriter
               .AppendMessage<PBTriggerAreaResult, (PBTriggerAreaResult result, uint timestamp)>(Arg.Any<System.Action<PBTriggerAreaResult, (PBTriggerAreaResult result, uint timestamp)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(PBTriggerAreaResult, uint)>())
               .Returns(ci =>
               {
                   var prepare = ci.Arg<System.Action<PBTriggerAreaResult, (PBTriggerAreaResult, uint)>>();
                   var res = new PBTriggerAreaResult();

                   if (firstCallOnly && capturedResult != null) return res;

                   var data = ci.ArgAt<(PBTriggerAreaResult, uint)>(3);
                   prepare(res, data);
                   capturedResult = res;
                   return res;
               });
        }

    }
}


