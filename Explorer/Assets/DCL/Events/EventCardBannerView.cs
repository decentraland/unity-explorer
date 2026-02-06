using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Communities.EventInfo;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.Events
{
    public class EventCardBannerView : EventCardView
    {
        private const string HOST_FORMAT_FOR_BANNER = "Organized by <b>{0}</b>";

        [Header("Specific-card references")]
        [SerializeField] private GameObject eventDateContainer = null!;
        [SerializeField] private TMP_Text eventDay = null!;

        public override void Configure(EventDTO eventInfo, ThumbnailLoader thumbnailLoader, PlacesData.PlaceInfo? placeInfo = null,
            List<Profile.CompactInfo>? friends = null, ProfileRepositoryWrapper? profileRepositoryWrapper = null, GetUserCommunitiesData.CommunityData? communityInfo = null)
        {
            base.Configure(eventInfo, thumbnailLoader, placeInfo, friends, profileRepositoryWrapper, communityInfo);

            hostName.text = string.Format(HOST_FORMAT_FOR_BANNER, eventInfo.user_name);
            eventDateContainer.SetActive(!eventInfo.live);
            eventDay.text = EventUtilities.GetEventDayText(eventInfo);
        }

        protected override void LoadThumbnail(EventDTO eventInfo, ThumbnailLoader thumbnailLoader)
        {
            loadingThumbnailCts = loadingThumbnailCts.SafeRestart();
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(
                !string.IsNullOrEmpty(eventInfo.image_vertical) ? eventInfo.image_vertical : eventInfo.image,
                eventThumbnail, null, loadingThumbnailCts.Token, true).Forget();
        }

        protected override void PlayHoverAnimation() { }

        protected override void PlayHoverExitAnimation(bool instant = false) { }
    }
}
