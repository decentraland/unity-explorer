using DCL.UI;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Community card built from LobbyCommunityCard.uxml and styled by LobbyCommunityCard.uss.
    /// </summary>
    [UxmlElement]
    public partial class LobbyCommunityCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-community-card";
        private const string USS_PRIVATE = USS_BLOCK + "--private";
        private const string MEMBERS_FORMAT = "{0} Members";
        private const string PUBLIC_TEXT = "Public";
        private const string PRIVATE_TEXT = "Private";

        private const string TITLE_NAME = "Title";
        private const string VISIBILITY_NAME = "Visibility";
        private const string MEMBERS_NAME = "Members";
        private const string VIEW_NAME = "View";

        private Label? titleLabel;
        private Label? visibilityLabel;
        private Label? membersLabel;

        private string title = string.Empty;
        private int membersCount;
        private bool isPrivate;

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
        public int MembersCount
        {
            get => membersCount;

            set
            {
                membersCount = value;

                if (membersLabel != null)
                    membersLabel.text = MembersText(value);
            }
        }

        [UxmlAttribute]
        public bool IsPrivate
        {
            get => isPrivate;

            set
            {
                isPrivate = value;
                EnableInClassList(USS_PRIVATE, value);

                if (visibilityLabel != null)
                    visibilityLabel.text = VisibilityText(value);
            }
        }

        public LobbyCommunityCardElement() : base(USS_BLOCK) { }

        protected override void ResolveChildren()
        {
            titleLabel = this.Q<Label>(TITLE_NAME);
            visibilityLabel = this.Q<Label>(VISIBILITY_NAME);
            membersLabel = this.Q<Label>(MEMBERS_NAME);

            titleLabel.text = title;
            visibilityLabel.text = VisibilityText(isPrivate);
            membersLabel.text = MembersText(membersCount);

            this.Q<Button>(VIEW_NAME).clicked += OnViewClicked;
        }

        private void OnViewClicked() =>
            Clicked?.Invoke();

        private static string VisibilityText(bool isPrivate) =>
            isPrivate ? PRIVATE_TEXT : PUBLIC_TEXT;

        // Compacted like the communities browser does, so both read "1.2k Members".
        private static string MembersText(int count) =>
            string.Format(MEMBERS_FORMAT, UIUtils.NumberToCompactString(count));
    }
}
