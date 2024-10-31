using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class SearchFiltersView : MonoBehaviour
    {
        [field: SerializeField]
        public Button AllButton { get; private set; }

        [field: SerializeField]
        public GameObject AllSelectedMarker { get; private set; }

        [field: SerializeField]
        public Button FavoritesButton { get; private set; }

        [field: SerializeField]
        public GameObject FavoritesSelectedMarker { get; private set; }

        [field: SerializeField]
        public Button VisitedButton { get; private set; }

        [field: SerializeField]
        public GameObject VisitedSelectedMarker { get; private set; }

        [field: SerializeField]
        public Button MostActiveButton { get; private set; }

        [field: SerializeField]
        public TMP_Text MostActiveText { get; private set; }

        [field: SerializeField]
        public Button BestRatedButton { get; private set; }

        [field: SerializeField]
        public TMP_Text BestRatedText { get; private set; }

        [field: SerializeField]
        public Button NewestButton { get; private set; }

        [field: SerializeField]
        public TMP_Text NewestText { get; private set; }

        [SerializeField] private Color activeSortingBackgroundColor;
        [SerializeField] private Color activeSortingTextColor;
        [SerializeField] private Color inactiveSortingBackgroundColor;
        [SerializeField] private Color inactiveSortingTextColor;

        public void Toggle(NavmapSearchPlaceFilter filter)
        {
            AllSelectedMarker.SetActive(filter == NavmapSearchPlaceFilter.All);
            FavoritesSelectedMarker.SetActive(filter == NavmapSearchPlaceFilter.Favorites);
            VisitedSelectedMarker.SetActive(filter == NavmapSearchPlaceFilter.Visited);
        }

        public void Toggle(NavmapSearchPlaceSorting sorting)
        {
            MostActiveButton.targetGraphic.color = sorting == NavmapSearchPlaceSorting.MostActive
                ? activeSortingBackgroundColor
                : inactiveSortingBackgroundColor;

            MostActiveText.color = sorting == NavmapSearchPlaceSorting.MostActive
                ? activeSortingTextColor
                : inactiveSortingTextColor;

            NewestButton.targetGraphic.color = sorting == NavmapSearchPlaceSorting.Newest
                ? activeSortingBackgroundColor
                : inactiveSortingBackgroundColor;

            NewestText.color = sorting == NavmapSearchPlaceSorting.Newest
                ? activeSortingTextColor
                : inactiveSortingTextColor;

            BestRatedButton.targetGraphic.color = sorting == NavmapSearchPlaceSorting.BestRated
                ? activeSortingBackgroundColor
                : inactiveSortingBackgroundColor;

            BestRatedText.color = sorting == NavmapSearchPlaceSorting.BestRated
                ? activeSortingTextColor
                : inactiveSortingTextColor;
        }
    }
}
