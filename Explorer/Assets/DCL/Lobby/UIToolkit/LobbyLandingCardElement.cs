using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The hero card showing the place the session lands in, with the
    ///     thumbnail filling it, the online users on top, and the title, the creator and the Jump in button along the bottom. Its hierarchy
    ///     comes from LobbyLandingCard.uxml, so the children only exist once that template is instantiated; LobbyLandingCard.uss lays it out.
    /// </summary>
    [UxmlElement]
    public partial class LobbyLandingCardElement : VisualElement, ILobbyThumbnailCard
    {
        private const string USS_BLOCK = "lobby-landing-card";
        private const string USS_LOADING = USS_BLOCK + "--loading";
        private const string USS_WITH_ONLINE = USS_BLOCK + "--with-online";
        private const string USS_STATIC = USS_BLOCK + "--static";

        private const string TITLE_NAME = "Title";
        private const string CREATOR_NAME = "Creator";
        private const string ONLINE_COUNT_NAME = "OnlineCount";
        private const string THUMBNAIL_NAME = "Thumbnail";
        private const string JUMP_IN_NAME = "JumpIn";

        /// <summary>
        ///     Raised on a click anywhere on the card except the Jump in button, while <see cref="CanOpen" />.
        /// </summary>
        public Action? Clicked;

        public Action? JumpInClicked;

        private Label? titleLabel;
        private Label? creatorLabel;
        private Label? onlineCountLabel;
        private VisualElement? thumbnail;
        private Button? jumpInButton;

        // Attribute values and the thumbnail can arrive before the template children do; they are applied once those attach
        private string title = string.Empty;
        private string creator = string.Empty;
        private int onlineCount;
        private bool canJumpIn = true;
        private Sprite? thumbnailSprite;

        [UxmlAttribute]
        public string Title
        {
            get => title;

            set
            {
                title = value;

                if (titleLabel != null)
                    titleLabel.text = value;
            }
        }

        [UxmlAttribute]
        public string Creator
        {
            get => creator;

            set
            {
                creator = value;

                if (creatorLabel != null)
                    creatorLabel.text = value;
            }
        }

        [UxmlAttribute]
        public int OnlineCount
        {
            get => onlineCount;

            set
            {
                onlineCount = value;

                if (onlineCountLabel != null)
                    onlineCountLabel.text = value.ToString();
            }
        }

        /// <summary>
        ///     The online counter is hidden while false, as while the place is still unknown.
        /// </summary>
        [UxmlAttribute]
        public bool HasOnlineCount
        {
            get => ClassListContains(USS_WITH_ONLINE);
            set => EnableInClassList(USS_WITH_ONLINE, value);
        }

        /// <summary>
        ///     The Jump in button is disabled while false, as while the place is still unknown.
        /// </summary>
        [UxmlAttribute]
        public bool CanJumpIn
        {
            get => canJumpIn;

            set
            {
                canJumpIn = value;
                jumpInButton?.SetEnabled(value);
            }
        }

        /// <summary>
        ///     The card takes no click and is marked static, which the stylesheet dims, while false: while the place is still unknown,
        ///     or when it has no details to open.
        /// </summary>
        [UxmlAttribute]
        public bool CanOpen
        {
            get => !ClassListContains(USS_STATIC);
            set => EnableInClassList(USS_STATIC, !value);
        }

        /// <summary>
        ///     Null falls back to the default place thumbnail of the stylesheet.
        /// </summary>
        public Sprite? Thumbnail
        {
            get => thumbnailSprite;

            set
            {
                thumbnailSprite = value;

                if (thumbnail != null)
                    ApplyThumbnail();
            }
        }

        public bool IsLoading
        {
            get => ClassListContains(USS_LOADING);
            set => EnableInClassList(USS_LOADING, value);
        }

        public LobbyLandingCardElement()
        {
            AddToClassList(USS_BLOCK);
            this.AddManipulator(new Clickable(OnClicked));
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent _)
        {
            // The same element can be detached and attached again; its children are resolved only the first time
            if (titleLabel != null)
                return;

            titleLabel = this.Q<Label>(TITLE_NAME);
            creatorLabel = this.Q<Label>(CREATOR_NAME);
            onlineCountLabel = this.Q<Label>(ONLINE_COUNT_NAME);
            thumbnail = this.Q<VisualElement>(THUMBNAIL_NAME);
            jumpInButton = this.Q<Button>(JUMP_IN_NAME);

            jumpInButton.clicked += OnJumpInClicked;

            titleLabel.text = title;
            creatorLabel.text = creator;
            onlineCountLabel.text = onlineCount.ToString();
            jumpInButton.SetEnabled(canJumpIn);
            ApplyThumbnail();
        }

        private void ApplyThumbnail()
        {
            thumbnail!.style.backgroundImage = LobbyCardBackground.From(thumbnailSprite);
        }

        private void OnClicked()
        {
            if (CanOpen)
                Clicked?.Invoke();
        }

        private void OnJumpInClicked() =>
            JumpInClicked?.Invoke();
    }
}
