using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Places
{
    public class PlacesView : MonoBehaviour
    {
        public Action<PlacesSections?, PlacesSections>? SectionChanged;

        public DiscoverSectionView DiscoverView => discoverSectionView;

        private PlacesSections? currentSection;

        [Header("Sections")]
        [SerializeField] private ButtonWithSelectableStateView discoverSectionTab = null!;
        [SerializeField] private DiscoverSectionView discoverSectionView = null!;
        [SerializeField] private ButtonWithSelectableStateView favoritesSectionTab = null!;
        [SerializeField] private GameObject favoritesSectionView = null!;
        [SerializeField] private ButtonWithSelectableStateView recentlyVisitedSectionTab = null!;
        [SerializeField] private GameObject recentlyVisitedSectionView = null!;
        [SerializeField] private ButtonWithSelectableStateView myPlacesSectionTab = null!;
        [SerializeField] private GameObject myPlacesSectionView = null!;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator = null!;
        [SerializeField] private Animator headerAnimator = null!;

        private void Awake()
        {
            discoverSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSections.DISCOVER));
            favoritesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSections.FAVORITES));
            recentlyVisitedSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSections.RECENTLY_VISITED));
            myPlacesSectionTab.Button.onClick.AddListener(() => OpenSection(PlacesSections.MY_PLACES));
        }

        private void OnDestroy()
        {
            discoverSectionTab.Button.onClick.RemoveAllListeners();
            favoritesSectionTab.Button.onClick.RemoveAllListeners();
            recentlyVisitedSectionTab.Button.onClick.RemoveAllListeners();
            myPlacesSectionTab.Button.onClick.RemoveAllListeners();
        }

        public void SetViewActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }

        public void OpenSection(PlacesSections section, bool force = false)
        {
            if (currentSection == section && !force)
                return;

            discoverSectionTab.SetSelected(false);
            discoverSectionView.SetActive(false);
            favoritesSectionTab.SetSelected(false);
            favoritesSectionView.SetActive(false);
            recentlyVisitedSectionTab.SetSelected(false);
            recentlyVisitedSectionView.SetActive(false);
            myPlacesSectionTab.SetSelected(false);
            myPlacesSectionView.SetActive(false);

            switch (section)
            {
                case PlacesSections.DISCOVER:
                    discoverSectionTab.SetSelected(true);
                    discoverSectionView.SetActive(true);
                    break;
                case PlacesSections.FAVORITES:
                    favoritesSectionTab.SetSelected(true);
                    favoritesSectionView.SetActive(true);
                    break;
                case PlacesSections.RECENTLY_VISITED:
                    recentlyVisitedSectionTab.SetSelected(true);
                    recentlyVisitedSectionView.SetActive(true);
                    break;
                case PlacesSections.MY_PLACES:
                    myPlacesSectionTab.SetSelected(true);
                    myPlacesSectionView.SetActive(true);
                    break;
            }

            SectionChanged?.Invoke(currentSection, section);
            currentSection = section;
        }
    }
}
