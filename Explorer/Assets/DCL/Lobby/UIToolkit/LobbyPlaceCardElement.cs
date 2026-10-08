using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Place card; its hierarchy comes from LobbyPlaceCard.uxml, laid out and hover-animated by LobbyPlaceCard.uss.
    ///     The online counter is only shown while somebody is there.
    /// </summary>
    [UxmlElement]
    public partial class LobbyPlaceCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-place-card";
        private const string WITH_ONLINE_MODIFIER = "--with-online";

        private const string TITLE_NAME = "Title";
        private const string CREATOR_NAME = "Creator";
        private const string ONLINE_COUNT_NAME = "OnlineCount";
        private const string JUMP_IN_NAME = "JumpIn";
        private const string CONNECTED_FRIENDS_NAME = "ConnectedFriends";

        public Action? JumpInClicked;

        protected Button? jumpInButton;

        private readonly string ussWithOnline;

        private Label? titleLabel;
        private Label? creatorLabel;
        private Label? onlineCountLabel;
        private LobbyConnectedFriendsElement? connectedFriends;

        private string title = string.Empty;
        private string creator = string.Empty;
        private int onlineCount;

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
                EnableInClassList(ussWithOnline, value > 0);

                if (onlineCountLabel != null)
                    onlineCountLabel.text = value.ToString();
            }
        }

        /// <summary>Row of the friends at the place; null until the first attach.</summary>
        public LobbyConnectedFriendsElement? ConnectedFriends => connectedFriends;

        public LobbyPlaceCardElement() : this(USS_BLOCK) { }

        protected LobbyPlaceCardElement(string ussBlock) : base(ussBlock)
        {
            ussWithOnline = ussBlock + WITH_ONLINE_MODIFIER;
        }

        protected override void ResolveChildren()
        {
            titleLabel = this.Q<Label>(TITLE_NAME);
            creatorLabel = this.Q<Label>(CREATOR_NAME);
            onlineCountLabel = this.Q<Label>(ONLINE_COUNT_NAME);
            jumpInButton = this.Q<Button>(JUMP_IN_NAME);
            connectedFriends = this.Q<LobbyConnectedFriendsElement>(CONNECTED_FRIENDS_NAME);

            jumpInButton.clicked += OnJumpInClicked;

            titleLabel.text = title;
            creatorLabel.text = creator;
            onlineCountLabel.text = onlineCount.ToString();
        }

        private void OnJumpInClicked() =>
            JumpInClicked?.Invoke();
    }
}
