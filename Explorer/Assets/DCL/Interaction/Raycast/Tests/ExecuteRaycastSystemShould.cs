using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Raycast.Systems;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;
using Component = UnityEngine.Component;
using RaycastHit = DCL.ECSComponents.RaycastHit;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.Interaction.Raycast.Tests
{
    public class ExecuteRaycastSystemShould : UnitySystemTestBase<ExecuteRaycastSystem>
    {
        private readonly List<Component> instantiatedTemp = new ();
        private IEntityCollidersSceneCache entityCollidersSceneCache;
        private IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private IReleasablePerformanceBudget budget;
        private ISceneStateProvider sceneStateProvider;
        private Entity sceneRoot;
        private Dictionary<CRDTEntity, Entity> entitiesMap;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PBRaycast pbRaycast;
        private PartitionComponent partitionComponent;
        private Entity raycastEntity;
        private PBRaycastResult raycastResult;
        private UnityEngine.Vector3 testScenePos;
        private ISceneData sceneData;

        [SetUp]
        public void SetUp()
        {
            sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            testScenePos = new UnityEngine.Vector3(10f, 0f, 15f);
            sceneData.Geometry.Returns(new ParcelMathHelper.SceneGeometry(testScenePos, new ParcelMathHelper.SceneCircumscribedPlanes(), 0.0f));

            budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);

            IComponentPool<PBRaycastResult> pbRaycastResultPool = Substitute.For<IComponentPool<PBRaycastResult>>();
            pbRaycastResultPool.Get().Returns(_ => new PBRaycastResult().Reset());

            IComponentPool<RaycastHit> raycastHitPool = Substitute.For<IComponentPool<RaycastHit>>();
            raycastHitPool.Get().Returns(_ => new RaycastHit().Reset());

            system = new ExecuteRaycastSystem(
                world,
                sceneData,
                budget,
                4,
                raycastHitPool,
                pbRaycastResultPool,
                entityCollidersSceneCache = Substitute.For<IEntityCollidersSceneCache>(),
                entityCollidersGlobalCache = Substitute.For<IEntityCollidersGlobalCache>(),
                entitiesMap = new Dictionary<CRDTEntity, Entity>(),
                ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>(),
                sceneStateProvider = Substitute.For<ISceneStateProvider>()
            );

            system.Initialize();

            ecsToCRDTWriter.When(x => x.PutMessage(Arg.Any<PBRaycastResult>(), Arg.Any<CRDTEntity>()))
                           .Do(c => raycastResult = c.Arg<PBRaycastResult>());

            sceneStateProvider.TickNumber.Returns(5u);
            sceneStateProvider.IsCurrent.Returns(true);


            pbRaycast = new PBRaycast
            {
                GlobalDirection = new Vector3 { X = 0, Y = 0, Z = 1 },
                MaxDistance = 1000,
                Timestamp = 5000u,
            };

            raycastEntity = world.Create(pbRaycast, new CRDTEntity(25), partitionComponent = new PartitionComponent(),
                new RaycastComponent());

            AddTransformToEntity(raycastEntity);
        }

        [TearDown]
        public void DestroyGarbage()
        {
            foreach (Component component in instantiatedTemp)
                UnityObjectUtils.SafeDestroyGameObject(component);

            instantiatedTemp.Clear();
        }

        [Test]
        public void FindClosestHit()
        {
            // Create two colliders, the first is not qualified
            CreateColliders(ColliderLayer.ClCustom5, ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);

            // 1 = second element

            ecsToCRDTWriter.Received(1)
                           .PutMessage(Arg.Any<PBRaycastResult>(), new CRDTEntity(25));

            UnityEngine.Vector3 expectedSceneRelativeRayOrigin = UnityEngine.Vector3.zero - testScenePos;

            Assert.That(raycastResult.TickNumber, Is.EqualTo(5u));
            Assert.That((UnityEngine.Vector3)raycastResult.Direction, Is.EqualTo(UnityEngine.Vector3.forward));
            Assert.That((UnityEngine.Vector3)raycastResult.GlobalOrigin, Is.EqualTo(expectedSceneRelativeRayOrigin));
            Assert.That(raycastResult.Timestamp, Is.EqualTo(5000u));
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(1));

            AssertHit(0, 1);
        }

        [Test]
        public void FindAllQualifiedHits()
        {
            // 8 ignore
            CreateColliders(ColliderLayer.ClCustom2, ColliderLayer.ClCustom4, ColliderLayer.ClCustom7, ColliderLayer.ClCustom8);

            pbRaycast.QueryType = RaycastQueryType.RqtQueryAll;
            pbRaycast.CollisionMask = (uint)(ColliderLayer.ClCustom2 | ColliderLayer.ClCustom4 | ColliderLayer.ClCustom7);

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(Arg.Any<PBRaycastResult>(), new CRDTEntity(25));

            UnityEngine.Vector3 expectedSceneRelativeRayOrigin = UnityEngine.Vector3.zero - testScenePos;

            Assert.That(raycastResult.TickNumber, Is.EqualTo(5u));
            Assert.That((UnityEngine.Vector3)raycastResult.Direction, Is.EqualTo(UnityEngine.Vector3.forward));
            Assert.That((UnityEngine.Vector3)raycastResult.GlobalOrigin, Is.EqualTo(expectedSceneRelativeRayOrigin));
            Assert.That(raycastResult.Timestamp, Is.EqualTo(5000u));
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(3));

            AssertHit(0, 0);
            AssertHit(1, 1);
            AssertHit(2, 2);
        }

        [Test]
        public void FindNoHits()
        {
            CreateColliders(ColliderLayer.ClCustom5, ColliderLayer.ClCustom8);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPointer;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);

            // 1 = second element

            ecsToCRDTWriter.Received(1)
                           .PutMessage(Arg.Any<PBRaycastResult>(), new CRDTEntity(25));

            UnityEngine.Vector3 expectedSceneRelativeRayOrigin = UnityEngine.Vector3.zero - testScenePos;

            Assert.That(raycastResult.TickNumber, Is.EqualTo(5u));
            Assert.That((UnityEngine.Vector3)raycastResult.Direction, Is.EqualTo(UnityEngine.Vector3.forward));
            Assert.That((UnityEngine.Vector3)raycastResult.GlobalOrigin, Is.EqualTo(expectedSceneRelativeRayOrigin));
            Assert.That(raycastResult.Timestamp, Is.EqualTo(5000u));
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(0));
        }

        [Test]
        public void IgnoreEntityOutsideBucketThreshold([Values(5, 7, 10)] byte value)
        {
            partitionComponent.Bucket = value;

            CreateColliders(ColliderLayer.ClCustom5, ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.False);

            // 1 = second element

            ecsToCRDTWriter.DidNotReceive()
                           .PutMessage(Arg.Any<PBRaycastResult>(), new CRDTEntity(25));
        }

        [Test]
        public void DoNothingIfOutOfBudget()
        {
            budget.TrySpendBudget().Returns(false);

            CreateColliders(ColliderLayer.ClCustom5, ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.False);

            // 1 = second element

            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<PBRaycastResult>(), Arg.Any<CRDTEntity>());
        }

        [Test]
        public void DoNothingIfSceneIsNotFinishedLoading()
        {
            sceneData.SceneLoadingConcluded.Returns(false);

            CreateColliders(ColliderLayer.ClPhysics);
            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.False);
            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<PBRaycastResult>(), Arg.Any<CRDTEntity>());
        }

        [Test]
        public void KeepExecutionIfContinuous()
        {
            pbRaycast.Continuous = true;
            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClCustom3;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.False);
        }

        [Test]
        public void FindCrossSceneHitWhenNotInSceneCache_PortableExperience()
        {
            // Set up scene as Portable Experience
            sceneData.IsPortableExperience().Returns(true);

            // Create a collider that is NOT in the scene cache but IS in the global cache
            CreateCrossSceneColliders(ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(Arg.Any<PBRaycastResult>(), new CRDTEntity(25));

            Assert.That(raycastResult.Hits.Count, Is.EqualTo(1));
            // Cross-scene entities should have EntityId 0 because foundEntity is null (we don't inform of entity IDs from other scenes)
            Assert.That(raycastResult.Hits[0].EntityId, Is.EqualTo(0u));
        }

        [Test]
        public void RegularSceneDoesNotCheckGlobalCache()
        {
            // Ensure scene is NOT a Portable Experience (default)
            sceneData.IsPortableExperience().Returns(false);

            // Create a collider that is NOT in the scene cache but IS in the global cache
            CreateCrossSceneColliders(ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(Arg.Any<PBRaycastResult>(), new CRDTEntity(25));

            // Regular scenes should NOT find cross-scene entities (only check scene cache)
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(0));
        }

        [Test]
        public void FilterCrossSceneHitsByCollisionMask_PortableExperience()
        {
            // Set up scene as Portable Experience
            sceneData.IsPortableExperience().Returns(true);

            // Create cross-scene colliders with different layers
            CreateCrossSceneColliders(ColliderLayer.ClCustom5, ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics; // Only Physics should qualify

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(1));
            // Cross-scene entities should have EntityId 0 because foundEntity is null (we don't inform of entity IDs from other scenes)
            Assert.That(raycastResult.Hits[0].EntityId, Is.EqualTo(0u));
        }

        [Test]
        public void FindAllQualifiedCrossSceneHits_PortableExperience()
        {
            // Set up scene as Portable Experience
            sceneData.IsPortableExperience().Returns(true);

            // Create multiple cross-scene colliders
            CreateCrossSceneColliders(ColliderLayer.ClCustom2, ColliderLayer.ClCustom4, ColliderLayer.ClCustom7, ColliderLayer.ClCustom8);

            pbRaycast.QueryType = RaycastQueryType.RqtQueryAll;
            pbRaycast.CollisionMask = (uint)(ColliderLayer.ClCustom2 | ColliderLayer.ClCustom4 | ColliderLayer.ClCustom7);

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(3)); // Should find 3 qualified hits

            // Cross-scene entities should all have EntityId 0 because foundEntity is null (we don't inform of entity IDs from other scenes)
            foreach (var hit in raycastResult.Hits)
            {
                Assert.That(hit.EntityId, Is.EqualTo(0u));
            }
        }

        [Test]
        public void FindCrossSceneHits_PortableExperience()
        {
            // Set up scene as Portable Experience
            sceneData.IsPortableExperience().Returns(true);

            // Create colliders in global cache only (Portable Experiences only check global cache)
            CreateCrossSceneColliders(ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtQueryAll;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            // Portable Experiences only check global cache (scene-agnostic), not their own scene cache
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(1)); // Should find global cache hit

            // Cross-scene entities should have EntityId 0 because foundEntity is null (we don't inform of entity IDs from other scenes)
            Assert.That(raycastResult.Hits[0].EntityId, Is.EqualTo(0u));
        }

        [Test]
        public void RegularSceneOnlyFindsSceneCacheHits()
        {
            // Ensure scene is NOT a Portable Experience
            sceneData.IsPortableExperience().Returns(false);

            // Create one collider in scene cache
            CreateColliders(ColliderLayer.ClPhysics);

            // Create one collider in global cache only (should be ignored for regular scenes)
            CreateCrossSceneColliders(ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtQueryAll;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            // Regular scenes should only find scene cache hits, not global cache hits
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(1));
            Assert.That(raycastResult.Hits[0].EntityId, Is.EqualTo(0u)); // Scene cache entity
        }

        [Test]
        public void PortableExperienceDoesNotCheckSceneCache()
        {
            // Set up scene as Portable Experience
            sceneData.IsPortableExperience().Returns(true);

            // Create a collider in scene cache only (should NOT be found by Portable Experiences)
            CreateColliders(ColliderLayer.ClPhysics);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics;

            system.Update(0);

            // Portable Experiences only check global cache, not their own scene cache
            // So scene cache entities should NOT be found
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(0));
        }

        [Test]
        public void IgnoreCrossSceneColliderNotInCollisionMask_PortableExperience()
        {
            // Set up scene as Portable Experience
            sceneData.IsPortableExperience().Returns(true);

            // Create cross-scene collider with wrong layer
            CreateCrossSceneColliders(ColliderLayer.ClCustom5);

            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClPhysics; // Different mask

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.True);
            Assert.That(raycastResult.Hits.Count, Is.EqualTo(0)); // Should not find any hits
        }

        private void AssertHit(int index, int colliderIndex)
        {
            var sorted = raycastResult.Hits.OrderBy(x => x.Length).ToList();

            RaycastHit hit = sorted[index];
            Assert.That(hit.MeshName, Is.EqualTo(nameof(ExecuteRaycastSystemShould) + colliderIndex));
            Assert.That(hit.EntityId, Is.EqualTo((uint)colliderIndex));

            UnityEngine.Vector3 globalHitPoint = new UnityEngine.Vector3(0, 0, ((colliderIndex + 1) * 5f) - 0.5f);
            UnityEngine.Vector3 expectedSceneRelativeHitPosition = globalHitPoint - testScenePos;
            Assert.That((UnityEngine.Vector3)hit.Position, Is.EqualTo(expectedSceneRelativeHitPosition));

            UnityEngine.Vector3 rayEntityGlobalPos = UnityEngine.Vector3.zero;
            UnityEngine.Vector3 expectedSceneRelativeRayOrigin = rayEntityGlobalPos - testScenePos;
            Assert.That((UnityEngine.Vector3)hit.GlobalOrigin, Is.EqualTo(expectedSceneRelativeRayOrigin));
        }

        private void CreateColliders(params ColliderLayer[] sdkLayerMask)
        {
            for (var i = 0; i < sdkLayerMask.Length; i++)
            {
                ColliderLayer mask = sdkLayerMask[i];
                var entity = new CRDTEntity(i);
                CreateSingleCollider(mask, i, entity, true);
            }
        }

        private void CreateCrossSceneColliders(params ColliderLayer[] sdkLayerMask)
        {
            for (var i = 0; i < sdkLayerMask.Length; i++)
            {
                ColliderLayer mask = sdkLayerMask[i];
                // Use entity IDs starting at 100 for cross-scene entities to distinguish them
                var entity = new CRDTEntity(100 + i);
                CreateSingleCollider(mask, i, entity, false);
            }
        }

        private BoxCollider CreateSingleCollider(ColliderLayer sdkLayerMask, int index, CRDTEntity? entity = null, bool inSceneCache = true)
        {
            var entityId = entity ?? new CRDTEntity(index);
            Transform colliderTransform = new GameObject(nameof(ExecuteRaycastSystemShould) + index).transform;
            colliderTransform.position = new UnityEngine.Vector3(0, 0, (index + 1) * 5f);
            PhysicsLayers.TryGetUnityLayerFromSDKLayer(sdkLayerMask, out int unityLayer);
            colliderTransform.gameObject.layer = unityLayer;
            BoxCollider collider = colliderTransform.gameObject.AddComponent<BoxCollider>();
            collider.size = UnityEngine.Vector3.one;
            collider.isTrigger = false;

            instantiatedTemp.Add(collider);

            if (inSceneCache)
            {
                // Set up scene cache
                entityCollidersSceneCache.TryGetEntity(collider, out Arg.Any<ColliderSceneEntityInfo>())
                                         .Returns(x =>
                                          {
                                              x[1] = new ColliderSceneEntityInfo(Entity.Null, entityId, sdkLayerMask);
                                              return true;
                                          });
            }
            else
            {
                // Ensure scene cache returns false (not found)
                entityCollidersSceneCache.TryGetEntity(collider, out Arg.Any<ColliderSceneEntityInfo>())
                                         .Returns(false);

                // Set up global cache for cross-scene collider
                var sceneEcsExecutor = new SceneEcsExecutor(World.Create());
                var globalEntityInfo = new GlobalColliderSceneEntityInfo(
                    sceneEcsExecutor,
                    new ColliderSceneEntityInfo(Entity.Null, entityId, sdkLayerMask));

                entityCollidersGlobalCache.TryGetSceneEntity(collider, out Arg.Any<GlobalColliderSceneEntityInfo>())
                                          .Returns(x =>
                                           {
                                               x[1] = globalEntityInfo;
                                               return true;
                                           });
            }

            return collider;
        }
    }
}
