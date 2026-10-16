using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UI.LayoutGroups.Tests
{
    public class WrappingLayoutGroupShould
    {
        private const float CHILD_WIDTH = 40f;
        private const float CHILD_HEIGHT = 20f;
        private const float SPACING_X = 7f;
        private const float SPACING_Y = 3f;

        private RectTransform group = null!;
        private WrappingLayoutGroup layout = null!;

        [SetUp]
        public void SetUp()
        {
            group = new GameObject(nameof(WrappingLayoutGroupShould)).AddComponent<RectTransform>();
            layout = group.gameObject.AddComponent<WrappingLayoutGroup>();
            layout.SpacingX = SPACING_X;
            layout.SpacingY = SPACING_Y;

            // Room for two children per row, so the third wraps.
            group.sizeDelta = new Vector2((CHILD_WIDTH * 2) + SPACING_X, 0f);

            for (var i = 0; i < 3; i++)
            {
                LayoutElement child = new GameObject($"Child{i}").AddComponent<LayoutElement>();
                child.preferredWidth = CHILD_WIDTH;
                child.preferredHeight = CHILD_HEIGHT;
                child.transform.SetParent(group, false);
            }
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(group.gameObject);

        [Test]
        public void AdvanceAlongTheRowBySpacingX()
        {
            // Act
            LayoutRebuilder.ForceRebuildLayoutImmediate(group);

            // Assert
            Assert.AreEqual(CHILD_WIDTH + SPACING_X, Child(1).anchoredPosition.x - Child(0).anchoredPosition.x, 0.01f);
            Assert.AreEqual(Child(0).anchoredPosition.y, Child(1).anchoredPosition.y, 0.01f);
        }

        [Test]
        public void StartTheNextRowBelowBySpacingY()
        {
            // Act
            LayoutRebuilder.ForceRebuildLayoutImmediate(group);

            // Assert
            Assert.AreEqual(Child(0).anchoredPosition.x, Child(2).anchoredPosition.x, 0.01f);
            Assert.AreEqual(-(CHILD_HEIGHT + SPACING_Y), Child(2).anchoredPosition.y - Child(0).anchoredPosition.y, 0.01f);
        }

        [Test]
        public void ReportTheHeightOfAllRows()
        {
            // Act
            LayoutRebuilder.ForceRebuildLayoutImmediate(group);

            // Assert
            Assert.AreEqual((CHILD_HEIGHT * 2) + SPACING_Y, layout.preferredHeight, 0.01f);
        }

        private RectTransform Child(int index) =>
            (RectTransform)group.GetChild(index);
    }
}
