using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.Tests
{
    public class PrimitiveInstanceBatchesShould
    {
        private readonly List<Object> created = new ();

        private PrimitiveInstanceBatches batches = null!;
        private Mesh box = null!;

        [SetUp]
        public void SetUp()
        {
            batches = new PrimitiveInstanceBatches();
            box = new Mesh();
            BoxFactory.Create(ref box);
            created.Add(box);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
                Object.DestroyImmediate(obj);

            created.Clear();
        }

        [Test]
        public void GroupInstancesByMeshAndMaterial()
        {
            // Arrange
            Material first = NewMaterial();
            Material second = NewMaterial();

            // Act
            batches.Add(box, first, ShadowCastingMode.On, Matrix4x4.identity);
            batches.Add(box, first, ShadowCastingMode.On, Matrix4x4.Translate(Vector3.one));
            batches.Add(box, second, ShadowCastingMode.On, Matrix4x4.identity);

            // Assert
            Assert.AreEqual(2, batches.BatchCount);
            Assert.AreEqual(2, batches.InstanceCount(0));
            Assert.AreEqual(1, batches.InstanceCount(1));
        }

        [Test]
        public void EncapsulateEveryInstanceInWorldBounds()
        {
            // Arrange
            Material material = NewMaterial();

            // Act
            batches.Add(box, material, ShadowCastingMode.On, Matrix4x4.TRS(new Vector3(10f, 0f, 0f), Quaternion.identity, Vector3.one * 2f));
            batches.Add(box, material, ShadowCastingMode.On, Matrix4x4.Translate(new Vector3(-10f, 0f, 0f)));

            // Assert
            Bounds bounds = batches.WorldBounds(0);
            Assert.AreEqual(-10.5f, bounds.min.x, 0.001f);
            Assert.AreEqual(11f, bounds.max.x, 0.001f);
            Assert.AreEqual(1f, bounds.max.y, 0.001f);
        }

        [Test]
        public void ForgetInstancesOnClear()
        {
            // Arrange
            Material material = NewMaterial();
            batches.Add(box, material, ShadowCastingMode.On, Matrix4x4.identity);
            batches.Add(box, material, ShadowCastingMode.On, Matrix4x4.identity);

            // Act
            batches.Clear();
            batches.Add(box, material, ShadowCastingMode.Off, Matrix4x4.identity);

            // Assert
            Assert.AreEqual(1, batches.BatchCount);
            Assert.AreEqual(1, batches.InstanceCount(0));
        }

        [Test]
        public void EnableInstancingOnMaterial()
        {
            // Arrange
            Material material = NewMaterial();
            material.enableInstancing = false;

            // Act
            batches.Add(box, material, ShadowCastingMode.On, Matrix4x4.identity);

            // Assert
            Assert.IsTrue(material.enableInstancing);
        }

        private Material NewMaterial()
        {
            var material = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));
            created.Add(material);
            return material;
        }
    }
}
