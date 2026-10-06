using DCL.UI;
using DCL.UI.ProfileElements;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Friend card of the lobby. Its children come from LobbyFriendCard.uxml, so values set before the template is
    ///     instantiated are applied on the first attach.
    /// </summary>
    [UxmlElement]
    public partial class LobbyFriendCardElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-friend-card";
        private const string USS_LOADING = USS_BLOCK + "--loading";
        private const string USS_VERIFIED = USS_BLOCK + "--verified";
        private const string USS_JOINABLE = USS_BLOCK + "--joinable";
        private const string USS_ONLINE = USS_BLOCK + "--online";
        private const string USS_AWAY = USS_BLOCK + "--away";

        private const string PICTURE_NAME = "Picture";
        private const string NAME_NAME = "Name";
        private const string WALLET_TAG_NAME = "WalletTag";
        private const string LOCATION_TEXT_NAME = "LocationText";
        private const string JOIN_NAME = "Join";

        public Action? Clicked;

        public Action? JoinClicked;

        private VisualElement? picture;
        private Label? nameLabel;
        private Label? walletTagLabel;
        private Label? locationLabel;
        private Button? joinButton;

        private string userName = string.Empty;
        private Color userNameColor = Color.white;
        private string walletTag = string.Empty;
        private string location = string.Empty;
        private OnlineStatus onlineStatus = OnlineStatus.Offline;
        private Sprite? pictureSprite;
        private Color pictureColor = Color.white;

        [UxmlAttribute]
        public string UserName
        {
            get => userName;

            set
            {
                userName = value;

                if (nameLabel != null)
                    nameLabel.text = value;
            }
        }

        [UxmlAttribute]
        public Color UserNameColor
        {
            get => userNameColor;

            set
            {
                userNameColor = value;

                if (nameLabel != null)
                    nameLabel.style.color = value;
            }
        }

        [UxmlAttribute]
        public string WalletTag
        {
            get => walletTag;

            set
            {
                walletTag = value;

                if (walletTagLabel != null)
                    walletTagLabel.text = value;
            }
        }

        [UxmlAttribute]
        public bool IsVerified
        {
            get => ClassListContains(USS_VERIFIED);
            set => EnableInClassList(USS_VERIFIED, value);
        }

        [UxmlAttribute]
        public OnlineStatus OnlineStatus
        {
            get => onlineStatus;

            set
            {
                onlineStatus = value;
                EnableInClassList(USS_ONLINE, value == OnlineStatus.Online);
                EnableInClassList(USS_AWAY, value == OnlineStatus.Away);
            }
        }

        [UxmlAttribute]
        public string Location
        {
            get => location;

            set
            {
                location = value;

                if (locationLabel != null)
                    locationLabel.text = value;
            }
        }

        [UxmlAttribute]
        public bool CanJoin
        {
            get => ClassListContains(USS_JOINABLE);
            set => EnableInClassList(USS_JOINABLE, value);
        }

        public Sprite? Picture
        {
            get => pictureSprite;

            set
            {
                pictureSprite = value;

                if (picture != null)
                    ApplyPicture(picture);
            }
        }

        public Color PictureColor
        {
            get => pictureColor;

            set
            {
                pictureColor = value;

                if (picture != null)
                    ApplyPicture(picture);
            }
        }

        public bool IsLoading
        {
            get => ClassListContains(USS_LOADING);
            set => EnableInClassList(USS_LOADING, value);
        }

        public LobbyFriendCardElement()
        {
            AddToClassList(USS_BLOCK);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);
            this.AddManipulator(new Clickable(OnClicked));
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent _)
        {
            if (nameLabel != null)
                return;

            picture = this.Q<VisualElement>(PICTURE_NAME);
            nameLabel = this.Q<Label>(NAME_NAME);
            walletTagLabel = this.Q<Label>(WALLET_TAG_NAME);
            locationLabel = this.Q<Label>(LOCATION_TEXT_NAME);
            joinButton = this.Q<Button>(JOIN_NAME);

            joinButton.clicked += OnJoinClicked;

            nameLabel.text = userName;
            nameLabel.style.color = userNameColor;
            walletTagLabel.text = walletTag;
            locationLabel.text = location;
            ApplyPicture(picture);
        }

        private void ApplyPicture(VisualElement target)
        {
            target.SetProfileColor(pictureColor);
            target.style.backgroundImage = VisualElementsExtensions.CoverBackground(pictureSprite);
        }

        private void OnClicked() =>
            Clicked?.Invoke();

        private void OnJoinClicked() =>
            JoinClicked?.Invoke();
    }
}
