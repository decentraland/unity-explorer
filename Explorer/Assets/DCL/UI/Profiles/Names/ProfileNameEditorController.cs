using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3;
using MVC;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using Utility;

namespace DCL.UI.ProfileNames
{
    public class ProfileNameEditorController : ControllerBase<ProfileNameEditorView>
    {
        private const string CLAIM_NAME_URL = "https://decentraland.org/marketplace/names/claim";

        private readonly IWebBrowser webBrowser;
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly List<TMP_Dropdown.OptionData> dropdownOptions = new ();
        private readonly Regex validNameRegex = new (@"^[a-zA-Z0-9]+$");
        private UniTaskCompletionSource? lifeCycleTask;
        private CancellationTokenSource? saveCancellationToken;
        private CancellationTokenSource? setupCancellationToken;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public ProfileNameEditorController(ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            ISelfProfile selfProfile,
            INftNamesProvider nftNamesProvider) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            lifeCycleTask = new UniTaskCompletionSource();
            return lifeCycleTask.Task.AttachExternalCancellation(ct);
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            ProfileNameEditorView.NonClaimedNameConfig nonClaimedConfig = viewInstance!.NonClaimedNameContainer;
            Initialize(nonClaimedConfig);

            ProfileNameEditorView.ClaimedNameConfig claimedConfig = viewInstance.ClaimedNameContainer;
            Initialize(claimedConfig.NonClaimedNameTabConfig);

            claimedConfig.ClaimedNameTabHeader.Select();
            claimedConfig.NonClaimedNameTabHeader.Deselect();

            claimedConfig.ClaimedNameTabHeader.SelectButton.onClick.AddListener(() =>
            {
                claimedConfig.ClaimedNameTabHeader.Select();
                claimedConfig.NonClaimedNameTabHeader.Deselect();
            });

            claimedConfig.NonClaimedNameTabHeader.SelectButton.onClick.AddListener(() =>
            {
                claimedConfig.ClaimedNameTabHeader.Deselect();
                claimedConfig.NonClaimedNameTabHeader.Select();
            });

            claimedConfig.cancelButton.onClick.AddListener(Close);
            claimedConfig.saveButton.onClick.AddListener(() => Save(claimedConfig));
            claimedConfig.claimedNameDropdown.onValueChanged.AddListener(i =>
            {
                claimedConfig.dropdownVerifiedIcon.SetActive(i != -1);
                claimedConfig.saveButton.interactable = i != -1;
            });

            claimedConfig.clickeableLink.OnLinkClicked += url => webBrowser.OpenUrl(url);

            return;

