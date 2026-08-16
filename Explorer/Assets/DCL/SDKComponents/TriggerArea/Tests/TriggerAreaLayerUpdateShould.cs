using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TriggerArea.Systems;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Interaction.Utility;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.TriggerArea.Tests
{
    public class TriggerAreaLayerUpdateShould : UnitySystemTestBase<TriggerAreaHandlerSystem>
    {
        private World globalWorld = null!;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private IEntityCollidersSceneCache collidersSceneCache = null!;
        private ISceneData sceneData = null!;
        private readonly List<PBTriggerAreaResult> capturedResults = new ();
        private Entity entity;
        private CRDTEntity crdtEntity;

        [SetUp]
        public void Setup()
        {
            // The fixture instance is shared across tests; the capture list must not leak
            // results from a previous test into the next one's assertions.
            capturedResults.Clear();

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
        public void UpdateMaskAndMeshTypeFromDirtyPBTriggerArea()
        {
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtBox,
                CollisionMask = (uint)ColliderLayer.ClPlayer,
            };

            world.Add(entity, pbTriggerArea);
            system.Update(0);

            world.Get<SDKEntityTriggerAreaComponent>(entity).IsDirty = false;

            pbTriggerArea.Mesh = TriggerAreaMeshType.TamtSphere;
            pbTriggerArea.CollisionMask = (uint)ColliderLayer.ClCustom5;
            pbTriggerArea.IsDirty = true;

            system.Update(0);

            var triggerAreaComponent = world.Get<SDKEntityTriggerAreaComponent>(entity);
            Assert.AreEqual(SDKEntityTriggerAreaMeshType.Sphere, triggerAreaComponent.MeshType);
            Assert.AreEqual(ColliderLayer.ClCustom5, triggerAreaComponent.LayerMask);
            Assert.IsTrue(triggerAreaComponent.IsDirty, "Component must be re-flagged dirty so TryAssignArea re-runs.");
            Assert.IsFalse(pbTriggerArea.IsDirty, "PBTriggerArea dirty flag must be consumed.");
        }

        [Test]
        public void EmitSyntheticExitWhenMaskChangeExcludesInsider()
        {
            SetupAreaWith(TriggerAreaMeshType.TamtBox, ColliderLayer.ClCustom1);

            var colliderGO = new GameObject("SDKEntityCollider_MaskExcludes");
            colliderGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            var sdkEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(sdkEntity);

            collidersSceneCache.TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>())
                               .Returns(ci =>
                                {
                                    ci[1] = new ColliderSceneEntityInfo(sdkEntity, new CRDTEntity(25), ColliderLayer.ClCustom1);
                                    return true;
                                });

            SetupCRDTWriterCapture();

            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.monoBehaviour!.OnTriggerEnter(box);
            system.Update(0);

            Assert.AreEqual(1, capturedResults.Count, "Sanity: the physical enter is reported.");
            Assert.AreEqual(TriggerAreaEventType.TaetEnter, capturedResults[0].EventType);
            capturedResults.Clear();

            // Flip the mask so the insider no longer matches; no physical exit occurs.
            SetPBTriggerAreaDirty(ColliderLayer.ClCustom2);
            system.Update(0);

            Assert.AreEqual(1, capturedResults.Count, "Expected exactly one synthetic EXIT after the mask excluded the insider.");
            Assert.AreEqual(TriggerAreaEventType.TaetExit, capturedResults[0].EventType);
            Assert.AreEqual((uint)ColliderLayer.ClCustom1, capturedResults[0].Trigger.Layers);

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void EmitSyntheticEnterWhenMaskChangeIncludesInsider()
        {
            SetupAreaWith(TriggerAreaMeshType.TamtBox, ColliderLayer.ClCustom2);

            var colliderGO = new GameObject("SDKEntityCollider_MaskIncludes");
            colliderGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            var sdkEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(sdkEntity);

            collidersSceneCache.TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>())
                               .Returns(ci =>
                                {
                                    ci[1] = new ColliderSceneEntityInfo(sdkEntity, new CRDTEntity(26), ColliderLayer.ClCustom1);
                                    return true;
                                });

            SetupCRDTWriterCapture();

            // Physical enter of a non-matching entity: registered inside, nothing reported.
            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(CreateAndAttachAreaMonoBehaviour(entity));
            var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
            comp.monoBehaviour!.OnTriggerEnter(box);
            system.Update(0);

            Assert.AreEqual(0, capturedResults.Count, "Sanity: a non-matching insider must not be reported.");

            // Widen the mask so the insider now matches; no physical re-enter occurs.
            SetPBTriggerAreaDirty(ColliderLayer.ClCustom1);
            system.Update(0);

            Assert.AreEqual(1, capturedResults.Count, "Expected exactly one synthetic ENTER after the mask included the insider.");
            Assert.AreEqual(TriggerAreaEventType.TaetEnter, capturedResults[0].EventType);
            Assert.AreEqual((uint)ColliderLayer.ClCustom1, capturedResults[0].Trigger.Layers);

            Object.DestroyImmediate(colliderGO);
        }

        [Test]
        public void RebindTargetTransformWhenMaskChangesFromMainPlayerOnly()
        {
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = TriggerAreaMeshType.TamtBox,
                CollisionMask = (uint)ColliderLayer.ClMainPlayer,
            };

            world.Add(entity, pbTriggerArea);
            system.Update(0);

            var pool = Substitute.For<IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>>();
            SDKEntityTriggerArea.SDKEntityTriggerArea area = CreateAndAttachAreaMonoBehaviour(entity);
            pool.Get().Returns(area);

            var mainPlayerGO = new GameObject("MainPlayerProxy_MaskUpdate");

            try
            {
                var transformComp = world.Get<TransformComponent>(entity);

                var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
                comp.TryAssignArea(pool, mainPlayerGO.transform, transformComp);
                Assert.AreSame(mainPlayerGO.transform, area.TargetTransform, "Sanity: CL_MAIN_PLAYER-only mask binds the main-player fast path.");

                world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(area);

                pbTriggerArea.CollisionMask = (uint)ColliderLayer.ClPlayer;
                pbTriggerArea.IsDirty = true;
                system.Update(0);

                var updatedComp = world.Get<SDKEntityTriggerAreaComponent>(entity);
                updatedComp.TryAssignArea(pool, mainPlayerGO.transform, transformComp);

                Assert.IsNull(area.TargetTransform, "A mask no longer CL_MAIN_PLAYER-only must clear the main-player fast path.");
            }
            finally
            {
                Object.DestroyImmediate(mainPlayerGO);
            }
        }

        [Test]
        public void EvictStaleInsiderWhenMaskCyclesThroughMainPlayerOnly()
        {
            SetupAreaWith(TriggerAreaMeshType.TamtBox, ColliderLayer.ClCustom1);

            var colliderGO = new GameObject("SDKEntityCollider_MaskCycle");
            colliderGO.layer = PhysicsLayers.SDK_CUSTOM_LAYER;
            BoxCollider box = colliderGO.AddComponent<BoxCollider>();

            var sdkEntity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(sdkEntity);

            collidersSceneCache.TryGetEntity(box, out Arg.Any<ColliderSceneEntityInfo>())
                               .Returns(ci =>
                                {
                                    ci[1] = new ColliderSceneEntityInfo(sdkEntity, new CRDTEntity(27), ColliderLayer.ClCustom1);
                                    return true;
                                });

            SetupCRDTWriterCapture();

            SDKEntityTriggerArea.SDKEntityTriggerArea area = CreateAndAttachAreaMonoBehaviour(entity);
            world.Get<SDKEntityTriggerAreaComponent>(entity).SetMonoBehaviour(area);
            area.OnTriggerEnter(box);
            system.Update(0);

            Assert.AreEqual(1, capturedResults.Count, "Sanity: the physical enter is reported.");
            capturedResults.Clear();

            // Narrow to the main-player-only fast path: the insider gets its synthetic EXIT...
            SetPBTriggerAreaDirty(ColliderLayer.ClMainPlayer);
            system.Update(0);

            Assert.AreEqual(1, capturedResults.Count, "Sanity: narrowing to CL_MAIN_PLAYER reports a synthetic EXIT.");
            Assert.AreEqual(TriggerAreaEventType.TaetExit, capturedResults[0].EventType);
            capturedResults.Clear();

            var pool = Substitute.For<IComponentPool<SDKEntityTriggerArea.SDKEntityTriggerArea>>();
            var mainPlayerGO = new GameObject("MainPlayerProxy_MaskCycle");

            try
            {
                // ... and the TryAssignArea re-run (production: SDKEntityTriggerAreaHandlerSystem
                // reacting to the component dirty flag) binds the filter, which must evict the
                // insider it can no longer track.
                var transformComp = world.Get<TransformComponent>(entity);
                var comp = world.Get<SDKEntityTriggerAreaComponent>(entity);
                comp.TryAssignArea(pool, mainPlayerGO.transform, transformComp);

                CollectionAssert.DoesNotContain(area.CurrentEntitiesInside, box,
                    "Binding the main-player filter must evict insiders whose exit callbacks it will swallow.");

                // The insider physically leaves while filtered: the exit callback is swallowed.
                area.OnTriggerExit(box);

                // Re-widening must not fabricate an ENTER for the long-gone insider.
                SetPBTriggerAreaDirty(ColliderLayer.ClCustom1);
                system.Update(0);

                Assert.AreEqual(0, capturedResults.Count, "No event may be fabricated for an entity that left while the main-player filter was bound.");
            }
            finally
            {
                Object.DestroyImmediate(mainPlayerGO);
                Object.DestroyImmediate(colliderGO);
            }
        }

        private void SetupAreaWith(TriggerAreaMeshType meshType, ColliderLayer mask)
        {
            var pbTriggerArea = new PBTriggerArea
            {
                Mesh = meshType,
                CollisionMask = (uint)mask,
            };

            world.Add(entity, pbTriggerArea);
            system.Update(0);
        }

        private void SetPBTriggerAreaDirty(ColliderLayer mask)
        {
            var pbTriggerArea = world.Get<PBTriggerArea>(entity);
            pbTriggerArea.CollisionMask = (uint)mask;
            pbTriggerArea.IsDirty = true;
        }

        private SDKEntityTriggerArea.SDKEntityTriggerArea CreateAndAttachAreaMonoBehaviour(Entity e)
        {
            var go = new GameObject("SDKEntityTriggerArea_LayerUpdate");
            var area = go.AddComponent<SDKEntityTriggerArea.SDKEntityTriggerArea>();
            go.AddComponent<BoxCollider>().isTrigger = true;
            area.BoxCollider = go.GetComponent<BoxCollider>();
            area.SphereCollider = go.AddComponent<SphereCollider>();
            area.SphereCollider.enabled = false;

            var transformComponent = world.Get<TransformComponent>(e);
            go.transform.SetParent(transformComponent.Transform, false);
            return area;
        }

        private void SetupCRDTWriterCapture()
        {
            ecsToCRDTWriter
               .AppendMessage(
                    Arg.Any<System.Action<PBTriggerAreaResult, TriggerAreaHandlerSystem.ResultData>>(),
                    Arg.Any<CRDTEntity>(), Arg.Any<int>(),
                    Arg.Any<TriggerAreaHandlerSystem.ResultData>())
               .Returns(ci =>
                {
                    var prepare = ci.Arg<System.Action<PBTriggerAreaResult, TriggerAreaHandlerSystem.ResultData>>();
                    var res = new PBTriggerAreaResult();
                    var data = ci.ArgAt<TriggerAreaHandlerSystem.ResultData>(3);
                    prepare(res, data);
                    capturedResults.Add(res);
                    return res;
                });
        }
    }
}
