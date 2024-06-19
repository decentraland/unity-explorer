using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.ResetExtensions;
using CrdtEcsBridge.Components.Special;
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
using RaycastHit = DCL.ECSComponents.RaycastHit;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.Interaction.Raycast.Tests
{
    public class ExecuteRaycastSystemShould : UnitySystemTestBase<ExecuteRaycastSystem>
    {
        private readonly List<Component> instantiatedTemp = new ();
        private IEntityCollidersSceneCache entityCollidersSceneCache;
        private IReleasablePerformanceBudget budget;
        private ISceneStateProvider sceneStateProvider;
        private Entity sceneRoot;
        private Dictionary<CRDTEntity, Entity> entitiesMap;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PBRaycast pbRaycast;
        private PartitionComponent partitionComponent;
        private Entity raycastEntity;
        private PBRaycastResult raycastResult;

        [SetUp]
        public void SetUp()
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(new ParcelMathHelper.SceneGeometry(UnityEngine.Vector3.zero, new ParcelMathHelper.SceneCircumscribedPlanes()));

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
                entitiesMap = new Dictionary<CRDTEntity, Entity>(),
                ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>(),
                sceneStateProvider = Substitute.For<ISceneStateProvider>()
            );

            system.Initialize();

            ecsToCRDTWriter.When(x => x.PutMessage(Arg.Any<PBRaycastResult>(), Arg.Any<CRDTEntity>()))
                           .Do(c => raycastResult = c.Arg<PBRaycastResult>());

            sceneStateProvider.TickNumber.Returns(5u);

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

            Assert.That(raycastResult.TickNumber, Is.EqualTo(5u));
            Assert.That((UnityEngine.Vector3)raycastResult.Direction, Is.EqualTo(UnityEngine.Vector3.forward));
            Assert.That((UnityEngine.Vector3)raycastResult.GlobalOrigin, Is.EqualTo(UnityEngine.Vector3.zero));
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

            Assert.That(raycastResult.TickNumber, Is.EqualTo(5u));
            Assert.That((UnityEngine.Vector3)raycastResult.Direction, Is.EqualTo(UnityEngine.Vector3.forward));
            Assert.That((UnityEngine.Vector3)raycastResult.GlobalOrigin, Is.EqualTo(UnityEngine.Vector3.zero));
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

            Assert.That(raycastResult.TickNumber, Is.EqualTo(5u));
            Assert.That((UnityEngine.Vector3)raycastResult.Direction, Is.EqualTo(UnityEngine.Vector3.forward));
            Assert.That((UnityEngine.Vector3)raycastResult.GlobalOrigin, Is.EqualTo(UnityEngine.Vector3.zero));
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
        public void KeepExecutionIfContinuous()
        {
            pbRaycast.Continuous = true;
            pbRaycast.QueryType = RaycastQueryType.RqtHitFirst;
            pbRaycast.CollisionMask = (uint)ColliderLayer.ClCustom3;

            system.Update(0);

            Assert.That(world.Get<RaycastComponent>(raycastEntity).Executed, Is.False);
        }

        private void AssertHit(int index, int colliderIndex)
        {
            var sorted = raycastResult.Hits.OrderBy(x => x.Length).ToList();

            RaycastHit hit = sorted[index];
            Assert.That(hit.MeshName, Is.EqualTo(nameof(ExecuteRaycastSystemShould) + colliderIndex));
            Assert.That(hit.EntityId, Is.EqualTo((uint)colliderIndex));
        }

        private void CreateColliders(params ColliderLayer[] sdkLayerMask)
        {
            for (var i = 0; i < sdkLayerMask.Length; i++)
            {
                ColliderLayer mask = sdkLayerMask[i];
                var entity = new CRDTEntity(i);
                Transform colliderTransform = new GameObject(nameof(ExecuteRaycastSystemShould) + i).transform;
                colliderTransform.position = new UnityEngine.Vector3(0, 0, (i + 1) * 5f);
                PhysicsLayers.TryGetUnityLayerFromSDKLayer(mask, out int unityLayer);
                colliderTransform.gameObject.layer = unityLayer;
                BoxCollider collider = colliderTransform.gameObject.AddComponent<BoxCollider>();
                collider.size = UnityEngine.Vector3.one;
                collider.isTrigger = false;

                instantiatedTemp.Add(collider);

                entityCollidersSceneCache.TryGetEntity(collider, out Arg.Any<ColliderSceneEntityInfo>())
                                         .Returns(x =>
                                          {
                                              x[1] = new ColliderSceneEntityInfo(EntityReference.Null, entity, mask);
                                              return true;
                                          });
            }
        }
    }
}
