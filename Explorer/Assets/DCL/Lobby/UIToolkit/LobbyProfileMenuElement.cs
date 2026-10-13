using DCL.UI.ProfileElements;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Dropdown of the own profile under the top bar: a full-screen layer whose backdrop takes the outside click.
    ///     Its children come from LobbyProfileMenu.uxml, so values set before the template is instantiated are applied on the first attach.
    /// </summary>
    [UxmlElement]
    public partial class LobbyProfileMenuElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-profile-menu";
        private const string USS_VERIFIED = USS_BLOCK + "--verified";
        private const string USS_COPIED = USS_BLOCK + "--copied";
        private const string USS_WITH_BUG_REPORT = USS_BLOCK + "--with-bug-report";
        private const string USS_PICTURE_LOADING = USS_BLOCK + "__picture--loading";

        private const string BACKDROP_NAME = "Backdrop";
        private const string PICTURE_NAME = "Picture";
        private const string NAME_NAME = "Name";
        private const string ADDRESS_NAME = "Address";
        private const string COPY_ADDRESS_NAME = "CopyAddress";
        private const string PROFILE_ITEM_NAME = "ProfileItem";
        private const string SETTINGS_ITEM_NAME = "SettingsItem";
        private const string REPORT_BUG_ITEM_NAME = "ReportBugItem";
        private const string SIGN_OUT_ITEM_NAME = "SignOutItem";
        private const string EXIT_ITEM_NAME = "ExitItem";
        private const string TERMS_LINK_NAME = "TermsLink";
        private const string PRIVACY_LINK_NAME = "PrivacyLink";

        public Action? OutsideClicked;
        public Action? CopyAddressClicked;
        public Action? ProfileClicked;
        public Action? SettingsClicked;
        public Action? ReportBugClicked;
        public Action? SignOutClicked;
        public Action? ExitClicked;
        public Action? TermsClicked;
        public Action? PrivacyClicked;

        private VisualElement? picture;
        private Label? nameLabel;
        private Label? addressLabel;

        private string userName = string.Empty;
        private Color userNameColor = Color.white;
        private string address = string.Empty;
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
        public string Address
        {
            get => address;

            set
            {
                address = value;

                if (addressLabel != null)
                    addressLabel.text = value;
            }
        }

        [UxmlAttribute]
        public bool IsVerified
        {
            get => ClassListContains(USS_VERIFIED);
            set => EnableInClassList(USS_VERIFIED, value);
        }

        [UxmlAttribute]
        public bool CanReportBug
        {
            get => ClassListContains(USS_WITH_BUG_REPORT);
            set => EnableInClassList(USS_WITH_BUG_REPORT, value);
        }

        public bool IsCopiedShown
        {
            get => ClassListContains(USS_COPIED);
            set => EnableInClassList(USS_COPIED, value);
        }

        public bool IsOpen => this.IsDisplayed();

        public LobbyProfileMenuElement()
        {
            AddToClassList(USS_BLOCK);
            pickingMode = PickingMode.Ignore;

            // Inline so IsOpen reads back what Open and Close set
            this.SetDisplayed(false);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        public void Open() =>
            this.SetDisplayed(true);

        public void Close() =>
            this.SetDisplayed(false);

        public void SetPicture(Sprite? sprite, Color color, bool loading)
        {
            pictureSprite = sprite;
            pictureColor = color;
            EnableInClassList(USS_PICTURE_LOADING, loading);

            if (picture != null)
                ApplyPicture(picture);
        }

        private void OnAttachToPanel(AttachToPanelEvent _)
        {
            if (nameLabel != null)
                return;

            picture = this.Q<VisualElement>(PICTURE_NAME);
            nameLabel = this.Q<Label>(NAME_NAME);
            addressLabel = this.Q<Label>(ADDRESS_NAME);

            this.Q<VisualElement>(BACKDROP_NAME).RegisterCallback<PointerDownEvent>(OnBackdropPointerDown);
            this.Q<Button>(COPY_ADDRESS_NAME).clicked += OnCopyAddressClicked;
            this.Q<Button>(PROFILE_ITEM_NAME).clicked += OnProfileClicked;
            this.Q<Button>(SETTINGS_ITEM_NAME).clicked += OnSettingsClicked;
            this.Q<Button>(REPORT_BUG_ITEM_NAME).clicked += OnReportBugClicked;
            this.Q<Button>(SIGN_OUT_ITEM_NAME).clicked += OnSignOutClicked;
            this.Q<Button>(EXIT_ITEM_NAME).clicked += OnExitClicked;
            this.Q<Button>(TERMS_LINK_NAME).clicked += OnTermsClicked;
            this.Q<Button>(PRIVACY_LINK_NAME).clicked += OnPrivacyClicked;

            nameLabel.text = userName;
            nameLabel.style.color = userNameColor;
            addressLabel.text = address;
            ApplyPicture(picture);
        }

        private void ApplyPicture(VisualElement target)
        {
            target.SetProfileColor(pictureColor);
            target.style.backgroundImage = VisualElementsExtensions.CoverBackground(pictureSprite);
        }

        private void OnBackdropPointerDown(PointerDownEvent _) =>
            OutsideClicked?.Invoke();

        private void OnCopyAddressClicked() =>
            CopyAddressClicked?.Invoke();

        private void OnProfileClicked() =>
            ProfileClicked?.Invoke();

        private void OnSettingsClicked() =>
            SettingsClicked?.Invoke();

        private void OnReportBugClicked() =>
            ReportBugClicked?.Invoke();

        private void OnSignOutClicked() =>
            SignOutClicked?.Invoke();

        private void OnExitClicked() =>
            ExitClicked?.Invoke();

        private void OnTermsClicked() =>
            TermsClicked?.Invoke();

        private void OnPrivacyClicked() =>
            PrivacyClicked?.Invoke();
    }
}
