using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.Profiles.Helpers;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunityResultCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Tweener? descriptionTween;
        private const float HOVER_ANIMATION_DURATION = 0.3f;
        private const float HOVER_ANIMATION_HEIGHT_TO_APPLY = 45f;

        public event Action<string>? MainButtonClicked;
        public event Action<string>? ViewCommunityButtonClicked;
        public event Action<string, CommunityResultCardView>? JoinCommunityButtonClicked;

        private const string PUBLIC_PRIVACY_TEXT = "Public";
        private const string PRIVATE_PRIVACY_TEXT = "Private";
        private const string MEMBERS_COUNTER_FORMAT = "{0} Members";

        [SerializeField] private RectTransform headerContainer = null!;
        [SerializeField] private RectTransform footerContainer = null!;
        [SerializeField] private TMP_Text communityTitle = null!;
        [SerializeField] private TMP_Text communityOwner = null!;
        [SerializeField] private TMP_Text communityDescription = null!;
        [SerializeField] private CanvasGroup communityDescriptionCanvasGroup = null!;
        [field: SerializeField] public ImageView communityThumbnail = null!;
        [SerializeField] private Image communityPrivacyIcon = null!;
        [SerializeField] private Sprite publicPrivacySprite = null!;
        [SerializeField] private Sprite privatePrivacySprite = null!;
        [SerializeField] private TMP_Text communityPrivacyText = null!;
        [SerializeField] private GameObject communityMembersSeparator = null!;
        [SerializeField] private TMP_Text communityMembersCountText = null!;
        [SerializeField] private GameObject communityLiveMark = null!;
        [SerializeField] private Button mainButton = null!;
        [SerializeField] private GameObject buttonsContainer = null!;
        [SerializeField] private Button viewCommunityButton = null!;
        [SerializeField] private Button joinCommunityButton = null!;
        [SerializeField] private GameObject joiningLoading = null!;
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
                public ProfileNameTooltipView profileNameTooltip;

                internal bool isPointerEventsSubscribed;
            }
        }

        private string currentCommunityId;
        private Tweener? headerTween;
        private Tweener? footerTween;
        private Vector2 originalHeaderSizeDelta;
        private Vector2 originalFooterSizeDelta;

        private void Awake()
        {
            mainButton.onClick.AddListener(() =>
            {
                if (currentCommunityId != null)
                    MainButtonClicked?.Invoke(currentCommunityId);
            });

            viewCommunityButton.onClick.AddListener(() =>
            {
                if (currentCommunityId != null)
                    ViewCommunityButtonClicked?.Invoke(currentCommunityId);
            });

            joinCommunityButton.onClick.AddListener(() =>
            {
                if (currentCommunityId != null)
                    JoinCommunityButtonClicked?.Invoke(currentCommunityId, this);
            });

            originalHeaderSizeDelta = headerContainer.sizeDelta;
            originalFooterSizeDelta = footerContainer.sizeDelta;
        }

        private void OnEnable() =>
            PlayHoverExitAnimation(instant: true);

        private void OnDestroy()
        {
            mainButton.onClick.RemoveAllListeners();
            viewCommunityButton.onClick.RemoveAllListeners();
            joinCommunityButton.onClick.RemoveAllListeners();
        }

        public void SetCommunityId(string id) =>
            currentCommunityId = id;

        public void SetTitle(string title) =>
            communityTitle.text = title;

        public void SetOwner(string owner) =>
            communityOwner.text = owner;

        public void SetDescription(string description) =>
            communityDescription.text = description;

        public void SetPrivacy(CommunityPrivacy privacy)
        {
            communityPrivacyIcon.sprite = privacy == CommunityPrivacy.@public ? publicPrivacySprite : privatePrivacySprite;
            communityPrivacyText.text = privacy == CommunityPrivacy.@public ? PUBLIC_PRIVACY_TEXT : PRIVATE_PRIVACY_TEXT;
        }

        public void SetMembersCount(int memberCount)
        {
            bool showMembers = CommunitiesFeatureAccess.Instance.CanMembersCounterBeDisplayer();
            communityMembersSeparator.SetActive(showMembers);
            communityMembersCountText.gameObject.SetActive(showMembers);
            if (showMembers)
                communityMembersCountText.text = string.Format(MEMBERS_COUNTER_FORMAT, CommunitiesUtility.NumberToCompactString(memberCount));
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

        public void SetupMutualFriends(ProfileRepositoryWrapper profileDataProvider, CommunityData communityData)
        {
            for (var i = 0; i < mutualFriends.thumbnails.Length; i++)
            {
                bool friendExists = i < communityData.friends.Length;
                mutualFriends.thumbnails[i].root.SetActive(friendExists);
                if (!friendExists) continue;
                GetUserCommunitiesData.FriendInCommunity mutualFriend = communityData.friends[i];
                mutualFriends.thumbnails[i].picture.Setup(profileDataProvider, ProfileNameColorHelper.GetNameColor(mutualFriend.name), mutualFriend.profilePictureUrl);
                mutualFriends.thumbnails[i].profileNameTooltip.Setup(mutualFriend.name, mutualFriend.isVerified);

                if (mutualFriends.thumbnails[i].isPointerEventsSubscribed)
                    continue;

                int thumbnailIndex = i;
                Action pointerEnterAction = () => mutualFriends.thumbnails[thumbnailIndex].profileNameTooltip.gameObject.SetActive(true);
                mutualFriends.thumbnails[i].picture.PointerEnter -= pointerEnterAction;
                mutualFriends.thumbnails[i].picture.PointerEnter += pointerEnterAction;

                Action pointerExitAction = () => mutualFriends.thumbnails[thumbnailIndex].profileNameTooltip.gameObject.SetActive(false);
                mutualFriends.thumbnails[i].picture.PointerExit -= pointerExitAction;
                mutualFriends.thumbnails[i].picture.PointerExit += pointerExitAction;

                mutualFriends.thumbnails[i].isPointerEventsSubscribed = true;
            }
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PlayHoverAnimation();

        public void OnPointerExit(PointerEventData eventData) =>
            PlayHoverExitAnimation();

        private void PlayHoverAnimation()
        {
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            headerTween = DOTween.To(() =>
                          headerContainer.sizeDelta,
                          newSizeDelta => headerContainer.sizeDelta = newSizeDelta,
                          new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y - HOVER_ANIMATION_HEIGHT_TO_APPLY),
                          HOVER_ANIMATION_DURATION)
                     .SetEase(Ease.OutQuad);

            footerTween = DOTween.To(() =>
                          footerContainer.sizeDelta,
                          newSizeDelta => footerContainer.sizeDelta = newSizeDelta,
                          new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y + HOVER_ANIMATION_HEIGHT_TO_APPLY),
                          HOVER_ANIMATION_DURATION)
                     .SetEase(Ease.OutQuad);

            descriptionTween = DOTween.To(() =>
                               communityDescriptionCanvasGroup.alpha,
                               newAlpha => communityDescriptionCanvasGroup.alpha = newAlpha,
                               1f,
                               HOVER_ANIMATION_DURATION)
                          .SetEase(Ease.OutQuad);
        }

        private void PlayHoverExitAnimation(bool instant = false)
        {
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            if (instant)
            {
                headerContainer.sizeDelta = originalHeaderSizeDelta;
                footerContainer.sizeDelta = originalFooterSizeDelta;
                communityDescriptionCanvasGroup.alpha = 0f;
            }
            else
            {
                headerTween = DOTween.To(() =>
                              headerContainer.sizeDelta,
                              x => headerContainer.sizeDelta = x,
                              new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y),
                              HOVER_ANIMATION_DURATION)
                         .SetEase(Ease.OutQuad);

                footerTween = DOTween.To(() =>
                              footerContainer.sizeDelta,
                              x => footerContainer.sizeDelta = x,
                              new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y),
                              HOVER_ANIMATION_DURATION)
                         .SetEase(Ease.OutQuad);

                descriptionTween = DOTween.To(() =>
                                   communityDescriptionCanvasGroup.alpha,
                                   newAlpha => communityDescriptionCanvasGroup.alpha = newAlpha,
                                   0f,
                                   HOVER_ANIMATION_DURATION)
                              .SetEase(Ease.OutQuad);
            }
        }
    }
}
