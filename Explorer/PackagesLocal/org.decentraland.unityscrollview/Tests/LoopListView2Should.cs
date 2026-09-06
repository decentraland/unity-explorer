using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SuperScrollView.Tests
{
    public class LoopListView2Should
    {
        private const float ROW = 40f;
        private const float EPSILON = 0.01f;
        private const float FRAME = 0.02f;
        private const int SNAP_FRAMES = 12;

        private static readonly Vector2 VIEWPORT = new (300f, 400f);

        private TestViews views;

        [SetUp]
        public void SetUp()
        {
            views = new TestViews();
        }

        [TearDown]
        public void TearDown()
        {
            views.Dispose();
        }

        [Test]
        public void KeepOnlyTheWindowResidentWhileScrolling()
        {
            // Arrange
            LoopListView2 view = UniformList(200, out _);

            // Act
            TestViews.ScrollTo(view, 4000f);
            int instantiated = TestViews.Instantiated<LoopListViewItem2>(view);
            TestViews.ScrollTo(view, 0f);

            // Assert
            Assert.That(view.ItemList.Count, Is.EqualTo(13));
            Assert.That(view.ItemList[0].ItemIndex, Is.EqualTo(0));
            Assert.That(view.GetShownItemByItemIndex(100), Is.Null);
            Assert.That(instantiated, Is.EqualTo(15));
            Assert.That(TestViews.Instantiated<LoopListViewItem2>(view), Is.EqualTo(instantiated));
        }

        [Test]
        public void ShowTheWindowAroundTheScrolledPosition()
        {
            // Arrange
            LoopListView2 view = UniformList(200, out _);

            // Act
            TestViews.ScrollTo(view, 4000f);

            // Assert
            Assert.That(view.ItemList.Count, Is.EqualTo(15));
            Assert.That(view.ItemList[0].ItemIndex, Is.EqualTo(98));
            Assert.That(view.ItemList[14].ItemIndex, Is.EqualTo(112));
            Assert.That(TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(100)), Is.EqualTo(0f).Within(EPSILON));
            Assert.That(view.GetShownItemByItemIndex(5), Is.Null);
        }

        [Test]
        public void PrewarmThePoolFromInitCreateCount()
        {
            // Arrange
            GameObject template = views.ListTemplate(VIEWPORT.x, ROW);
            LoopListView2 view = views.List(ListItemArrangeType.TopToBottom, VIEWPORT, template, initCreateCount: 5);
            var provider = new RowProvider(true);

            // Act
            view.InitListView(0, provider.Provide);
            int prewarmed = TestViews.Instantiated<LoopListViewItem2>(view);
            int activeBefore = TestViews.Active<LoopListViewItem2>(view);
            view.SetListItemCount(3);

            // Assert
            Assert.That(prewarmed, Is.EqualTo(5));
            Assert.That(activeBefore, Is.EqualTo(0));
            Assert.That(TestViews.Instantiated<LoopListViewItem2>(view), Is.EqualTo(5));
            Assert.That(TestViews.Active<LoopListViewItem2>(view), Is.EqualTo(3));
            Assert.That(view.ItemList.Count, Is.EqualTo(3));
        }

        [Test]
        public void MeasureVariableRowExtentsIntoTheContentSize()
        {
            // Arrange
            LoopListView2 view = VariableList(30, out RowProvider provider);

            // Act
            for (var leading = 0f; leading <= 2000f; leading += 150f)
                TestViews.ScrollTo(view, leading);

            // Assert
            Assert.That(view.ScrollRect.content.rect.height, Is.EqualTo(1800f).Within(EPSILON));

            for (int i = 0; i < view.ItemList.Count; i++)
                Assert.That(view.ItemList[i].ItemSizeWithPadding, Is.EqualTo(provider.ExtentOf(view.ItemList[i].ItemIndex)).Within(EPSILON));
        }

        [Test]
        public void TreatRowPaddingAsPartOfTheExtent()
        {
            // Arrange
            GameObject template = views.ListTemplate(VIEWPORT.x, ROW);
            LoopListView2 view = views.List(ListItemArrangeType.TopToBottom, VIEWPORT, template, padding: 4f);
            var provider = new RowProvider(true);

            // Act
            view.InitListView(10, provider.Provide);

            // Assert
            Assert.That(view.ScrollRect.content.rect.height, Is.EqualTo(440f).Within(EPSILON));
            Assert.That(view.GetShownItemByItemIndex(3).ItemSizeWithPadding, Is.EqualTo(44f).Within(EPSILON));
            Assert.That(TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(3)), Is.EqualTo(132f).Within(EPSILON));
        }

        [Test]
        public void InsetTheFirstRowByStartPosOffset()
        {
            // Arrange
            GameObject template = views.ListTemplate(VIEWPORT.x, ROW);
            LoopListView2 view = views.List(ListItemArrangeType.TopToBottom, VIEWPORT, template, startPosOffset: 30f);
            var provider = new RowProvider(true);

            // Act
            view.InitListView(10, provider.Provide);
            float insetAtRest = TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(0));
            view.MovePanelToItemIndex(0, 0f);

            // Assert
            Assert.That(insetAtRest, Is.EqualTo(30f).Within(EPSILON));
            Assert.That(view.ScrollRect.content.rect.height, Is.EqualTo(430f).Within(EPSILON));
            Assert.That(TestViews.Leading(view), Is.EqualTo(30f).Within(EPSILON));
        }

        [Test]
        public void KeepTheScrollPositionWhenTheCountGrowsWithoutReset()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _);
            TestViews.ScrollTo(view, 800f);

            // Act
            view.SetListItemCount(80, false);
            float kept = TestViews.Leading(view);
            float rowAtTop = TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(20));
            view.SetListItemCount(80, true);

            // Assert
            Assert.That(kept, Is.EqualTo(800f).Within(EPSILON));
            Assert.That(rowAtTop, Is.EqualTo(0f).Within(EPSILON));
            Assert.That(view.ItemTotalCount, Is.EqualTo(80));
            Assert.That(TestViews.Leading(view), Is.EqualTo(0f).Within(EPSILON));
        }

        [TestCase(ListItemArrangeType.TopToBottom)]
        [TestCase(ListItemArrangeType.BottomToTop)]
        [TestCase(ListItemArrangeType.LeftToRight)]
        [TestCase(ListItemArrangeType.RightToLeft)]
        public void LandExactlyOnTheRequestedRowAfterAJumpAcrossUnmeasuredRows(ListItemArrangeType arrange)
        {
            // Arrange
            LoopListView2 view = VariableList(60, out _, arrange);

            // Act
            view.MovePanelToItemIndex(45, 0f);
            float flush = TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(45));
            view.MovePanelToItemIndex(12, 100f);
            float inset = TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(12));

            // Assert
            Assert.That(flush, Is.EqualTo(0f).Within(0.5f));
            Assert.That(inset, Is.EqualTo(100f).Within(0.5f));
        }

        [Test]
        public void ClampAJumpToTheScrollableRange()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _);

            // Act
            view.MovePanelToItemIndex(49, 0f);

            // Assert
            Assert.That(TestViews.Leading(view), Is.EqualTo(1600f).Within(EPSILON));
            Assert.That(view.GetShownItemByItemIndex(49), Is.Not.Null);
        }

        [Test]
        public void AlignTheJumpedToRowWithTheViewportInWorldSpace()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _);
            var rowCorners = new Vector3[4];
            var viewportCorners = new Vector3[4];

            // Act
            view.MovePanelToItemIndex(20, 0f);
            ((RectTransform)view.GetShownItemByItemIndex(20).transform).GetWorldCorners(rowCorners);
            view.ScrollRect.viewport.GetWorldCorners(viewportCorners);

            // Assert
            Assert.That(rowCorners[1].y, Is.EqualTo(viewportCorners[1].y).Within(EPSILON));
        }

        [TestCase(ListItemArrangeType.TopToBottom, 120f, 120f)]
        [TestCase(ListItemArrangeType.BottomToTop, -120f, 120f)]
        [TestCase(ListItemArrangeType.LeftToRight, -120f, 120f)]
        [TestCase(ListItemArrangeType.RightToLeft, 120f, 120f)]
        public void MoveThePanelInTheContentsAnchoredSpace(ListItemArrangeType arrange, float delta, float expectedLeadingShift)
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _, arrange);
            TestViews.ScrollTo(view, 400f);
            Vector2 anchoredBefore = view.ScrollRect.content.anchoredPosition;
            int axis = ScrollSpace.MajorAxis(arrange);

            // Act
            view.MovePanelByOffset(delta);

            // Assert
            Assert.That(TestViews.Leading(view), Is.EqualTo(400f + expectedLeadingShift).Within(EPSILON));
            Assert.That(view.ScrollRect.content.anchoredPosition[axis] - anchoredBefore[axis], Is.EqualTo(delta).Within(EPSILON));
        }

        [Test]
        public void RebindOnlyTheRequestedRowOnRefreshItemByItemIndex()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out RowProvider provider);
            int bindsOfThree = provider.Binds[3];
            int bindsOfFour = provider.Binds[4];

            // Act
            view.RefreshItemByItemIndex(3);

            // Assert
            Assert.That(provider.Binds[3], Is.EqualTo(bindsOfThree + 1));
            Assert.That(provider.Binds[4], Is.EqualTo(bindsOfFour));
            Assert.That(view.GetShownItemByItemIndex(3).ItemIndex, Is.EqualTo(3));
        }

        [Test]
        public void RebindEveryShownRowOnRefreshAllShownItem()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out RowProvider provider);
            var before = new Dictionary<int, int>(provider.Binds);
            int instantiated = TestViews.Instantiated<LoopListViewItem2>(view);

            // Act
            view.RefreshAllShownItem();

            // Assert
            for (int i = 0; i < view.ItemList.Count; i++)
                Assert.That(provider.Binds[view.ItemList[i].ItemIndex], Is.EqualTo(before[view.ItemList[i].ItemIndex] + 1));

            Assert.That(TestViews.Instantiated<LoopListViewItem2>(view), Is.EqualTo(instantiated));
        }

        [Test]
        public void RewindToTheLeadingEdgeKeepingItsRowsOnResetListView()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _);
            TestViews.ScrollTo(view, 800f);
            int instantiated = TestViews.Instantiated<LoopListViewItem2>(view);

            // Act
            view.ResetListView();

            // Assert
            Assert.That(view.ItemTotalCount, Is.EqualTo(50));
            Assert.That(TestViews.Leading(view), Is.EqualTo(0f).Within(0.01f));
            Assert.That(view.ItemList.Count, Is.GreaterThan(0));
            Assert.That(view.ItemList[0].ItemIndex, Is.EqualTo(0));
            Assert.That(TestViews.Instantiated<LoopListViewItem2>(view), Is.EqualTo(instantiated));
        }

        [Test]
        public void KeepShowingRowsWhenResetAfterTheCountIsRefreshed()
        {
            // Arrange — the panel sections refresh the count and then reset the view;
            // a reset that dropped the count would leave that sequence showing nothing.
            LoopListView2 view = UniformList(50, out _);

            // Act
            view.SetListItemCount(50, false);
            view.RefreshAllShownItem();
            view.ResetListView();

            // Assert
            Assert.That(view.ItemTotalCount, Is.EqualTo(50));
            Assert.That(TestViews.Active<LoopListViewItem2>(view), Is.GreaterThan(0));
        }

        [Test]
        public void VisitEveryShownRowInIndexOrder()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _);
            TestViews.ScrollTo(view, 800f);
            var visited = new List<int>();

            // Act
            view.DoActionForEachShownItem(static (row, state) => ((List<int>)state).Add(row.ItemIndex), visited);

            // Assert
            Assert.That(visited.Count, Is.EqualTo(view.ItemList.Count));

            for (int i = 1; i < visited.Count; i++)
                Assert.That(visited[i], Is.EqualTo(visited[i - 1] + 1));

            Assert.That(visited[0], Is.EqualTo(18));
        }

        [Test]
        public void ReturnNullForIndicesOutsideTheWindowOrTheCount()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _);

            // Act & Assert
            Assert.That(view.GetShownItemByItemIndex(5), Is.Not.Null);
            Assert.That(view.GetShownItemByItemIndex(30), Is.Null);
            Assert.That(view.GetShownItemByItemIndex(50), Is.Null);
            Assert.That(view.GetShownItemByItemIndex(-1), Is.Null);
        }

        [Test]
        public void SnapTheNearestRowOnceInertiaIsSpent()
        {
            // Arrange
            LoopListView2 view = SnappingList(out _);
            TestViews.ScrollTo(view, 413f);

            // Act
            ((IEndDragHandler)view).OnEndDrag(new PointerEventData(null));
            view.ScrollRect.velocity = new Vector2(0f, 500f);
            view.Tick(FRAME);
            float whileFlinging = TestViews.Leading(view);
            view.ScrollRect.velocity = Vector2.zero;

            for (var i = 0; i < SNAP_FRAMES; i++)
                view.Tick(FRAME);

            // Assert
            Assert.That(whileFlinging, Is.EqualTo(413f).Within(EPSILON));
            Assert.That(TestViews.Leading(view), Is.EqualTo(430f).Within(EPSILON));
            Assert.That(TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(20)), Is.EqualTo(370f).Within(EPSILON));
        }

        [Test]
        public void CancelTheSnapOnANewDrag()
        {
            // Arrange
            LoopListView2 view = SnappingList(out _);
            TestViews.ScrollTo(view, 413f);
            ((IEndDragHandler)view).OnEndDrag(new PointerEventData(null));
            view.Tick(FRAME);
            view.Tick(FRAME);
            float partway = TestViews.Leading(view);

            // Act
            ((IBeginDragHandler)view).OnBeginDrag(new PointerEventData(null));

            for (var i = 0; i < SNAP_FRAMES; i++)
                view.Tick(FRAME);

            // Assert
            Assert.That(partway, Is.GreaterThan(413f).And.LessThan(430f));
            Assert.That(TestViews.Leading(view), Is.EqualTo(partway).Within(EPSILON));
        }

        [Test]
        public void CancelTheSnapOnAProgrammaticMove()
        {
            // Arrange
            LoopListView2 view = SnappingList(out _);
            TestViews.ScrollTo(view, 413f);
            ((IEndDragHandler)view).OnEndDrag(new PointerEventData(null));
            view.Tick(FRAME);
            view.Tick(FRAME);

            // Act
            view.MovePanelToItemIndex(5, 0f);

            for (var i = 0; i < SNAP_FRAMES; i++)
                view.Tick(FRAME);

            // Assert
            Assert.That(TestViews.Leading(view), Is.EqualTo(200f).Within(EPSILON));
        }

        [Test]
        public void NotSnapWhenSnappingIsDisabled()
        {
            // Arrange
            LoopListView2 view = UniformList(50, out _);
            TestViews.ScrollTo(view, 413f);

            // Act
            ((IEndDragHandler)view).OnEndDrag(new PointerEventData(null));

            for (var i = 0; i < SNAP_FRAMES; i++)
                view.Tick(FRAME);

            // Assert
            Assert.That(TestViews.Leading(view), Is.EqualTo(413f).Within(EPSILON));
        }

        private LoopListView2 UniformList(int count, out RowProvider provider, ListItemArrangeType arrange = ListItemArrangeType.TopToBottom)
        {
            bool vertical = ScrollSpace.IsVertical(arrange);
            Vector2 viewport = vertical ? VIEWPORT : new Vector2(VIEWPORT.y, VIEWPORT.x);
            GameObject template = vertical ? views.ListTemplate(viewport.x, ROW) : views.ListTemplate(ROW, viewport.y);
            LoopListView2 view = views.List(arrange, viewport, template);
            provider = new RowProvider(vertical);
            view.InitListView(count, provider.Provide);
            return view;
        }

        // Extents cycle 40/60/80 so the running average sits well above the
        // template's 40 px seed and every unmeasured row starts on a low estimate.
        private LoopListView2 VariableList(int count, out RowProvider provider, ListItemArrangeType arrange = ListItemArrangeType.TopToBottom)
        {
            bool vertical = ScrollSpace.IsVertical(arrange);
            Vector2 viewport = vertical ? VIEWPORT : new Vector2(VIEWPORT.y, VIEWPORT.x);
            GameObject template = vertical ? views.ListTemplate(viewport.x, ROW) : views.ListTemplate(ROW, viewport.y);
            LoopListView2 view = views.List(arrange, viewport, template);
            provider = new RowProvider(vertical) { ExtentOf = static i => ROW + ((i % 3) * 20f) };
            view.InitListView(count, provider.Provide);
            return view;
        }

        // A 410 px viewport over 40 px rows with the shipped (0,0) pivots: the row
        // whose bottom edge is nearest the viewport's bottom edge snaps flush to it.
        private LoopListView2 SnappingList(out RowProvider provider)
        {
            var viewport = new Vector2(VIEWPORT.x, 410f);
            GameObject template = views.ListTemplate(viewport.x, ROW);
            LoopListView2 view = views.List(ListItemArrangeType.TopToBottom, viewport, template, snap: true);
            provider = new RowProvider(true);
            view.InitListView(50, provider.Provide);
            return view;
        }
    }
}
