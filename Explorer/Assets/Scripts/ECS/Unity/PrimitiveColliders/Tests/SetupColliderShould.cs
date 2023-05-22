using DCL.ECSComponents;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders.Tests
{
    public class SetupColliderShould
    {
        [Test]
        public void SetBoxSize()
        {
            BoxCollider collider = new GameObject("Box").AddComponent<BoxCollider>();
            collider.size = new Vector3(40f, 40f, 33f);
            new SetupBoxCollider().Execute(collider, new PBMeshCollider { Box = new PBMeshCollider.Types.BoxMesh() });

            Assert.AreEqual(new Vector3(SetupBoxCollider.CUBE_SIZE, SetupBoxCollider.CUBE_SIZE, SetupBoxCollider.CUBE_SIZE), collider.size);
        }

        [Test]
        public void SetSphereSize()
        {
            SphereCollider collider = new GameObject("Sphere").AddComponent<SphereCollider>();
            collider.radius = 40f;
            new SetupSphereCollider().Execute(collider, new PBMeshCollider { Sphere = new PBMeshCollider.Types.SphereMesh() });

            Assert.AreEqual(SetupSphereCollider.SPHERE_RADIUS, collider.radius);
        }

        [Test]
        public void SetPlaneSize()
        {
            BoxCollider collider = new GameObject("Plane").AddComponent<BoxCollider>();
            collider.size = new Vector3(40f, 40f, 33f);
            new SetupPlaneCollider().Execute(collider, new PBMeshCollider { Plane = new PBMeshCollider.Types.PlaneMesh() });

            Assert.AreEqual(SetupPlaneCollider.SIZE, collider.size);
        }
    }
}
