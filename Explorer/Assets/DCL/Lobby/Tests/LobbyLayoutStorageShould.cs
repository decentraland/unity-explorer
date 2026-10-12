using DCL.Prefs;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace DCL.Lobby.Tests
{
    [TestFixture]
    public class LobbyLayoutStorageShould
    {
        private static readonly Vector2Int TOP_GRID = new (4, 6);
        private static readonly Vector2Int BOTTOM_GRID = new (6, 2);

        private static readonly FieldInfo PREFS_FIELD = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private LobbyLayout layout = null!;

        [SetUp]
        public void SetUp()
        {
            PREFS_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());
            layout = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
        }

        [TearDown]
        public void TearDown()
        {
            PREFS_FIELD.SetValue(null, null);
        }

        [Test]
        public void UseTheDefaultLayoutWhenNothingIsStored()
        {
            //Arrange
            var expected = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            expected.ResetToDefault();

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            AssertSamePlacements(expected, layout);
        }

        [Test]
        public void RoundTripThePlacements()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopLeft, new Vector2Int(0, 1));
            layout.TryPlace(LobbyWidgetId.Landing4x3, LobbyGridCorner.TopRight, new Vector2Int(0, 2));
            layout.TryPlace(LobbyWidgetId.RecommendedPlaces6x2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));
            var loaded = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);

            //Act
            LobbyLayoutStorage.Save(layout);
            LobbyLayoutStorage.Load(loaded);

            //Assert
            AssertSamePlacements(layout, loaded);
        }

        [Test]
        public void KeepAnEmptyLayoutEmpty()
        {
            //Arrange
            layout.Clear();
            LobbyLayoutStorage.Save(layout);
            layout.ResetToDefault();

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            Assert.AreEqual(0, layout.Placements.Count);
        }

        [Test]
        public void SkipUnknownWidgetsAndCorners()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOBBY_LAYOUT,
                "{\"Version\":1,\"Entries\":[" +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"Landing9x9\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"Middle\",\"Widget\":\"LiveEvents3x3\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"7\",\"Widget\":\"Landing4x3\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"42\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"BottomRight\",\"Widget\":\"JumpBackIn6x2\",\"X\":0,\"Y\":0}]}");

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            Assert.AreEqual(1, layout.Placements.Count);
            Assert.AreEqual(LobbyWidgetId.JumpBackIn6x2, layout.Placements[0].WidgetId);
            Assert.AreEqual(LobbyGridCorner.BottomRight, layout.Placements[0].Corner);
        }

        [Test]
        public void SkipEntriesThatDoNotFitTheGrid()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOBBY_LAYOUT,
                "{\"Version\":1,\"Entries\":[" +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"Landing4x3\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"FriendsOnline4x2\",\"X\":0,\"Y\":2}," +
                "{\"Corner\":\"BottomLeft\",\"Widget\":\"LiveEvents3x3\",\"X\":0,\"Y\":0}]}");

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            Assert.AreEqual(1, layout.Placements.Count);
            Assert.AreEqual(LobbyWidgetId.Landing4x3, layout.Placements[0].WidgetId);
        }

        [Test]
        public void FallBackToTheDefaultOnMalformedJson()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOBBY_LAYOUT, "{\"Version\":1,\"Entries\":[{");
            var expected = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            expected.ResetToDefault();

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            AssertSamePlacements(expected, layout);
        }

        [Test]
        public void FallBackToTheDefaultOnAnotherVersion()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOBBY_LAYOUT, "{\"Version\":0,\"Entries\":[]}");
            var expected = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);
            expected.ResetToDefault();

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            AssertSamePlacements(expected, layout);
        }

        [Test]
        public void StoreWidgetsAndCornersByName()
        {
            //Arrange
            layout.TryPlace(LobbyWidgetId.LiveEvents3x3, LobbyGridCorner.TopRight, new Vector2Int(1, 0));

            //Act
            LobbyLayoutStorage.Save(layout);

            //Assert
            string json = DCLPlayerPrefs.GetString(DCLPrefKeys.LOBBY_LAYOUT);
            StringAssert.Contains("\"Widget\":\"LiveEvents3x3\"", json);
            StringAssert.Contains("\"Corner\":\"TopRight\"", json);
        }

        private static void AssertSamePlacements(LobbyLayout expected, LobbyLayout actual)
        {
            Assert.AreEqual(expected.Placements.Count, actual.Placements.Count);

            foreach (LobbyWidgetPlacement placement in expected.Placements)
            {
                Assert.IsTrue(actual.TryGetPlacement(placement.WidgetId, out LobbyWidgetPlacement actualPlacement), placement.WidgetId.ToString());
                Assert.AreEqual(placement.Corner, actualPlacement.Corner, placement.WidgetId.ToString());
                Assert.AreEqual(placement.Cell, actualPlacement.Cell, placement.WidgetId.ToString());
            }
        }
    }
}
