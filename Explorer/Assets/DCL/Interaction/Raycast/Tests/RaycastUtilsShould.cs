using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Raycast.Systems;
using DCL.Interaction.Utility;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.Interaction.Raycast.Tests
{
    public class RaycastUtilsShould : UnitySystemTestBase<ExecuteRaycastSystem>
    {
        private readonly List<Collider> temp = new ();
        private TransformComponent ts;


        public void Setup()
        {
            Entity e = world.Create(new PBRaycast());
            ts = AddTransformToEntity(e);
        }


        public void DestroyGarbage()
        {
            foreach (Collider o in temp)
                UnityObjectUtils.SafeDestroyGameObject(o);

            temp.Clear();
        }


        public void CreateLocalDirectionRay()
        {
            var rot = Quaternion.Euler(0, 45, 0);
            ts.SetTransform(new Vector3(2, 1, 0), rot, Vector3.one);

            var pbRaycast = new PBRaycast { LocalDirection = new Decentraland.Common.Vector3 { X = 0, Y = 0, Z = 10 } };

            Assert.That(pbRaycast.TryCreateRay(world, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>(), Vector3.zero, in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(2, 1, 0)));
            Assert.That(ray.direction, Is.EqualTo(rot * new Vector3(0, 0, 10).normalized));
        }


        public void CreateGlobalTargetRay()
        {
            var pbRaycast = new PBRaycast
            {
                GlobalTarget = new Decentraland.Common.Vector3 { X = 10, Y = 5, Z = 0 },
                OriginOffset = new Decentraland.Common.Vector3 { X = 2, Y = 0, Z = 0 },
            };

            Assert.That(pbRaycast.TryCreateRay(world, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>(), new Vector3(0, 0, 10), in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(2, 0, 0)));
            Assert.That(ray.direction, Is.EqualTo(new Vector3(10, 5, 10).normalized));
        }


        public void CreateTargetEntityRay()
        {
            Entity targetEntity = world.Create();
            TransformComponent targetTs = AddTransformToEntity(targetEntity);

            targetTs.SetTransform(new Vector3(100, 0, 300), Quaternion.identity, Vector3.one);
            world.Set(targetEntity, targetTs);
            ts.SetTransform(new Vector3(-100, 500, 1000), Quaternion.identity, Vector3.one);

            IReadOnlyDictionary<CRDTEntity, Entity> dict = Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>();

            dict.TryGetValue(115, out Arg.Any<Entity>())
                .Returns(x =>
                 {
                     x[1] = targetEntity;
                     return true;
                 });

            var pbRaycast = new PBRaycast { TargetEntity = 115 };

            Assert.That(pbRaycast.TryCreateRay(world, dict, Vector3.zero, in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(-100, 500, 1000)));
            Assert.That(ray.direction, Is.EqualTo(new Vector3(200, -500, -700).normalized));
        }


        public void CreateGlobalDirectionRay()
        {
            var pbRaycast = new PBRaycast { GlobalDirection = new Decentraland.Common.Vector3 { X = -5, Y = 0, Z = 10 } };

            Assert.That(pbRaycast.TryCreateRay(world, Substitute.For<IReadOnlyDictionary<CRDTEntity, Entity>>(), Vector3.zero, in ts, out Ray ray), Is.True);
            Assert.That(ray.origin, Is.EqualTo(new Vector3(0, 0, 0)));
            Assert.That(ray.direction, Is.EqualTo(new Vector3(-5, 0, 10).normalized));
        }


        public void FillSDKRaycastHit()
        {
            BoxCollider collider = new GameObject(nameof(RaycastUtilsShould)).AddComponent<BoxCollider>();
            collider.name = "custom";
            collider.isTrigger = true;
            collider.center = new Vector3(0, 0, 10);
            collider.size = Vector3.one;

            temp.Add(collider);

            var ray = new Ray(Vector3.zero, Vector3.forward);
            Physics.Raycast(ray, out RaycastHit hit, 100, ~0, QueryTriggerInteraction.Collide);

            var sdkHit = new ECSComponents.RaycastHit { Direction = new Decentraland.Common.Vector3(), GlobalOrigin = new Decentraland.Common.Vector3(), Position = new Decentraland.Common.Vector3(), NormalHit = new Decentraland.Common.Vector3() };
            sdkHit.FillSDKRaycastHit(new Vector3(1, 0, 1), hit, collider.name, 100, Vector3.zero, Vector3.forward);

            Assert.That(sdkHit.EntityId, Is.EqualTo(100u));
            Assert.That(sdkHit.MeshName, Is.EqualTo("custom"));
            Assert.That(sdkHit.Length, Is.EqualTo(9.5f).Within(0.01f));
            Assert.That((Vector3)sdkHit.NormalHit, Is.EqualTo(hit.normal));
            Assert.That((Vector3)sdkHit.Position, Is.EqualTo(hit.point - new Vector3(1, 0, 1)));
            Assert.That((Vector3)sdkHit.GlobalOrigin, Is.EqualTo(Vector3.zero));
            Assert.That((Vector3)sdkHit.Direction, Is.EqualTo(Vector3.forward));
        }
    }
}
