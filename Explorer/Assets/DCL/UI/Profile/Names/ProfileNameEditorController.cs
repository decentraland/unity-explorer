using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3;
using MVC;
using System.Collections.Generic;
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
                Profile? profile = await selfProfile.ProfileAsync(ct);

                using INftNamesProvider.PaginatedNamesResponse names = await nftNamesProvider.GetAsync(new Web3Address(profile!.UserId), 1, 100, ct);

                viewInstance!.NonClaimedNameContainer.root.SetActive(names.TotalAmount <= 0);
                viewInstance!.ClaimedNameContainer.NonClaimedNameTabConfig.root.SetActive(names.TotalAmount > 0);

                if (names.TotalAmount > 0)
                    SetUpClaimed(viewInstance!.ClaimedNameContainer, profile, names);
                else
                    SetUpNonClaimed(viewInstance!.NonClaimedNameContainer, profile);
            }

            void SetUpClaimed(ProfileNameEditorView.ClaimedNameConfig config, Profile profile, INftNamesProvider.PaginatedNamesResponse names)
            {
                SetUpNonClaimed(config.NonClaimedNameTabConfig, profile);

                config.ClaimedNameTabHeader.Select();
                config.NonClaimedNameTabHeader.Deselect();
                config.saveButton.interactable = true;

                dropdownOptions.Clear();

                foreach (string name in names.Names)
                    dropdownOptions.Add(new TMP_Dropdown.OptionData(name));

                config.claimedNameDropdown.options = dropdownOptions;
            }

            void SetUpNonClaimed(ProfileNameEditorView.NonClaimedNameConfig config, Profile profile)
            {
                config.userHashLabel.text = $"#{profile.UserId[^4..]}";
                config.input.text = string.Empty;
                config.saveButton.interactable = true;
            }
        }

        private void OnInputValueChanged(string value, ProfileNameEditorView.NonClaimedNameConfig config)
        {
            config.characterCountLabel.text = $"{value.Length}/{config.input.characterLimit}";
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