            void Initialize(ProfileNameEditorView.NonClaimedNameConfig config)
            {
                config.cancelButton.onClick.AddListener(Close);
                config.saveButton.onClick.AddListener(() => Save(config));
                config.claimNameButton.onClick.AddListener(ClaimNewName);
                config.input.onValueChanged.AddListener(s => OnInputValueChanged(s, config));
            }
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            setupCancellationToken = setupCancellationToken.SafeRestart();
            SetUpAsync(setupCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SetUpAsync(CancellationToken ct)
            {
                ProfileNameEditorView.ClaimedNameConfig claimedConfig = viewInstance!.ClaimedNameContainer;
                claimedConfig.claimedNameDropdown.ClearOptions();
                claimedConfig.claimedNameDropdown.value = -1;
                claimedConfig.dropdownVerifiedIcon.SetActive(false);
                claimedConfig.saveButton.interactable = false;
                claimedConfig.NonClaimedNameTabConfig.input.text = string.Empty;
                claimedConfig.NonClaimedNameTabConfig.saveButton.interactable = false;
                claimedConfig.dropdownLoadingSpinner.SetActive(true);
                claimedConfig.claimedNameDropdown.gameObject.SetActive(false);

                ProfileNameEditorView.NonClaimedNameConfig nonClaimedConfig = viewInstance!.NonClaimedNameContainer;
                nonClaimedConfig.input.text = string.Empty;
                nonClaimedConfig.saveButton.interactable = false;

                Profile? profile = await selfProfile.ProfileAsync(ct);

                using INftNamesProvider.PaginatedNamesResponse names = await nftNamesProvider.GetAsync(new Web3Address(profile!.UserId), 1, 100, ct);

                nonClaimedConfig.root.SetActive(names.TotalAmount <= 0);
                claimedConfig.NonClaimedNameTabConfig.root.SetActive(names.TotalAmount > 0);
                claimedConfig.dropdownLoadingSpinner.SetActive(false);
                claimedConfig.claimedNameDropdown.gameObject.SetActive(true);

                if (names.TotalAmount > 0)
                    SetUpClaimed(claimedConfig, profile, names);
                else
                    SetUpNonClaimed(nonClaimedConfig, profile);
            }

            void SetUpClaimed(ProfileNameEditorView.ClaimedNameConfig config, Profile profile, INftNamesProvider.PaginatedNamesResponse names)
            {
                SetUpNonClaimed(config.NonClaimedNameTabConfig, profile);

                // This provokes an undesired change in the tab navigation. Just keep the last state..
                // if (profile.HasClaimedName)
                // {
                //     config.ClaimedNameTabHeader.Select();
                //     config.NonClaimedNameTabHeader.Deselect();
                // }
                // else
                // {
                //     config.ClaimedNameTabHeader.Deselect();
                //     config.NonClaimedNameTabHeader.Select();
                // }

                dropdownOptions.Clear();

                foreach (string name in names.Names)
                    dropdownOptions.Add(new TMP_Dropdown.OptionData(name));

                config.claimedNameDropdown.options = dropdownOptions;

                int selectedIndex = config.claimedNameDropdown.options.FindIndex(option => option.text == profile.Name);
                config.claimedNameDropdown.value = selectedIndex;
            }

            void SetUpNonClaimed(ProfileNameEditorView.NonClaimedNameConfig config, Profile profile)
            {
                config.userHashLabel.text = $"#{profile.UserId[^4..]}";
                config.input.text = string.Empty;
                config.saveButton.interactable = false;
            }
        }

        private void OnInputValueChanged(string value, ProfileNameEditorView.NonClaimedNameConfig config)
        {
            config.characterCountLabel.text = $"{value.Length}/{config.input.characterLimit}";
            config.saveButton.interactable = IsValidName();
            return;

            bool IsValidName()
            {
                if (string.IsNullOrEmpty(value)) return false;
                if (!validNameRegex.IsMatch(value)) return false;
                return true;
            }
        }

        private void ClaimNewName() =>
            webBrowser.OpenUrl(CLAIM_NAME_URL);

        private void Save(ProfileNameEditorView.NonClaimedNameConfig config)
        {
            saveCancellationToken = saveCancellationToken.SafeRestart();
            SaveAsync(saveCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SaveAsync(CancellationToken ct)
            {
                config.saveButton.interactable = false;

                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (profile != null)
                {
                    profile.Name = config.input.text;
                    profile.HasClaimedName = false;

                    await selfProfile.UpdateProfileAsync(profile, ct);
                }

                config.saveButton.interactable = true;

                Close();
            }
        }

        private void Save(ProfileNameEditorView.ClaimedNameConfig config)
        {
            saveCancellationToken = saveCancellationToken.SafeRestart();
            SaveAsync(saveCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SaveAsync(CancellationToken ct)
            {
                config.saveButton.interactable = false;

                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (profile != null)
                {
                    profile.Name = config.claimedNameDropdown.options[config.claimedNameDropdown.value].text;
                    profile.HasClaimedName = true;

                    await selfProfile.UpdateProfileAsync(profile, ct);
                }

                config.saveButton.interactable = true;

                Close();
            }
        }

        private void Close() =>
            lifeCycleTask?.TrySetResult();
    }
}
