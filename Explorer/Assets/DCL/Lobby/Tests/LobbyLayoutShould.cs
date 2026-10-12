using NUnit.Framework;
using UnityEngine;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyLayoutShould
    {
        private static readonly Vector2Int TOP_GRID = new (4, 6);
        private static readonly Vector2Int BOTTOM_GRID = new (6, 2);

        private LobbyLayout layout = null!;

        [SetUp]
        public void SetUp()
        {
            layout = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
        }

        [Test]
        public void PlaceAWidgetInsideItsGrid()
        {
            //Act
            bool placed = layout.TryPlace(LobbyWidgetId.Events3x4, LobbyGridCorner.TopRight, new Vector2Int(1, 2));

            //Assert
            Assert.IsTrue(placed);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Events3x4, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopRight, placement.Corner);
            Assert.AreEqual(new Vector2Int(1, 2), placement.Cell);
            Assert.AreEqual(1, layout.Placements.Count);
        }

        [TestCase(2, 0, TestName = "RejectAPlacementPastTheColumns")]
        [TestCase(0, 3, TestName = "RejectAPlacementPastTheRows")]
        [TestCase(-1, 0, TestName = "RejectAPlacementBeforeTheColumns")]
        [TestCase(0, -1, TestName = "RejectAPlacementBeforeTheRows")]
        public void RejectAPlacementOutsideTheGrid(int x, int y)
        {
            //Act
            bool placed = layout.TryPlace(LobbyWidgetId.Events3x4, LobbyGridCorner.TopLeft, new Vector2Int(x, y));

            //Assert
            Assert.IsFalse(placed);
            Assert.AreEqual(0, layout.Placements.Count);
        }

        [Test]
        public void RejectAnOverlappingPlacement()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Act
            bool placed = layout.TryPlace(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 2));

            //Assert
            Assert.IsFalse(placed);
            Assert.IsFalse(layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4x2, out _));
        }

        [Test]
        public void AllowTheSameCellsOnAnotherGrid()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Act
            bool placed = layout.TryPlace(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopRight, new Vector2Int(0, 0));

            //Assert
            Assert.IsTrue(placed);
        }

        [Test]
        public void MoveAPlacedWidgetOverItsOwnCells()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Act
            bool moved = layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 1));

            //Assert
            Assert.IsTrue(moved);
            Assert.AreEqual(1, layout.Placements.Count);
            Assert.AreEqual(new Vector2Int(0, 1), layout.Placements[0].Cell);
        }

        [Test]
        public void KeepTheOldPlacementWhenAMoveFails()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 4));

            //Act
            bool moved = layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 2));

            //Assert
            Assert.IsFalse(moved);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement placement));
            Assert.AreEqual(new Vector2Int(0, 0), placement.Cell);
        }

        [Test]
        public void RemoveAPlacement()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.JumpBackIn6x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));

            //Act
            bool removed = layout.Remove(LobbyWidgetId.JumpBackIn6x2);
            bool removedAgain = layout.Remove(LobbyWidgetId.JumpBackIn6x2);

            //Assert
            Assert.IsTrue(removed);
            Assert.IsFalse(removedAgain);
            Assert.AreEqual(0, layout.Placements.Count);
            Assert.IsTrue(layout.IsFree(LobbyGridCorner.BottomLeft, new Vector2Int(0, 0), LobbyWidgetId.JumpBackIn6x2.Span()));
        }

        [Test]
        public void ReportOccupiedCellsAsNotFree()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Events3x4, LobbyGridCorner.TopRight, new Vector2Int(1, 0));

            //Act
            bool overlapping = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 3), new Vector2Int(2, 1));
            bool beside = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 0), new Vector2Int(1, 6));
            bool below = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(1, 4), new Vector2Int(3, 2));

            //Assert
            Assert.IsFalse(overlapping);
            Assert.IsTrue(beside);
            Assert.IsTrue(below);
        }

        [Test]
        public void IgnoreAWidgetWhenAsked()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Events3x4, LobbyGridCorner.TopRight, new Vector2Int(1, 0));

            //Act
            bool withoutIgnoring = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 0), new Vector2Int(4, 2));
            bool ignoring = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 0), new Vector2Int(4, 2), LobbyWidgetId.Events3x4);

            //Assert
            Assert.IsFalse(withoutIgnoring);
            Assert.IsTrue(ignoring);
        }

        [Test]
        public void SnapToTheNearestCell()
        {
            //Act
            bool found = layout.TryFindSnapCell(LobbyGridCorner.TopLeft, new Vector2Int(2, 2), new Vector2(1.4f, 2.6f), out Vector2Int cell);

            //Assert
            Assert.IsTrue(found);
            Assert.AreEqual(new Vector2Int(1, 3), cell);
        }

        [Test]
        public void SnapToTheClosestFreeCellWhenTheNearestIsTaken()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Act
            bool found = layout.TryFindSnapCell(LobbyGridCorner.TopLeft, LobbyWidgetId.FriendsOnline4x2.Span(), new Vector2(0f, 2f), out Vector2Int cell);

            //Assert
            Assert.IsTrue(found);
            Assert.AreEqual(new Vector2Int(0, 3), cell);
        }

        [Test]
        public void ClampTheSnapCellToWhereTheSpanFits()
        {
            //Act
            bool found = layout.TryFindSnapCell(LobbyGridCorner.BottomRight, new Vector2Int(3, 1), new Vector2(9f, 7f), out Vector2Int cell);

            //Assert
            Assert.IsTrue(found);
            Assert.AreEqual(new Vector2Int(3, 1), cell);
        }

        [Test]
        public void ClampANegativeSnapPositionToTheFirstCell()
        {
            //Act
            bool found = layout.TryFindSnapCell(LobbyGridCorner.TopLeft, new Vector2Int(1, 1), new Vector2(-3f, -0.7f), out Vector2Int cell);

            //Assert
            Assert.IsTrue(found);
            Assert.AreEqual(new Vector2Int(0, 0), cell);
        }

        [Test]
        public void FailToSnapWhenTheSpanIsLargerThanTheGrid()
        {
            //Act
            bool found = layout.TryFindSnapCell(LobbyGridCorner.BottomLeft, LobbyWidgetId.Events3x4.Span(), Vector2.zero, out _);

            //Assert
            Assert.IsFalse(found);
        }

        [Test]
        public void FailToSnapWhenTheGridIsFull()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.JumpBackIn6x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));

            //Act
            bool found = layout.TryFindSnapCell(LobbyGridCorner.BottomLeft, new Vector2Int(1, 1), new Vector2(2f, 1f), out _);

            //Assert
            Assert.IsFalse(found);
        }

        [Test]
        public void PlaceEveryWidgetByDefault()
        {
            //Act
            layout.ResetToDefault();

            //Assert
            Assert.AreEqual(5, layout.Placements.Count);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement landing));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4x2, out LobbyWidgetPlacement friends));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Events3x4, out LobbyWidgetPlacement events));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.JumpBackIn6x2, out LobbyWidgetPlacement recent));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.RecommendedPlaces6x2, out LobbyWidgetPlacement featured));
            Assert.AreEqual(LobbyGridCorner.TopLeft, landing.Corner);
            Assert.AreEqual(LobbyGridCorner.TopLeft, friends.Corner);
            Assert.AreEqual(LobbyGridCorner.TopRight, events.Corner);
            Assert.AreEqual(LobbyGridCorner.BottomLeft, recent.Corner);
            Assert.AreEqual(LobbyGridCorner.BottomRight, featured.Corner);
        }

        [Test]
        public void ReplaceThePlacementsWithTheDefault()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));

            //Act
            layout.ResetToDefault();

            //Assert
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement landing));
            Assert.AreEqual(LobbyGridCorner.TopLeft, landing.Corner);
        }

        [Test]
        public void ClearEveryPlacement()
        {
            //Arrange
            layout.ResetToDefault();

            //Act
            layout.Clear();

            //Assert
            Assert.AreEqual(0, layout.Placements.Count);
        }

        [Test]
        public void ExposeTheGridSizes()
        {
            Assert.AreEqual(TOP_GRID, layout.GridSize(LobbyGridCorner.TopLeft));
            Assert.AreEqual(TOP_GRID, layout.GridSize(LobbyGridCorner.TopRight));
            Assert.AreEqual(BOTTOM_GRID, layout.GridSize(LobbyGridCorner.BottomLeft));
            Assert.AreEqual(BOTTOM_GRID, layout.GridSize(LobbyGridCorner.BottomRight));
        }
    }
}
