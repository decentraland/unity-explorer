using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.WebRequests;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunityResultCardView : MonoBehaviour
    {
        private const string PUBLIC_PRIVACY_TEXT = "Public";
        private const string PRIVATE_PRIVACY_TEXT = "Private";

        [field: SerializeField] internal TMP_Text communityTitle { get; private set; }
        [field: SerializeField] internal ImageView communityThumbnail { get; private set; }
        [field: SerializeField] internal Image communityPrivacyIcon { get; private set; }
        [field: SerializeField] internal Sprite publicPrivacySprite { get; private set; }
        [field: SerializeField] internal Sprite privatePrivacySprite { get; private set; }
        [field: SerializeField] internal TMP_Text communityPrivacyText { get; private set; }
        [field: SerializeField] internal TMP_Text communityMembersCountText { get; private set; }
        [field: SerializeField] internal GameObject communityLiveMark { get; private set; }
        [field: SerializeField] internal Button mainButton { get; private set; }
        [field: SerializeField] internal GameObject buttonsContainer { get; private set; }
        [field: SerializeField] internal Button viewCommunityButton { get; private set; }
        [field: SerializeField] internal Button joinCommunityButton { get; private set; }
        [field: SerializeField] internal GameObject joiningLoading { get; private set; }
        [field: SerializeField] internal MutualFriendsConfig mutualFriends { get; private set; }

        [Serializable]
        internal struct MutualFriendsConfig
        {
            public MutualThumbnail[] thumbnails;

            [Serializable]
            public struct MutualThumbnail
            {
                public GameObject root;
                public ProfilePictureView picture;
            }
        }

        private ImageController imageController;

        public void ConfigureImageController(IWebRequestController webRequestController)
        {
            if (imageController != null)
                return;

            imageController = new ImageController(communityThumbnail, webRequestController);
        }

        public void SetCommunityThumbnail(string imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl))
                imageController?.RequestImage(imageUrl, hideImageWhileLoading: true);
        }

        public void SetTitle(string title) =>
            communityTitle.text = title;

        public void SetPrivacy(CommunityPrivacy privacy)
        {
            communityPrivacyIcon.sprite = privacy == CommunityPrivacy.@public ? publicPrivacySprite : privatePrivacySprite;
            communityPrivacyText.text = privacy == CommunityPrivacy.@public ? PUBLIC_PRIVACY_TEXT : PRIVATE_PRIVACY_TEXT;
        }

        public void SetMembersCount(int memberCount)
        {
            string formattedCount;

            if (memberCount >= 1000)
            {
                float countInK = memberCount / 1000f;
                formattedCount = $"{countInK:0.#}k";
            }
            else
                formattedCount = memberCount.ToString();

            communityMembersCountText.text = $"{formattedCount} members";
        }

        public void SetOwnership(bool isMember)
        {
            joinCommunityButton.gameObject.SetActive(!isMember);
            viewCommunityButton.gameObject.SetActive(isMember);
        }

        public void SetLiveMarkAsActive(bool isLiveMark) =>
            communityLiveMark.SetActive(isLiveMark);

        public void SetJoiningLoadingActive(bool isActive)
        {
            joiningLoading.SetActive(isActive);
            buttonsContainer.SetActive(!isActive);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            foreach (MutualFriendsConfig.MutualThumbnail thumbnail in mutualFriends.thumbnails)
                thumbnail.picture.InjectDependencies(dependencies);
        }
    }
}
