using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SuperScrollView.Tests
{
    public class LoopGridViewShould
    {
        private const float CELL = 100f;
        private const int ACROSS = 3;
        private const float EPSILON = 0.01f;
        private const float FRAME = 0.02f;
        private const int SNAP_FRAMES = 12;

        private static readonly Vector2 VIEWPORT = new (300f, 300f);
        private static readonly Vector2 CELL_SIZE = new (CELL, CELL);

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

        [TestCase(50f, 0, 14)]
        [TestCase(0f, 3, 14)]
        [TestCase(250f, 0, 20)]
        public void KeepLinesWithinTheRecycleDistanceResident(float recycleDistance, int expectedFirst, int expectedLast)
        {
            // Arrange
            LoopGridView view = Grid(60, out _, recycleDistance: new Vector2(0f, recycleDistance));

            // Act
            TestViews.ScrollTo(view, 120f);

            // Assert
            Assert.That(view.ItemList[0].ItemIndex, Is.EqualTo(expectedFirst));
            Assert.That(view.ItemList[view.ItemList.Count - 1].ItemIndex, Is.EqualTo(expectedLast));
            Assert.That(view.ItemList.Count, Is.EqualTo(expectedLast - expectedFirst + 1));
        }

        [Test]
        public void ReadTheRecycleDistanceOnTheScrollingAxis()
        {
            // Arrange
            LoopGridView view = Grid(60, out _, recycleDistance: new Vector2(250f, 0f));

            // Act
            TestViews.ScrollTo(view, 120f);

            // Assert
            Assert.That(view.ItemList[0].ItemIndex, Is.EqualTo(3));
            Assert.That(view.ItemList[view.ItemList.Count - 1].ItemIndex, Is.EqualTo(14));
        }

        [Test]
        public void KeepInstantiationBoundedWhileScrollingFar()
        {
            // Arrange
            LoopGridView view = Grid(300, out _, recycleDistance: new Vector2(0f, 50f));

            // Act
            TestViews.ScrollTo(view, 3000f);
            int instantiated = TestViews.Instantiated<LoopGridViewItem>(view);
            TestViews.ScrollTo(view, 0f);

            // Assert
            Assert.That(instantiated, Is.EqualTo(15));
            Assert.That(TestViews.Instantiated<LoopGridViewItem>(view), Is.EqualTo(instantiated));
            Assert.That(view.ItemList.Count, Is.EqualTo(12));
        }

        [Test]
        public void PrewarmThePoolFromInitCreateCount()
        {
            // Arrange
            GameObject template = views.GridTemplate(CELL, CELL);
            LoopGridView view = views.Grid(ListItemArrangeType.TopToBottom, VIEWPORT, template, ACROSS, CELL_SIZE, initCreateCount: 6);
            var provider = new CellProvider();

            // Act
            view.InitGridView(0, provider.Provide);
            int prewarmed = TestViews.Instantiated<LoopGridViewItem>(view);
            view.SetListItemCount(4);

            // Assert
            Assert.That(prewarmed, Is.EqualTo(6));
            Assert.That(TestViews.Instantiated<LoopGridViewItem>(view), Is.EqualTo(6));
            Assert.That(TestViews.Active<LoopGridViewItem>(view), Is.EqualTo(4));
        }

        [Test]
        public void ResolveRowAndColumnForAVerticalGrid()
        {
            // Arrange
            LoopGridView view = Grid(30, out CellProvider provider);

            // Act
            LoopGridViewItem cell = view.GetShownItemByItemIndex(7);

            // Assert
            Assert.That(provider.Placement[7], Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(cell.RowIndex, Is.EqualTo(2));
            Assert.That(cell.ColumnIndex, Is.EqualTo(1));
        }

        [Test]
        public void ResolveRowAndColumnForAHorizontalGrid()
        {
            // Arrange
            LoopGridView view = Grid(30, out CellProvider provider, ListItemArrangeType.LeftToRight);

            // Act
            LoopGridViewItem cell = view.GetShownItemByItemIndex(7);

            // Assert
            Assert.That(provider.Placement[7], Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(cell.RowIndex, Is.EqualTo(1));
            Assert.That(cell.ColumnIndex, Is.EqualTo(2));
        }

        [Test]
        public void PlaceCellsByLineAndSlotWithPaddingAndSpacing()
        {
            // Arrange
            GameObject template = views.GridTemplate(CELL, CELL);
            LoopGridView view = views.Grid(ListItemArrangeType.TopToBottom, VIEWPORT, template, ACROSS, CELL_SIZE,
                itemPadding: new Vector2(10f, 10f), padLeft: 5, padTop: 20);
            var provider = new CellProvider();

            // Act
            view.InitGridView(30, provider.Provide);
            LoopGridViewItem cell = view.GetShownItemByItemIndex(4);

            // Assert
            Assert.That(TestViews.LeadingEdgeInViewport(view, cell), Is.EqualTo(130f).Within(EPSILON));
            Assert.That(TestViews.MinorEdgeInContent(view, cell), Is.EqualTo(115f).Within(EPSILON));
            Assert.That(((RectTransform)cell.transform).rect.size, Is.EqualTo(CELL_SIZE));
            Assert.That(view.ScrollRect.content.rect.height, Is.EqualTo(20f + (10f * 110f) - 10f).Within(EPSILON));
        }

        [TestCase(350f, 3, 1, 1)]
        [TestCase(240f, 2, 2, 0)]
        public void DeriveTheCellsAcrossFromTheViewportWhenGridFixedTypeIsOne(float viewportWidth, int expectedAcross, int expectedRow, int expectedColumn)
        {
            // Arrange
            GameObject template = views.GridTemplate(CELL, CELL);
            LoopGridView view = views.Grid(ListItemArrangeType.TopToBottom, new Vector2(viewportWidth, 300f), template, 1, CELL_SIZE,
                itemPadding: new Vector2(10f, 10f), gridFixedType: 1);
            var provider = new CellProvider();

            // Act
            view.InitGridView(30, provider.Provide);
            LoopGridViewItem cell = view.GetShownItemByItemIndex(4);

            // Assert
            Assert.That(view.GetShownItemByItemIndex(expectedAcross - 1).RowIndex, Is.EqualTo(0));
            Assert.That(view.GetShownItemByItemIndex(expectedAcross).RowIndex, Is.EqualTo(1));
            Assert.That(cell.RowIndex, Is.EqualTo(expectedRow));
            Assert.That(cell.ColumnIndex, Is.EqualTo(expectedColumn));
        }

        [Test]
        public void RebindOnlyTheRequestedCellOnRefreshItemByItemIndex()
        {
            // Arrange
            LoopGridView view = Grid(30, out CellProvider provider);
            int bindsOfSeven = provider.Binds[7];
            int bindsOfEight = provider.Binds[8];

            // Act
            view.RefreshItemByItemIndex(7);

            // Assert
            Assert.That(provider.Binds[7], Is.EqualTo(bindsOfSeven + 1));
            Assert.That(provider.Binds[8], Is.EqualTo(bindsOfEight));
            Assert.That(view.GetShownItemByItemIndex(7).ItemIndex, Is.EqualTo(7));
        }

        [Test]
        public void KeepTheScrollPositionWhenTheCountGrowsWithoutReset()
        {
            // Arrange
            LoopGridView view = Grid(30, out _);
            TestViews.ScrollTo(view, 300f);

            // Act
            view.SetListItemCount(60, false);
            float kept = TestViews.Leading(view);
            view.SetListItemCount(60, true);

            // Assert
            Assert.That(kept, Is.EqualTo(300f).Within(EPSILON));
            Assert.That(view.ItemTotalCount, Is.EqualTo(60));
            Assert.That(TestViews.Leading(view), Is.EqualTo(0f).Within(EPSILON));
        }

        [Test]
        public void SnapTheNearestLineOnceInertiaIsSpent()
        {
            // Arrange
            LoopGridView view = SnappingGrid(out _);
            TestViews.ScrollTo(view, 130f);

            // Act
            ((IEndDragHandler)view).OnEndDrag(new PointerEventData(null));
            view.ScrollRect.velocity = new Vector2(0f, 500f);
            view.Tick(FRAME);
            float whileFlinging = TestViews.Leading(view);
            view.ScrollRect.velocity = Vector2.zero;

            for (var i = 0; i < SNAP_FRAMES; i++)
                view.Tick(FRAME);

            // Assert
            Assert.That(whileFlinging, Is.EqualTo(130f).Within(EPSILON));
            Assert.That(TestViews.Leading(view), Is.EqualTo(90f).Within(EPSILON));
            Assert.That(TestViews.LeadingEdgeInViewport(view, view.GetShownItemByItemIndex(9)), Is.EqualTo(210f).Within(EPSILON));
        }

        [Test]
        public void CancelTheSnapOnANewDrag()
        {
            // Arrange
            LoopGridView view = SnappingGrid(out _);
            TestViews.ScrollTo(view, 130f);
            ((IEndDragHandler)view).OnEndDrag(new PointerEventData(null));
            view.Tick(FRAME);
            view.Tick(FRAME);
            float partway = TestViews.Leading(view);

            // Act
            ((IBeginDragHandler)view).OnBeginDrag(new PointerEventData(null));

            for (var i = 0; i < SNAP_FRAMES; i++)
                view.Tick(FRAME);

            // Assert
            Assert.That(partway, Is.LessThan(130f).And.GreaterThan(90f));
            Assert.That(TestViews.Leading(view), Is.EqualTo(partway).Within(EPSILON));
        }

        [Test]
        public void NotSnapWhenSnappingIsDisabled()
        {
            // Arrange
            LoopGridView view = Grid(60, out _);
            TestViews.ScrollTo(view, 130f);

            // Act
            ((IEndDragHandler)view).OnEndDrag(new PointerEventData(null));

            for (var i = 0; i < SNAP_FRAMES; i++)
                view.Tick(FRAME);

            // Assert
            Assert.That(TestViews.Leading(view), Is.EqualTo(130f).Within(EPSILON));
        }

        private LoopGridView Grid(int count, out CellProvider provider, ListItemArrangeType arrange = ListItemArrangeType.TopToBottom, Vector2 recycleDistance = default)
        {
            GameObject template = views.GridTemplate(CELL, CELL);
            LoopGridView view = views.Grid(arrange, VIEWPORT, template, ACROSS, CELL_SIZE, recycleDistance: recycleDistance);
            provider = new CellProvider();
            view.InitGridView(count, provider.Provide);
            return view;
        }

        // A 310 px viewport over 100 px lines with the shipped (0,0) pivots: the line
        // whose bottom edge is nearest the viewport's bottom edge snaps flush to it.
        private LoopGridView SnappingGrid(out CellProvider provider)
        {
            GameObject template = views.GridTemplate(CELL, CELL);
            LoopGridView view = views.Grid(ListItemArrangeType.TopToBottom, new Vector2(VIEWPORT.x, 310f), template, ACROSS, CELL_SIZE, snap: true);
            provider = new CellProvider();
            view.InitGridView(60, provider.Provide);
            return view;
        }
    }
}
