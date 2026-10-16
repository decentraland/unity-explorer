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
        private static readonly Vector2Int BOTTOM_GRID = new (7, 2);

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
            layout.TryPlace(LobbyWidgetId.LiveEvents3By3, LobbyGridCorner.TopLeft, new Vector2Int(0, 1));
            layout.TryPlace(LobbyWidgetId.Landing4By3, LobbyGridCorner.TopRight, new Vector2Int(0, 2));
            layout.TryPlace(LobbyWidgetId.RecommendedPlaces7By2, LobbyGridCorner.BottomLeft, new Vector2Int(0, 0));
            var loaded = new LobbyLayout(TOP_GRID, TOP_GRID, BOTTOM_GRID, BOTTOM_GRID);

            //Act
            LobbyLayoutStorage.Save(layout);
            LobbyLayoutStorage.Load(loaded);

            //Assert
            AssertSamePlacements(layout, loaded);
        }

        [Test]
        public void PutTheRequiredWidgetBackIntoAStoredLayoutWithoutIt()
        {
            //Arrange
            layout.Clear();
            LobbyLayoutStorage.Save(layout);
            layout.ResetToDefault();

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            Assert.AreEqual(1, layout.Placements.Count, "Only the required widget is put back");
            Assert.IsTrue(layout.TryGetPlacement(LobbyLayout.REQUIRED_WIDGET, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.TopLeft, placement.Corner);
            Assert.AreEqual(Vector2Int.zero, placement.Cell);
        }

        [Test]
        public void SkipUnknownWidgetsAndCorners()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOBBY_LAYOUT,
                "{\"Version\":1,\"Entries\":[" +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"Landing9By9\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"Middle\",\"Widget\":\"LiveEvents3By3\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"7\",\"Widget\":\"Landing4By3\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"42\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"BottomRight\",\"Widget\":\"JumpBackIn7By2\",\"X\":0,\"Y\":0}]}");

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            Assert.AreEqual(2, layout.Placements.Count, "The known entry and the required widget put back");
            Assert.IsTrue(layout.TryGetPlacement(LobbyWidgetId.JumpBackIn7By2, out LobbyWidgetPlacement placement));
            Assert.AreEqual(LobbyGridCorner.BottomRight, placement.Corner);
            Assert.IsTrue(layout.TryGetPlacement(LobbyLayout.REQUIRED_WIDGET, out _));
        }

        [Test]
        public void SkipEntriesThatDoNotFitTheGrid()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOBBY_LAYOUT,
                "{\"Version\":1,\"Entries\":[" +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"Landing4By3\",\"X\":0,\"Y\":0}," +
                "{\"Corner\":\"TopLeft\",\"Widget\":\"FriendsOnline4By2\",\"X\":0,\"Y\":2}," +
                "{\"Corner\":\"BottomLeft\",\"Widget\":\"LiveEvents3By3\",\"X\":0,\"Y\":0}]}");

            //Act
            LobbyLayoutStorage.Load(layout);

            //Assert
            Assert.AreEqual(1, layout.Placements.Count);
            Assert.AreEqual(LobbyWidgetId.Landing4By3, layout.Placements[0].WidgetId);
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
            layout.TryPlace(LobbyWidgetId.LiveEvents3By3, LobbyGridCorner.TopRight, new Vector2Int(1, 0));

            //Act
            LobbyLayoutStorage.Save(layout);

            //Assert
            string json = DCLPlayerPrefs.GetString(DCLPrefKeys.LOBBY_LAYOUT);
            StringAssert.Contains("\"Widget\":\"LiveEvents3By3\"", json);
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
