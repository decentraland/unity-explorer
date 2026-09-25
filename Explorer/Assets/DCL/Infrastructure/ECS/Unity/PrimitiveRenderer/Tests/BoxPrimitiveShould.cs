using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using Google.Protobuf.Collections;
using NUnit.Framework;

namespace ECS.Unity.PrimitiveRenderer.Tests
{
    public class BoxPrimitiveShould
    {
        [Test]
        public void ShareMeshWhenUVsAreDefault()
        {
            var first = new BoxPrimitive();
            var second = new BoxPrimitive();

            first.ApplyUVs(null);
            second.ApplyUVs(new RepeatedField<float>());

            Assert.AreSame(first.Mesh, second.Mesh);
        }

        [Test]
        public void UseOwnMeshWhenUVsAreCustom()
        {
            var defaultBox = new BoxPrimitive();
            var customBox = new BoxPrimitive();

            customBox.ApplyUVs(CustomUVs());

            Assert.AreNotSame(defaultBox.Mesh, customBox.Mesh);
            Assert.AreEqual(0.5f, customBox.Mesh.uv[0].x);
        }

        [Test]
        public void ReturnToSharedMeshWhenUVsAreReset()
        {
            var defaultBox = new BoxPrimitive();
            var box = new BoxPrimitive();

            box.ApplyUVs(CustomUVs());
            box.ApplyUVs(null);

            Assert.AreSame(defaultBox.Mesh, box.Mesh);
        }

        private static RepeatedField<float> CustomUVs()
        {
            var uvs = new RepeatedField<float>();

            for (var i = 0; i < 48; i++)
                uvs.Add(0.5f);

            return uvs;
        }
    }
}
