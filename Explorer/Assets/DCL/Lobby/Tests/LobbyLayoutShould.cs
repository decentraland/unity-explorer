using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyLayoutShould
    {
        private static readonly Vector2Int TOP_GRID = new (4, 6);
        private static readonly Vector2Int BOTTOM_GRID = new (6, 2);

        private LobbyLayout layout = null!;
        private List<LobbyWidgetPlacement> pushed = null!;

        [SetUp]
        public void SetUp()
        {
            layout = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            pushed = new List<LobbyWidgetPlacement>();
        }

        [Test]
        public void PlaceAWidgetInsideItsGrid()
        {
            //Act
            bool placed = layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopRight, new Vector2Int(1, 2));

            //Assert
            Assert.IsTrue(placed);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.LiveEvents3x3, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopRight, placement.Corner);
            Assert.AreEqual(new Vector2Int(1, 2), placement.Cell);
            Assert.AreEqual(1, layout.Placements.Count);
        }

        [TestCase(2, 0, TestName = "RejectAPlacementPastTheColumns")]
        [TestCase(0, 4, TestName = "RejectAPlacementPastTheRows")]
        [TestCase(-1, 0, TestName = "RejectAPlacementBeforeTheColumns")]
        [TestCase(0, -1, TestName = "RejectAPlacementBeforeTheRows")]
        public void RejectAPlacementOutsideTheGrid(int x, int y)
        {
            //Act
            bool placed = layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopLeft, new Vector2Int(x, y));

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
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopRight, new Vector2Int(1, 0));

            //Act
            bool overlapping = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 2), new Vector2Int(2, 1));
            bool beside = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 0), new Vector2Int(1, 6));
            bool below = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(1, 3), new Vector2Int(3, 3));

            //Assert
            Assert.IsFalse(overlapping);
            Assert.IsTrue(beside);
            Assert.IsTrue(below);
        }

        [Test]
        public void IgnoreAWidgetWhenAsked()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopRight, new Vector2Int(1, 0));

            //Act
            bool withoutIgnoring = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 0), new Vector2Int(4, 2));
            bool ignoring = layout.IsFree(LobbyGridCorner.TopRight, new Vector2Int(0, 0), new Vector2Int(4, 2), LobbyWidgetId.LiveEvents3x3);

            //Assert
            Assert.IsFalse(withoutIgnoring);
            Assert.IsTrue(ignoring);
        }

        [Test]
        public void PushTheWidgetDownWhenCoveringItsTopRows()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(1, pushed.Count);
            Assert.AreEqual(LobbyWidgetId.Landing4x3, pushed[0].WidgetId);
            Assert.AreEqual(LobbyGridCorner.TopLeft, pushed[0].Corner);
            Assert.AreEqual(new Vector2Int(0, 2), pushed[0].Cell);

            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement landing), "Resolving changes nothing");
            Assert.AreEqual(new Vector2Int(0, 0), landing.Cell);
            Assert.IsFalse(layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4x2, out _));
        }

        [Test]
        public void PushTheWidgetRightWhenCoveringItsLeftColumns()
        {
            //Arrange
            layout = new LobbyLayout(new Vector2Int(8, 6), TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopLeft, new Vector2Int(3, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(1, pushed.Count);
            Assert.AreEqual(new Vector2Int(4, 0), PushedCell(LobbyWidgetId.UpcomingEvents3x2));
        }

        [Test]
        public void PushTheWidgetLeftWhenCoveringItsRightColumns()
        {
            //Arrange
            layout = new LobbyLayout(new Vector2Int(8, 6), TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopLeft, new Vector2Int(1, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(3, 0), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(1, pushed.Count);
            Assert.AreEqual(new Vector2Int(0, 0), PushedCell(LobbyWidgetId.UpcomingEvents3x2));
        }

        [Test]
        public void PushTheWidgetUpWhenCoveringItsBottomRows()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 2));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 3), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(1, pushed.Count);
            Assert.AreEqual(new Vector2Int(0, 1), PushedCell(LobbyWidgetId.UpcomingEvents3x2));
        }

        [Test]
        public void PushOffTheNextCheapestSideWhenTheGridBlocksTheCheapest()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 1), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(new Vector2Int(0, 3), PushedCell(LobbyWidgetId.Landing4x3), "Up would leave the grid, so the widget goes down");
        }

        [Test]
        public void PushSidewaysOnAGridTooShortToPushDown()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsFalse(resolved, "Right would leave a six-column grid and down a two-row one");

            //Act
            layout = new LobbyLayout(TOP_GRID, TOP_GRID, new Vector2Int(7, 2), BOTTOM_GRID);
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));
            resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(new Vector2Int(4, 0), PushedCell(LobbyWidgetId.UpcomingEvents3x2));
        }

        [Test]
        public void PushAChainedWidgetOffTheSideOfTheWidgetThatLandsOnIt()
        {
            //Arrange
            layout = new LobbyLayout(new Vector2Int(9, 6), TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopRight, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopLeft, new Vector2Int(3, 1));
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopLeft, new Vector2Int(6, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(2, pushed.Count);
            Assert.AreEqual(new Vector2Int(4, 1), PushedCell(LobbyWidgetId.UpcomingEvents3x2), "Pushed right off the placed widget");
            Assert.AreEqual(new Vector2Int(6, 3), PushedCell(LobbyWidgetId.LiveEvents3x3), "Pushed down off the pushed widget, right would leave the grid");
        }

        [Test]
        public void FailWhenAChainedPushCanOnlyLandOnThePlacedWidget()
        {
            //Arrange
            layout = new LobbyLayout(new Vector2Int(9, 4), TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopRight, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopLeft, new Vector2Int(3, 1));
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopLeft, new Vector2Int(6, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsFalse(resolved, "Every side but left leaves the grid and left lands on the placed widget");
            Assert.AreEqual(0, pushed.Count);
        }

        [Test]
        public void PushTheWidgetsAPushedWidgetLandsOn()
        {
            //Arrange
            layout = new LobbyLayout(new Vector2Int(4, 8), TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 2));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(2, pushed.Count);
            Assert.AreEqual(new Vector2Int(0, 2), PushedCell(LobbyWidgetId.UpcomingEvents3x2));
            Assert.AreEqual(new Vector2Int(0, 4), PushedCell(LobbyWidgetId.LiveEvents3x3));
        }

        [Test]
        public void PushOnlyTheWidgetsInTheWay()
        {
            //Arrange
            layout = new LobbyLayout(new Vector2Int(7, 8), TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.UpcomingEvents3x2, LobbyGridCorner.TopLeft, new Vector2Int(4, 0));
            layout.TryPlace(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 6));
            layout.TryPlace(LobbyWidgetId.JumpBackIn6x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(1, pushed.Count, "The widget beside, the one further below and the ones on other grids stay");
            Assert.AreEqual(new Vector2Int(0, 3), PushedCell(LobbyWidgetId.LiveEvents3x3));
        }

        [Test]
        public void ResolveAFreeCellWithoutPushes()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 3), pushed);

            //Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(0, pushed.Count);
        }

        [Test]
        public void FailToPushAWidgetOffTheGrid()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 3));

            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 2), pushed);

            //Assert
            Assert.IsFalse(resolved);
            Assert.AreEqual(0, pushed.Count);
        }

        [Test]
        public void FailToPushFromOutsideTheGrid()
        {
            //Act
            bool resolved = layout.TryResolvePush(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(1, 0), pushed);

            //Assert
            Assert.IsFalse(resolved);
        }

        [Test]
        public void MoveTheWidgetsInTheWayWhenPlacingWithAPush()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 3));

            //Act
            bool placed = layout.TryPlacePushing(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Assert
            Assert.IsTrue(placed);
            Assert.AreEqual(2, layout.Placements.Count);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4x2, out LobbyWidgetPlacement friends));
            Assert.AreEqual(new Vector2Int(0, 0), friends.Cell);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement landing));
            Assert.AreEqual(new Vector2Int(0, 2), landing.Cell, "The pushed widget takes the cells the moved one leaves");
        }

        [Test]
        public void PushWhenMovingAWidgetInFromAnotherGrid()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopRight, new Vector2Int(1, 0));

            //Act
            bool placed = layout.TryPlacePushing(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));

            //Assert
            Assert.IsTrue(placed);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.LiveEvents3x3, out LobbyWidgetPlacement liveEvents));
            Assert.AreEqual(LobbyGridCorner.TopLeft, liveEvents.Corner);
            Assert.AreEqual(new Vector2Int(0, 0), liveEvents.Cell);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement landing));
            Assert.AreEqual(new Vector2Int(0, 3), landing.Cell);
        }

        [Test]
        public void KeepTheLayoutWhenAPushFails()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 0));
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 3));

            //Act
            bool placed = layout.TryPlacePushing(LobbyWidgetId.FriendsOnline4x2, LobbyGridCorner.TopLeft, new Vector2Int(0, 2));

            //Assert
            Assert.IsFalse(placed);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4x2, out LobbyWidgetPlacement friends));
            Assert.AreEqual(new Vector2Int(0, 0), friends.Cell);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement landing));
            Assert.AreEqual(new Vector2Int(0, 3), landing.Cell);
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
            bool found = layout.TryFindSnapCell(LobbyGridCorner.BottomLeft, LobbyWidgetId.LiveEvents3x3.Span(), Vector2.zero, out _);

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
            Assert.AreEqual(6, layout.Placements.Count);
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.Landing4x3, out LobbyWidgetPlacement landing));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.FriendsOnline4x2, out LobbyWidgetPlacement friends));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.LiveEvents3x3, out LobbyWidgetPlacement liveEvents));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.UpcomingEvents3x2, out LobbyWidgetPlacement upcomingEvents));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.JumpBackIn6x2, out LobbyWidgetPlacement recent));
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.RecommendedPlaces6x2, out LobbyWidgetPlacement featured));
            Assert.AreEqual(LobbyGridCorner.TopLeft, landing.Corner);
            Assert.AreEqual(LobbyGridCorner.TopLeft, friends.Corner);
            Assert.AreEqual(LobbyGridCorner.TopRight, liveEvents.Corner);
            Assert.AreEqual(LobbyGridCorner.TopRight, upcomingEvents.Corner);
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

        private Vector2Int PushedCell(LobbyWidgetId widgetId)
        {
            for (var i = 0; i < pushed.Count; i++)
                if (pushed[i].WidgetId == widgetId)
                    return pushed[i].Cell;

            throw new AssertionException($"{widgetId} was not pushed");
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
