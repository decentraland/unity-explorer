using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.WebRequests;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesResponse.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunityResultCardView : MonoBehaviour
    {
        public event Action<string> OnMainButtonClicked;
        public event Action<string> OnViewCommunityButtonClicked;
        public event Action<int, CommunityResultCardView> OnJoinCommunityButtonClicked;

        private const string PUBLIC_PRIVACY_TEXT = "Public";
        private const string PRIVATE_PRIVACY_TEXT = "Private";
        private const string MEMBERS_COUNTER_FORMAT = "{0} members";

        [SerializeField] private TMP_Text communityTitle;
        [SerializeField] private ImageView communityThumbnail;
        [SerializeField] private Image communityPrivacyIcon;
        [SerializeField] private Sprite publicPrivacySprite;
        [SerializeField] private Sprite privatePrivacySprite;
        [SerializeField] private TMP_Text communityPrivacyText;
        [SerializeField] private TMP_Text communityMembersCountText;
        [SerializeField] private GameObject communityLiveMark;
        [SerializeField] private Button mainButton;
        [SerializeField] private GameObject buttonsContainer;
        [SerializeField] private Button viewCommunityButton;
        [SerializeField] private Button joinCommunityButton;
        [SerializeField] private GameObject joiningLoading;
        [SerializeField] private MutualFriendsConfig mutualFriends;

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
        private string currentCommunityId;
        private int currentIndex;

        private void Awake()
        {
            mainButton.onClick.AddListener(() => OnMainButtonClicked?.Invoke(currentCommunityId));
            viewCommunityButton.onClick.AddListener(() => OnViewCommunityButtonClicked?.Invoke(currentCommunityId));
            joinCommunityButton.onClick.AddListener(() => OnJoinCommunityButtonClicked?.Invoke(currentIndex, this));
        }

        private void OnDestroy()
        {
            mainButton.onClick.RemoveAllListeners();
            viewCommunityButton.onClick.RemoveAllListeners();
            joinCommunityButton.onClick.RemoveAllListeners();
        }

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

        public void SetCommunityId(string id) =>
            currentCommunityId = id;

        public void SetIndex(int index) =>
            currentIndex = index;

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

            communityMembersCountText.text = string.Format(MEMBERS_COUNTER_FORMAT, formattedCount);
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

        public void SetupMutualFriends(ViewDependencies viewDependencies, CommunityData communityData)
        {
            foreach (MutualFriendsConfig.MutualThumbnail thumbnail in mutualFriends.thumbnails)
                thumbnail.picture.InjectDependencies(viewDependencies);

            for (var i = 0; i < mutualFriends.thumbnails.Length; i++)
            {
                bool friendExists = i < communityData.friends.Length;
                mutualFriends.thumbnails[i].root.SetActive(friendExists);
                if (!friendExists) continue;
                GetUserCommunitiesResponse.FriendInCommunity mutualFriend = communityData.friends[i];
                mutualFriends.thumbnails[i].picture.Setup(ProfileNameColorHelper.GetNameColor(mutualFriend.name), mutualFriend.profilePictureUrl, mutualFriend.id);
            }
        }
    }
}
