using DCL.ECSComponents;
using NUnit.Framework;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveColliders.Tests
{
    public class SetupColliderShould
    {

        public void SetBoxSize()
        {
            BoxCollider collider = new GameObject("Box").AddComponent<BoxCollider>();
            collider.size = new Vector3(40f, 40f, 33f);
            new SetupBoxCollider().Execute(collider, new PBMeshCollider { Box = new PBMeshCollider.Types.BoxMesh() });

            Assert.AreEqual(new Vector3(PrimitivesSize.CUBE_SIZE, PrimitivesSize.CUBE_SIZE, PrimitivesSize.CUBE_SIZE), collider.size);
        }


        public void SetSphereSize()
        {
            SphereCollider collider = new GameObject("Sphere").AddComponent<SphereCollider>();
            collider.radius = 40f;
            new SetupSphereCollider().Execute(collider, new PBMeshCollider { Sphere = new PBMeshCollider.Types.SphereMesh() });

            Assert.AreEqual(PrimitivesSize.SPHERE_RADIUS, collider.radius);
        }


        public void SetPlaneSize()
        {
            BoxCollider collider = new GameObject("Plane").AddComponent<BoxCollider>();
            collider.size = new Vector3(40f, 40f, 33f);
            new SetupPlaneCollider().Execute(collider, new PBMeshCollider { Plane = new PBMeshCollider.Types.PlaneMesh() });

            Assert.AreEqual(PrimitivesSize.PLANE_SIZE, collider.size);
        }
    }
}
