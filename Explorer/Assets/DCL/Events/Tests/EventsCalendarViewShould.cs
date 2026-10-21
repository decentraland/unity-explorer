using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Events.Tests
{
    public class EventsCalendarViewShould
    {
        private const string PREFAB_PATH = "Assets/DCL/Events/Prefabs/EventsCalendar.prefab";

        private GameObject instance = null!;
        private EventsCalendarView view = null!;
        private int authoredDays;

        [SetUp]
        public void SetUp()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH));
            view = instance.GetComponentInChildren<EventsCalendarView>(true);
            authoredDays = view.GetComponentsInChildren<EventsDaySelectorButton>(true).Length;
        }

        [TearDown]
        public void TearDown() =>
            Object.DestroyImmediate(instance);

        [Test]
        public void ShowEveryAuthoredDayInTheExplorePanel()
        {
            // Act
            view.SetCompactLayout(false);
            view.SetHighlightedCarousel(null);

            // Assert
            Assert.AreEqual(authoredDays, view.MaxDays);
            Assert.AreEqual(authoredDays, ActiveDayButtons());
        }

        [Test]
        public void DropTheLastDayColumnInTheCompactLayout()
        {
            // Act
            view.SetCompactLayout(true);
            view.SetHighlightedCarousel(null);

            // Assert
            Assert.AreEqual(authoredDays - 1, view.MaxDays);
            Assert.AreEqual(authoredDays - 1, ActiveDayButtons());
        }

        [Test]
        public void RestoreTheDayColumnWhenTheLayoutIsNoLongerCompact()
        {
            // Arrange
            view.SetCompactLayout(true);
            view.SetHighlightedCarousel(null);

            // Act
            view.SetCompactLayout(false);
            view.SetHighlightedCarousel(null);

            // Assert
            Assert.AreEqual(authoredDays, ActiveDayButtons());
        }

        private int ActiveDayButtons() =>
            view.GetComponentsInChildren<EventsDaySelectorButton>(false).Length;
    }
}
