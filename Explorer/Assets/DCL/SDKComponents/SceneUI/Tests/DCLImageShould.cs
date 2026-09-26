using DCL.SDKComponents.SceneUI.Classes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class DCLImageShould
    {
        [Test]
        public void PopulateStretchedQuadSpanningContentRectWhenNoInset()
        {
            // Arrange
            var vertices = new Vertex[4];
            var contentRect = new Rect(0f, 0f, 100f, 50f);

            // Act
            DCLImage.PopulateStretchedQuad(vertices, contentRect);

            // Assert
            Assert.AreEqual(new Vector3(0f, 50f, Vertex.nearZ), vertices[0].position); // bottom-left
            Assert.AreEqual(new Vector3(0f, 0f, Vertex.nearZ), vertices[1].position); // top-left
            Assert.AreEqual(new Vector3(100f, 0f, Vertex.nearZ), vertices[2].position); // top-right
            Assert.AreEqual(new Vector3(100f, 50f, Vertex.nearZ), vertices[3].position); // bottom-right
        }

        [Test]
        public void PopulateStretchedQuadHonoringContentRectOffsetWhenInset()
        {
            // Arrange - contentRect is inset by the element's border/padding, so it carries an offset (x/y)
            var vertices = new Vertex[4];
            var contentRect = new Rect(10f, 20f, 100f, 50f);

            // Act
            DCLImage.PopulateStretchedQuad(vertices, contentRect);

            // Assert - the quad must span the inset rect, not be anchored at the local origin
            Assert.AreEqual(new Vector3(10f, 70f, Vertex.nearZ), vertices[0].position); // bottom-left
            Assert.AreEqual(new Vector3(10f, 20f, Vertex.nearZ), vertices[1].position); // top-left
            Assert.AreEqual(new Vector3(110f, 20f, Vertex.nearZ), vertices[2].position); // top-right
            Assert.AreEqual(new Vector3(110f, 70f, Vertex.nearZ), vertices[3].position); // bottom-right
        }
    }
}
