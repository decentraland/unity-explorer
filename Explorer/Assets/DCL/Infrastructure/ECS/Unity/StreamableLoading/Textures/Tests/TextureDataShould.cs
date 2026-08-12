using DCL.Diagnostics.Tests;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.Textures.Tests
{
    [TestFixture]
    public class TextureDataShould
    {
        // EnsureTexture2D is a pure, log-free accessor: it must not touch ReportHub on the hot path.
        [Test]
        public void EnsureTexture2D_DoesNotLog()
        {
            using var scope = new MockedReportScope();

            var texture = new Texture2D(2, 2);
            var data = new TextureData(AnyTexture.FromTexture2D(texture));

            data.EnsureTexture2D();

            Assert.That(scope.Mock.ReceivedCalls(), Is.Empty,
                "EnsureTexture2D must not emit any report/log");

            Object.DestroyImmediate(texture);
        }
    }
}
