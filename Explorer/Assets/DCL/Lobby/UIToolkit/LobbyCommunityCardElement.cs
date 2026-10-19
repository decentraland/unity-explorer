using DCL.UI;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Community card; its hierarchy comes from LobbyCommunityCard.uxml, laid out and hover-animated by
    ///     LobbyCommunityCard.uss. The private badge is only shown on a private community; the View button a hover brings
    ///     up does what a click on the card does.
    /// </summary>
    [UxmlElement]
    public partial class LobbyCommunityCardElement : LobbyThumbnailCardElement
    {
        private const string USS_BLOCK = "lobby-community-card";
        private const string USS_PRIVATE = USS_BLOCK + "--private";
        private const string MEMBERS_FORMAT = "{0} Members";

        private const string TITLE_NAME = "Title";
        private const string MEMBERS_NAME = "Members";
        private const string VIEW_NAME = "View";

        private Label? titleLabel;
        private Label? membersLabel;

        private string title = string.Empty;
        private int membersCount;

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
            get => ClassListContains(USS_PRIVATE);
            set => EnableInClassList(USS_PRIVATE, value);
        }

        public LobbyCommunityCardElement() : base(USS_BLOCK) { }

        protected override void ResolveChildren()
        {
            titleLabel = this.Q<Label>(TITLE_NAME);
            membersLabel = this.Q<Label>(MEMBERS_NAME);

            titleLabel.text = title;
            membersLabel.text = MembersText(membersCount);

            // The View button of the hovered card opens what the card itself opens
            this.Q<Button>(VIEW_NAME).clicked += OnViewClicked;
        }

        private void OnViewClicked() =>
            Clicked?.Invoke();

        // Counted like the communities browser counts them, so a big community reads as "1.2K Members" in both
        private static string MembersText(int count) =>
            string.Format(MEMBERS_FORMAT, UIUtils.NumberToCompactString(count));
    }
}
