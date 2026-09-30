using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     UI Toolkit counterpart of <see cref="LobbyPlaceCardView" />: thumbnail with the online users on top, title and creator.
    ///     Its hierarchy comes from LobbyPlaceCard.uxml, so the children only exist once that template is instantiated; hovering
    ///     raises the footer over the thumbnail and swaps the creator for the Jump in button, all driven by LobbyPlaceCard.uss.
    /// </summary>
    [UxmlElement]
    public partial class LobbyPlaceCardElement : VisualElement, ILobbyThumbnailCard
    {
        private const string USS_BLOCK = "lobby-place-card";
        private const string USS_LOADING = USS_BLOCK + "--loading";

        private const string TITLE_NAME = "Title";
        private const string CREATOR_NAME = "Creator";
        private const string ONLINE_COUNT_NAME = "OnlineCount";
        private const string THUMBNAIL_NAME = "Thumbnail";
        private const string JUMP_IN_NAME = "JumpIn";

        /// <summary>
        ///     Raised on a click anywhere on the card except the Jump in button.
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

        public LobbyPlaceCardElement()
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
            ApplyThumbnail();
        }

        private void ApplyThumbnail()
        {
            thumbnail!.style.backgroundImage = LobbyCardBackground.From(thumbnailSprite);
        }

        private void OnClicked() =>
            Clicked?.Invoke();

        private void OnJumpInClicked() =>
            JumpInClicked?.Invoke();
    }
}
