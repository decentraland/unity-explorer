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
            Assert.AreEqual(4 * LobbyGridElement.CELL_WIDTH + 3 * LobbyGridElement.GUTTER, grid.style.width.value.value);
            Assert.AreEqual(6 * LobbyGridElement.CELL_HEIGHT + 5 * LobbyGridElement.GUTTER, grid.style.height.value.value);
        }

        [Test]
        public void ResizeWhenItsCellsChange()
        {
            //Act
            grid.Columns = 6;
            grid.Rows = 2;

            //Assert
            Assert.AreEqual(6 * LobbyGridElement.CELL_WIDTH + 5 * LobbyGridElement.GUTTER, grid.style.width.value.value);
            Assert.AreEqual(2 * LobbyGridElement.CELL_HEIGHT + LobbyGridElement.GUTTER, grid.style.height.value.value);
        }

        [Test]
        public void KeepAtLeastOneCell()
        {
            //Act
            grid.Columns = 0;
            grid.Rows = -3;

            //Assert
            Assert.AreEqual(new Vector2Int(1, 1), grid.Size);
            Assert.AreEqual(LobbyGridElement.CELL_WIDTH, grid.style.width.value.value);
            Assert.AreEqual(LobbyGridElement.CELL_HEIGHT, grid.style.height.value.value);
        }

        [Test]
        public void SizeASpanWithTheGuttersBetweenItsCells()
        {
            //Act
            Vector2 size = LobbyGridElement.SpanToSize(new Vector2Int(3, 4));

            //Assert
            Assert.AreEqual(3 * LobbyGridElement.CELL_WIDTH + 2 * LobbyGridElement.GUTTER, size.x);
            Assert.AreEqual(4 * LobbyGridElement.CELL_HEIGHT + 3 * LobbyGridElement.GUTTER, size.y);
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
        public void ShowTheCellLinesOnlyWhileEditing()
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
    }
}
