using CommunicationData.URLHelpers;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public class FriendPanelUserView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        protected readonly List<Button> buttons = new ();

        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }
        [field: SerializeField] public Button MainButton { get; private set; }

        [field: Header("User")]
        [field: SerializeField] public TMP_Text UserName { get; private set; }
        [field: SerializeField] public TMP_Text UserNameTag { get; private set; }
        [field: SerializeField] public GameObject VerifiedIcon { get; private set; }
        [field: SerializeField] public Image ThumbnailBackground { get; private set; }
        [field: SerializeField] public ImageView ProfileImageView { get; private set; }

        private bool canUnHover = true;
        private ImageController? imageController;

        public FriendProfile UserProfile { get; protected set; }
        public event Action<FriendProfile>? MainButtonClicked;
        public event Action<Sprite>? SpriteLoaded;

        internal bool CanUnHover
        {
            get => canUnHover;
            set
            {
                if (!canUnHover && value)
                {
                    canUnHover = value;
                    UnHover();
                }
                canUnHover = value;
            }
        }

        private void Start()
        {
            Background.color = NormalColor;
            MainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(UserProfile));
        }

        public void SafelyResetMainButtonListeners()
        {
            MainButton.onClick.RemoveAllListeners();
            MainButton.onClick.AddListener(() => MainButtonClicked?.Invoke(UserProfile));
        }

        public void RemoveMainButtonClickListeners()
        {
            MainButtonClicked = null;
        }

        public void RemoveSpriteLoadedListeners()
        {
            SpriteLoaded = null;
        }

        public virtual void Configure(FriendProfile friendProfile, IWebRequestController webRequestController, IProfileThumbnailCache profileThumbnailCache)
        {
            if (imageController == null)
            {
                imageController = new ImageController(ProfileImageView, webRequestController);
                imageController.SpriteLoaded += sprite => SpriteLoaded?.Invoke(sprite);
            }

            UnHover();
            UserProfile = friendProfile;

            Color userColor = friendProfile.UserNameColor;

            UserName.text = friendProfile.Name;
            UserName.color = userColor;
            UserNameTag.text = $"#{friendProfile.Address.ToString()[^4..]}";
            UserNameTag.gameObject.SetActive(!friendProfile.HasClaimedName);
            VerifiedIcon.SetActive(friendProfile.HasClaimedName);
            ThumbnailBackground.color = userColor;

            Sprite? thumbnail = profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString());
            if (thumbnail != null)
                imageController.SetImage(thumbnail);
            else if (friendProfile.FacePictureUrl != URLAddress.EMPTY)
                imageController.RequestImage(friendProfile.FacePictureUrl, removePrevious: true);
        }

        protected virtual void ToggleButtonView(bool isActive)
        {
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].gameObject.SetActive(isActive);
        }

        private void UnHover()
        {
            ToggleButtonView(false);
            Background.color = NormalColor;
        }

        private void Hover()
        {
            ToggleButtonView(true);
            Background.color = HoveredColor;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            Hover();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (canUnHover)
                UnHover();
        }
    }
}
