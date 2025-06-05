using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3;
using MVC;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.UI.ProfileNames
{
    public class ProfileNameEditorController : ControllerBase<ProfileNameEditorView>
    {
        private const int MAX_NAME_LENGTH = 15;
        private const string CHARACTER_LIMIT_REACHED_MESSAGE = "Character limit reached";
        private const string VALID_CHARACTERS_ARE_ALLOWED_MESSAGE = "Please use only letters and numbers";

        private readonly IWebBrowser webBrowser;
        private readonly ISelfProfile selfProfile;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IProfileChangesBus profileChangesBus;
        private readonly List<TMP_Dropdown.OptionData> dropdownOptions = new ();
        private readonly Regex validNameRegex = new (@"^[a-zA-Z0-9]+$");
        private UniTaskCompletionSource? lifeCycleTask;
        private CancellationTokenSource? saveCancellationToken;
        private CancellationTokenSource? setupCancellationToken;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action? NameChanged;
        public event Action? NameClaimRequested;

        public ProfileNameEditorController(ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            ISelfProfile selfProfile,
            INftNamesProvider nftNamesProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            IProfileChangesBus profileChangesBus) : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.selfProfile = selfProfile;
            this.nftNamesProvider = nftNamesProvider;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.profileChangesBus = profileChangesBus;
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
                claimedConfig.saveButtonInteractable = i != -1;
            });

            claimedConfig.clickeableLink.OnLinkClicked += url => webBrowser.OpenUrl(new Uri(url));

            viewInstance.OverlayCloseButton.onClick.AddListener(Close);

            return;

            void Initialize(ProfileNameEditorView.NonClaimedNameConfig config)
            {
                config.cancelButton.onClick.AddListener(Close);
                config.saveButton.onClick.AddListener(() => Save(config));
                config.claimNameButton.onClick.AddListener(ClaimNewName);
                config.input.onValueChanged.AddListener(s => OnInputValueChanged(s, config));
                config.errorContainer.SetActive(false);
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
                claimedConfig.saveButtonInteractable = false;
                claimedConfig.saveLoading.SetActive(false);
                claimedConfig.NonClaimedNameTabConfig.input.text = string.Empty;
                claimedConfig.NonClaimedNameTabConfig.saveButtonInteractable = false;
                claimedConfig.dropdownLoadingSpinner.SetActive(true);
                claimedConfig.claimedNameDropdown.gameObject.SetActive(false);

                ProfileNameEditorView.NonClaimedNameConfig nonClaimedConfig = viewInstance!.NonClaimedNameContainer;
                nonClaimedConfig.input.text = string.Empty;
                nonClaimedConfig.saveButtonInteractable = false;
                nonClaimedConfig.saveLoading.SetActive(false);

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
                // Always start as disabled as it makes no sense save your own current name again..
                config.saveButtonInteractable = false;
                config.saveLoading.SetActive(false);
            }

            void SetUpNonClaimed(ProfileNameEditorView.NonClaimedNameConfig config, Profile profile)
            {
                config.userHashLabel.text = $"#{profile.UserId[^4..]}";
                config.input.text = string.Empty;
                config.saveButtonInteractable = false;
                config.saveLoading.SetActive(false);
            }
        }

        private void OnInputValueChanged(string value, ProfileNameEditorView.NonClaimedNameConfig config)
        {
            bool isValidLength = value.Length <= MAX_NAME_LENGTH;
            bool isValidName = validNameRegex.IsMatch(value);
            bool isEmpty = string.IsNullOrEmpty(value);

            config.characterCountLabel.text = $"{value.Length}/{MAX_NAME_LENGTH}";
            config.saveButtonInteractable = !isEmpty && isValidName && isValidLength;

            if ((!isValidLength || !isValidName) && !isEmpty)
            {
                config.characterCountLabel.color = Color.red;
                config.inputOutline.color = Color.red;
                config.errorContainer.SetActive(true);

                if (!isValidLength)
                    config.inputErrorMessage.text = CHARACTER_LIMIT_REACHED_MESSAGE;
                else if (!isValidName)
                    config.inputErrorMessage.text = VALID_CHARACTERS_ARE_ALLOWED_MESSAGE;
            }
            else
            {
                Color color = Color.white;
                color.a = 0.5f;
                config.inputOutline.color = color;
                config.characterCountLabel.color = color;
                config.errorContainer.SetActive(false);
            }
        }

        private void ClaimNewName()
        {
            webBrowser.OpenUrl(decentralandUrlsSource.Url(DecentralandUrl.MarketplaceClaimName));
            NameClaimRequested?.Invoke();
        }

        private void Save(ProfileNameEditorView.NonClaimedNameConfig config)
        {
            saveCancellationToken = saveCancellationToken.SafeRestart();
            SaveAsync(saveCancellationToken.Token).Forget();
            return;

            async UniTaskVoid SaveAsync(CancellationToken ct)
            {
                config.saveButtonInteractable = false;
                config.saveLoading.SetActive(true);

                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (profile != null)
                {
                    profile.Name = config.input.text;
                    profile.HasClaimedName = false;

                    try
                    {
                        Profile? updatedProfile = await selfProfile.UpdateProfileAsync(profile, ct);
                        NameChanged?.Invoke();

                        if (updatedProfile != null)
                            profileChangesBus.PushProfileNameChange(updatedProfile);
                    }
                    catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.PROFILE); }
                }

                config.saveButtonInteractable = true;
                config.saveLoading.SetActive(false);

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
                config.saveButtonInteractable = false;
                config.saveLoading.SetActive(true);

                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (profile != null)
                {
                    profile.Name = config.claimedNameDropdown.options[config.claimedNameDropdown.value].text;
                    profile.HasClaimedName = true;

                    try
                    {
                        Profile? updatedProfile = await selfProfile.UpdateProfileAsync(profile, ct);
                        NameChanged?.Invoke();

                        if (updatedProfile != null)
                            profileChangesBus.PushProfileNameChange(updatedProfile);
                    }
                    catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.PROFILE); }
                }

                config.saveButtonInteractable = true;
                config.saveLoading.SetActive(false);

                Close();
            }
        }

        private void Close() =>
            lifeCycleTask?.TrySetResult();
    }
}
