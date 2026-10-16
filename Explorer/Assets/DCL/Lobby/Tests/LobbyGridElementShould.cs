using NUnit.Framework;
using UnityEngine;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyGridElementShould
    {
        private LobbyGridElement grid = null!;

        [SetUp]
        public void SetUp()
        {
            grid = new LobbyGridElement { Corner = LobbyGridCorner.TopLeft, Columns = 4, Rows = 6 };
        }

        [Test]
        public void SizeItselfFromItsCells()
        {
            //Assert
            Assert.AreEqual(new Vector2Int(4, 6), grid.Size);
            Assert.AreEqual((4 * LobbyGridElement.CELL_SIZE) + (3 * LobbyGridElement.GUTTER), grid.style.width.value.value);
            Assert.AreEqual((6 * LobbyGridElement.CELL_SIZE) + (5 * LobbyGridElement.GUTTER), grid.style.height.value.value);
        }

        [Test]
        public void ResizeWhenItsCellsChange()
        {
            //Act
            grid.Columns = 6;
            grid.Rows = 2;

            //Assert
            Assert.AreEqual((6 * LobbyGridElement.CELL_SIZE) + (5 * LobbyGridElement.GUTTER), grid.style.width.value.value);
            Assert.AreEqual((2 * LobbyGridElement.CELL_SIZE) + LobbyGridElement.GUTTER, grid.style.height.value.value);
        }

        [Test]
        public void KeepAtLeastOneCell()
        {
            //Act
            grid.Columns = 0;
            grid.Rows = -3;

            //Assert
            Assert.AreEqual(new Vector2Int(1, 1), grid.Size);
            Assert.AreEqual(LobbyGridElement.CELL_SIZE, grid.style.width.value.value);
            Assert.AreEqual(LobbyGridElement.CELL_SIZE, grid.style.height.value.value);
        }

        [Test]
        public void SizeASpanWithTheGuttersBetweenItsCells()
        {
            //Act
            Vector2 size = LobbyGridElement.SpanToSize(new Vector2Int(3, 4));

            //Assert
            Assert.AreEqual((3 * LobbyGridElement.CELL_SIZE) + (2 * LobbyGridElement.GUTTER), size.x);
            Assert.AreEqual((4 * LobbyGridElement.CELL_SIZE) + (3 * LobbyGridElement.GUTTER), size.y);
        }

        [Test]
        public void ConvertACellToItsLocalPosition()
        {
            //Act
            Vector2 local = LobbyGridElement.CellToLocal(new Vector2Int(1, 2));

            //Assert
            Assert.AreEqual(LobbyGridElement.CellPitch.x, local.x);
            Assert.AreEqual(2 * LobbyGridElement.CellPitch.y, local.y);
        }

        [Test]
        public void ConvertALocalPositionToAFractionalCell()
        {
            //Act
            Vector2 exact = LobbyGridElement.LocalToCell(new Vector2(LobbyGridElement.CellPitch.x, 2 * LobbyGridElement.CellPitch.y));
            Vector2 half = LobbyGridElement.LocalToCell(LobbyGridElement.CellPitch * 0.5f);

            //Assert
            Assert.AreEqual(new Vector2(1f, 2f), exact);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), half);
        }

        [Test]
        public void RoundALocalPositionToTheNearestCell()
        {
            //Act
            Vector2Int down = LobbyGridElement.NearestCell(new Vector2(LobbyGridElement.CellPitch.x * 1.4f, LobbyGridElement.CellPitch.y * 2.4f));
            Vector2Int up = LobbyGridElement.NearestCell(new Vector2(LobbyGridElement.CellPitch.x * 1.6f, LobbyGridElement.CellPitch.y * 2.6f));
            Vector2Int outside = LobbyGridElement.NearestCell(new Vector2(-LobbyGridElement.CellPitch.x * 0.8f, 0f));

            //Assert
            Assert.AreEqual(new Vector2Int(1, 2), down);
            Assert.AreEqual(new Vector2Int(2, 3), up);
            Assert.AreEqual(new Vector2Int(-1, 0), outside);
        }

        [Test]
        public void ToggleTheHighlight()
        {
            //Act
            grid.SetHighlighted(true);
            bool highlighted = grid.ClassListContains("lobby-grid--highlighted");
            grid.SetHighlighted(false);

            //Assert
            Assert.IsTrue(highlighted);
            Assert.IsFalse(grid.ClassListContains("lobby-grid--highlighted"));
        }

        [Test]
        public void ShowTheAnchorDotsOnlyWhileEditing()
        {
            //Assert
            Assert.IsFalse(grid.IsEditing);
            Assert.IsFalse(grid.ClassListContains("lobby-grid--editing"));

            //Act
            grid.SetEditing(true);

            //Assert
            Assert.IsTrue(grid.IsEditing);
            Assert.IsTrue(grid.ClassListContains("lobby-grid--editing"));

            //Act
            grid.SetEditing(false);

            //Assert
            Assert.IsFalse(grid.IsEditing);
            Assert.IsFalse(grid.ClassListContains("lobby-grid--editing"));
        }

        [Test]
        public void TintTheDotsOfTheHintedCellsOnlyWhileEditing()
        {
            //Arrange
            grid.SetEditing(true);

            //Act
            grid.SetDropHint(new Vector2Int(1, 1), new Vector2Int(2, 1), valid: true);

            //Assert
            Assert.IsTrue(grid.HasDropHint);

            //Act
            grid.SetEditing(false);

            //Assert
            Assert.IsFalse(grid.HasDropHint, "Leaving edit mode drops the hint");
        }

        [Test]
        public void ClearTheDropHint()
        {
            //Arrange
            grid.SetEditing(true);
            grid.SetDropHint(Vector2Int.zero, new Vector2Int(3, 3), valid: false);

            //Act
            grid.ClearDropHint();

            //Assert
            Assert.IsFalse(grid.HasDropHint);
        }
    }
}
