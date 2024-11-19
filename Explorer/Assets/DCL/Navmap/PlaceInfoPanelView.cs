using DCL.AssetsProvision;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class PlaceInfoPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public ImageView Thumbnail { get; private set; }

        [field: SerializeField]
        public Button JumpInButton { get; private set; }

        [field: SerializeField]
        public Button StartNavigationButton { get; private set; }

        [field: SerializeField]
        public Button StopNavigationButton { get; private set; }

        [field: SerializeField]
        public MultiStateButtonView LikeButton { get; private set; }

        [field: SerializeField]
        public MultiStateButtonView DislikeButton { get; private set; }

        [field: SerializeField]
        public MultiStateButtonView FavoriteButton { get; private set; }

        [field: SerializeField]
        public Button ShareButton { get; private set; }

        [field: SerializeField]
        public RectTransform SharePivot { get; private set; }

        [field: SerializeField]
        public Button SetAsHomeButton { get; private set; }

        [field: SerializeField]
        public GameObject LiveEventContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text LiveEventNameLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text PlaceNameLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text CreatorNameLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text LikeRateLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text PlayerCountLabel { get; private set; }

        [field: SerializeField]
        public EventElementAssetReference EventElementViewRef { get; private set; }

        [field: SerializeField]
        public RectTransform TabsLayoutRoot { get; private set; }

        [field: Header("Overview Tab")]
        [field: SerializeField]
        public Button OverviewTabButton { get; private set; }

        [field: SerializeField]
        public GameObject OverviewTabContainer { get; private set; }

        [field: SerializeField]
        public GameObject OverviewTabSelected { get; private set; }

        [field: SerializeField]
        public TMP_Text DescriptionLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text CoordinatesLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text ParcelCountLabel { get; private set; }

        [field: SerializeField]
        public GameObject AppearsOnContainer { get; private set; }

        [field: SerializeField]
        public AppearsOnCategory[] AppearsOnCategories { get; private set; }

        [field: Header("Photos Tab")]
        [field: SerializeField]
        public Button PhotosTabButton { get; private set; }

        [field: SerializeField]
        public GameObject PhotosTabContainer { get; private set; }

        [field: SerializeField]
        public GameObject PhotosTabSelected { get; private set; }

        [field: Header("Events Tab")]
        [field: SerializeField]
        public Button EventsTabButton { get; private set; }

        [field: SerializeField]
        public GameObject EventsTabContainer { get; private set; }

        [field: SerializeField]
        public GameObject EventsTabSelected { get; private set; }

        [Serializable]
        public struct AppearsOnCategory
        {
            public string category;
            public GameObject container;
        }

        [Serializable]
        public class EventElementAssetReference : ComponentReference<EventElementView>
        {
            public EventElementAssetReference(string guid) : base(guid) { }
        }
    }
}
