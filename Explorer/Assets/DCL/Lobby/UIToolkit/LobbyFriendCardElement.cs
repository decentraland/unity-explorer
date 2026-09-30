using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="LobbyFriendCardView" />: picture with the online dot, name, and a location row that
    ///     hovering swaps for a Join button while the friend can be joined. Its hierarchy comes from LobbyFriendCard.uxml, so the
    ///     children only exist once that template is instantiated; the hover swap and the status colors are driven by LobbyFriendCard.uss.
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

        /// <summary>
        ///     Raised on a click anywhere on the card except the Join button.
        /// </summary>
        public Action? Clicked;

        public Action? JoinClicked;

        private VisualElement? picture;
        private Label? nameLabel;
        private Label? walletTagLabel;
        private Label? locationLabel;
        private Button? joinButton;

        // Attribute values and the picture can arrive before the template children do; they are applied once those attach
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

        /// <summary>
        ///     Shown after the name while the friend has no claimed name.
        /// </summary>
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

        /// <summary>
        ///     Null falls back to the default profile picture of the stylesheet.
        /// </summary>
        public Sprite? Picture
        {
            get => pictureSprite;

            set
            {
                pictureSprite = value;

                if (picture != null)
                    ApplyPicture();
            }
        }

        /// <summary>
        ///     Shown behind the picture, and instead of it while there is none.
        /// </summary>
        public Color PictureColor
        {
            get => pictureColor;

            set
            {
                pictureColor = value;

                if (picture != null)
                    ApplyPicture();
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
            this.AddManipulator(new Clickable(OnClicked));
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent _)
        {
            // The same element can be detached and attached again; its children are resolved only the first time
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
            ApplyPicture();
        }

        private void ApplyPicture()
        {
            picture!.style.backgroundColor = pictureColor;
            picture.style.backgroundImage = LobbyCardBackground.From(pictureSprite);
        }

        private void OnClicked() =>
            Clicked?.Invoke();

        private void OnJoinClicked() =>
            JoinClicked?.Invoke();
    }
}
