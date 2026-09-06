using NUnit.Framework;
using UnityEngine;

namespace SuperScrollView.Tests
{
    public class ScrollSpaceShould
    {
        private const float EPSILON = 0.001f;

        private GameObject parentGo;
        private RectTransform parent;

        [SetUp]
        public void SetUp()
        {
            parentGo = new GameObject("Viewport", typeof(RectTransform));
            parent = (RectTransform)parentGo.transform;
            parent.sizeDelta = new Vector2(300f, 400f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parentGo);
        }

        [TestCase(ListItemArrangeType.TopToBottom)]
        [TestCase(ListItemArrangeType.BottomToTop)]
        [TestCase(ListItemArrangeType.LeftToRight)]
        [TestCase(ListItemArrangeType.RightToLeft)]
        public void RoundTripLeadingOnAStretchedContentWithAnOffCentrePivot(ListItemArrangeType arrange)
        {
            // Arrange
            RectTransform content = Child(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.3f, 0.7f), new Vector2(200f, 600f));

            // Act
            ScrollSpace.SetLeadingOfContent(arrange, content, parent.rect, 123.4f);

            // Assert
            Assert.That(ScrollSpace.LeadingOfContent(arrange, content, parent.rect), Is.EqualTo(123.4f).Within(EPSILON));
        }

        [Test]
        public void ReportZeroLeadingWhenTheContentIsFlushWithTheViewport()
        {
            // Arrange
            RectTransform content = Child(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1000f));

            // Act
            float leading = ScrollSpace.LeadingOfContent(ListItemArrangeType.TopToBottom, content, parent.rect);

            // Assert
            Assert.That(leading, Is.EqualTo(0f).Within(EPSILON));
        }

        [Test]
        public void PlaceAnItemEdgeAtTheRequestedDistanceFromEitherContentEdge()
        {
            // Arrange
            RectTransform content = Child(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1000f));
            RectTransform item = Child(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(100f, 40f), content);
            Rect contentRect = content.rect;

            // Act
            ScrollSpace.SetAnchoredComponent(item, 1, ScrollSpace.AnchoredForEdge(item, contentRect, 1, 250f, true));
            ScrollSpace.EdgesInParent(item, contentRect, out _, out Vector2 maxFromTop);

            ScrollSpace.SetAnchoredComponent(item, 1, ScrollSpace.AnchoredForEdge(item, contentRect, 1, 250f, false));
            ScrollSpace.EdgesInParent(item, contentRect, out Vector2 minFromBottom, out _);

            // Assert
            Assert.That(contentRect.yMax - maxFromTop.y, Is.EqualTo(250f).Within(EPSILON));
            Assert.That(minFromBottom.y - contentRect.yMin, Is.EqualTo(250f).Within(EPSILON));
        }

        [Test]
        public void LeaveTheOtherAnchoredComponentUntouched()
        {
            // Arrange
            RectTransform item = Child(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, 40f));
            item.anchoredPosition = new Vector2(17f, -5f);

            // Act
            ScrollSpace.SetAnchoredComponent(item, 1, -300f);

            // Assert
            Assert.That(item.anchoredPosition, Is.EqualTo(new Vector2(17f, -300f)));
        }

        [Test]
        public void ReportAStretchOnlyWhereTheAnchorsDiffer()
        {
            // Arrange
            RectTransform item = Child(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 40f));

            // Act & Assert
            Assert.That(ScrollSpace.StretchesOn(item, 0), Is.True);
            Assert.That(ScrollSpace.StretchesOn(item, 1), Is.False);
        }

        [Test]
        public void AccountForTheAnchorSpanWhenSizingAStretchedContent()
        {
            // Arrange
            RectTransform pinned = Child(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero);
            RectTransform stretched = Child(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero);

            // Act
            float pinnedDelta = ScrollSpace.SizeDeltaForExtent(pinned, parent.rect, 1, 1000f);
            float stretchedDelta = ScrollSpace.SizeDeltaForExtent(stretched, parent.rect, 1, 1000f);

            // Assert
            Assert.That(pinnedDelta, Is.EqualTo(1000f).Within(EPSILON));
            Assert.That(stretchedDelta, Is.EqualTo(600f).Within(EPSILON));
        }

        [TestCase(ListItemArrangeType.TopToBottom, 0f, 1f, 0f)]
        [TestCase(ListItemArrangeType.TopToBottom, 0f, 0f, 1f)]
        [TestCase(ListItemArrangeType.BottomToTop, 0f, 0f, 0f)]
        [TestCase(ListItemArrangeType.BottomToTop, 0f, 1f, 1f)]
        [TestCase(ListItemArrangeType.LeftToRight, 0f, 0f, 0f)]
        [TestCase(ListItemArrangeType.LeftToRight, 1f, 0f, 1f)]
        [TestCase(ListItemArrangeType.RightToLeft, 1f, 0f, 0f)]
        [TestCase(ListItemArrangeType.RightToLeft, 0f, 0f, 1f)]
        public void MeasureASnapPivotFromTheLeadingSide(ListItemArrangeType arrange, float pivotX, float pivotY, float expected)
        {
            // Act
            float factor = ScrollSpace.SnapFactor(arrange, new Vector2(pivotX, pivotY));

            // Assert
            Assert.That(factor, Is.EqualTo(expected).Within(EPSILON));
        }

        private RectTransform Child(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, RectTransform under = null)
        {
            var go = new GameObject("Child", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(under != null ? under : parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }
    }
}
