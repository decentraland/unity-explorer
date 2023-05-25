using DCL.ECSComponents;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.Tests
{
    public class SetupRendererShould
    {
        private Mesh mesh;
        private MeshRenderer meshRenderer;

        [SetUp]
        public void SetUp()
        {
            mesh = new Mesh();
            var gameObject = new GameObject();
            gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        [Test]
        public void SetBoxSize()
        {
            new SetupBoxMesh().Execute(new PBMeshRenderer
                { Box = new PBMeshRenderer.Types.BoxMesh() }, mesh);

            meshRenderer.GetComponent<MeshFilter>().mesh = mesh;
            Assert.AreEqual(new Vector3(0.50f, 0.50f, 0.50f), meshRenderer.bounds.extents);
        }

        [Test]
        public void SetSphereSize()
        {
            new SetupSphereMesh().Execute(new PBMeshRenderer
                { Sphere = new PBMeshRenderer.Types.SphereMesh() }, mesh);

            meshRenderer.GetComponent<MeshFilter>().mesh = mesh;
            Assert.Less((meshRenderer.bounds.extents - new Vector3(0.50f, 0.50f, 0.50f)).magnitude, 0.005f);
        }

        [Test]
        public void SetPlaneSize()
        {
            new SetupPlaneMesh().Execute(new PBMeshRenderer
                { Plane = new PBMeshRenderer.Types.PlaneMesh() }, mesh);

            meshRenderer.GetComponent<MeshFilter>().mesh = mesh;
            Assert.AreEqual(new Vector3(0.50f, 0.50f, 0), meshRenderer.bounds.extents);
        }
    }
}
