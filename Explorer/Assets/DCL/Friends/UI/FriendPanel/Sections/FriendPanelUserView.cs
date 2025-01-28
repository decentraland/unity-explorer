using CommunicationData.URLHelpers;
using DCL.Chat;
using DCL.UI;
using DCL.WebRequests;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public class FriendPanelUserView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        protected Button[] buttons { get; set; }
        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }
        [field: SerializeField] public Button MainButton { get; private set; }

        [field: Header("User")]
        [field: SerializeField] public ChatEntryConfigurationSO ChatEntryConfiguration { get; private set; }
        [field: SerializeField] public TMP_Text UserName { get; private set; }
        [field: SerializeField] public TMP_Text UserNameTag { get; private set; }
        [field: SerializeField] public Image FaceFrame { get; private set; }
        [field: SerializeField] public Image FaceRim { get; private set; }
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

            Color userColor = ChatEntryConfiguration.GetNameColor(friendProfile.Name);

            UserName.text = friendProfile.Name;
            UserName.color = userColor;
            UserNameTag.text = $"#{friendProfile.Address.ToString()[^4..]}";
            UserNameTag.gameObject.SetActive(!friendProfile.HasClaimedName);
            FaceFrame.color = userColor;
            userColor.r += 0.3f;
            userColor.g += 0.3f;
            userColor.b += 0.3f;
            FaceRim.color = userColor;

            Sprite? thumbnail = profileThumbnailCache.GetThumbnail(friendProfile.Address.ToString());
            if (thumbnail != null)
                imageController.SetImage(thumbnail);
            else if (friendProfile.FacePictureUrl != URLAddress.EMPTY)
                imageController.RequestImage(friendProfile.FacePictureUrl, removePrevious: true);
        }

        protected virtual void ToggleButtonView(bool isActive)
        {
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].gameObject.SetActive(isActive);
        }

        protected void UnHover()
        {
            ToggleButtonView(false);
            Background.color = NormalColor;
        }

        protected void Hover()
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
