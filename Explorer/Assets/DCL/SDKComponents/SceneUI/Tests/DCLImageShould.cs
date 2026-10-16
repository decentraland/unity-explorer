using DCL.SDKComponents.SceneUI.Classes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class DCLImageShould
    {
        [Test]
        public void PopulateStretchedQuadSpanningRectWhenNoInset()
        {
            // Arrange
            var vertices = new Vertex[4];
            var rect = new Rect(0f, 0f, 100f, 50f);

            // Act
            DCLImage.PopulateStretchedQuad(vertices, rect);

            // Assert
            Assert.AreEqual(new Vector3(0f, 50f, Vertex.nearZ), vertices[0].position); // bottom-left
            Assert.AreEqual(new Vector3(0f, 0f, Vertex.nearZ), vertices[1].position); // top-left
            Assert.AreEqual(new Vector3(100f, 0f, Vertex.nearZ), vertices[2].position); // top-right
            Assert.AreEqual(new Vector3(100f, 50f, Vertex.nearZ), vertices[3].position); // bottom-right
        }

        [Test]
        public void PopulateStretchedQuadHonoringRectOffsetWhenInset()
        {
            // Arrange - the painted rect is inset by the element's border, so it carries an offset (x/y)
            var vertices = new Vertex[4];
            var rect = new Rect(10f, 20f, 100f, 50f);

            // Act
            DCLImage.PopulateStretchedQuad(vertices, rect);

            // Assert - the quad must span the inset rect, not be anchored at the local origin
            Assert.AreEqual(new Vector3(10f, 70f, Vertex.nearZ), vertices[0].position); // bottom-left
            Assert.AreEqual(new Vector3(10f, 20f, Vertex.nearZ), vertices[1].position); // top-left
            Assert.AreEqual(new Vector3(110f, 20f, Vertex.nearZ), vertices[2].position); // top-right
            Assert.AreEqual(new Vector3(110f, 70f, Vertex.nearZ), vertices[3].position); // bottom-right
        }

        [Test]
        public void ToPaddingRectExpandOverPadding()
        {
            // Arrange - contentRect of an 85x50 padding box with 5/10/15/20 padding
            var contentRect = new Rect(5f, 10f, 65f, 20f);

            // Act
            Rect paddingRect = DCLImage.ToPaddingRect(contentRect, 5f, 10f, 15f, 20f);

            // Assert
            Assert.AreEqual(new Rect(0f, 0f, 85f, 50f), paddingRect);
        }

        [Test]
        public void ToPaddingRectKeepBorderOffsetWithoutPadding()
        {
            // Arrange - no padding, 1px border pushes the content rect off the local origin
            var contentRect = new Rect(1f, 1f, 98f, 48f);

            // Act
            Rect paddingRect = DCLImage.ToPaddingRect(contentRect, 0f, 0f, 0f, 0f);

            // Assert - the rect must stay inside the border, unchanged
            Assert.AreEqual(contentRect, paddingRect);
        }
    }
}
