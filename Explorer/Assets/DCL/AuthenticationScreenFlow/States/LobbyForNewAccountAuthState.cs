using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Browser;
using DCL.Browser.DecentralandUrls;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Utilities;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using Avatar = DCL.Profiles.Avatar;
using Random = UnityEngine.Random;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyForNewAccountAuthState : AuthStateBase, IPayloadedState<(Profile profile, string email, bool isCached, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> fsm;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly ISelfProfile selfProfile;
        private readonly LobbyForNewAccountAuthView view;

        private readonly IWearablesProvider wearablesProvider;
        private readonly IWebBrowser webBrowser;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private readonly List<Avatar> avatarHistory = new ();

        private Dictionary<string, List<URN>>? maleWearablesByCategory;
        private Dictionary<string, List<URN>>? femaleWearablesByCategory;

        private int currentAvatarIndex = -1;
        private bool baseWearablesLoaded;

        private Profile newUserProfile;
        private string userEmail;
        private CancellationToken ct;

        private readonly CharacterPreviewView characterPreviewView;
        private readonly Vector3 characterPreviewOrigPosition;

        public LobbyForNewAccountAuthState(MVCStateMachine<AuthStateBase> fsm,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController,
            ISelfProfile selfProfile,
            IWearablesProvider wearablesProvider,
            IWebBrowser webBrowser,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource) : base(viewInstance)
        {
            view = viewInstance.LobbyForNewAccountAuthView;

            this.fsm = fsm;
            this.controller = controller;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;
            this.selfProfile = selfProfile;
            this.wearablesProvider = wearablesProvider;
            this.webBrowser = webBrowser;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;

            characterPreviewView = viewInstance.CharacterPreviewView;
            characterPreviewOrigPosition = characterPreviewView.transform.localPosition;

            view.OnViewHidden += ReparentCharacterPreview;
        }

        private void ReparentCharacterPreview()
        {
            characterPreviewView.transform.SetParent(viewInstance.transform);
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;
        }

        public void Enter((Profile profile, string email, bool isCached, CancellationToken ct) payload)
        {
            ct = payload.ct;
            userEmail = payload.email;

            InitializeAvatarAsync().Forget();

            view.PrevRandomButton.interactable = false;
            view.NextRandomButton.interactable = false;

            currentState.Value = payload.isCached ? AuthStatus.LoggedInCached : AuthStatus.LoggedIn;

            newUserProfile = payload.profile;

            view.Show();
            characterPreviewView.transform.SetParent(view.transform);
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;

            view.ProfileNameInputField.InputValueChanged += OnProfileNameChanged;

            view.FinalizeNewUserButton.onClick.AddListener(FinalizeNewUser);
            view.BackButton.onClick.AddListener(OnBackButtonClicked);

            view.RandomizeButton.onClick.AddListener(OnRandomizeButtonPressed);
            view.PrevRandomButton.onClick.AddListener(PrevRandomAvatar);
            view.NextRandomButton.onClick.AddListener(NextRandomAvatar);

            // Toggle listeners for terms agreement
            view.SubscribeToggle.SetIsOnWithoutNotify(false);
            view.TermsOfUse.SetIsOnWithoutNotify(false);

            view.SubscribeToggle.onValueChanged.AddListener(OnToggleChanged);
            view.TermsOfUse.onValueChanged.AddListener(OnToggleChanged);

            view.TermsOfUseAndPrivacyLink.OnLinkClicked += OpenClickableURL;

            UpdateFinalizeButtonState();
        }

        public override void Exit()
        {
            characterPreviewController?.OnHide();

            // Listeners
            view.ProfileNameInputField.InputValueChanged -= OnProfileNameChanged;

            view.FinalizeNewUserButton.onClick.RemoveAllListeners();
            view.BackButton.onClick.RemoveAllListeners();

            view.RandomizeButton.onClick.RemoveAllListeners();
            view.PrevRandomButton.onClick.RemoveAllListeners();
            view.NextRandomButton.onClick.RemoveAllListeners();

            // Toggle listeners for terms agreement
            view.SubscribeToggle.onValueChanged.RemoveAllListeners();
            view.TermsOfUse.onValueChanged.RemoveAllListeners();

            view.SubscribeToggle.SetIsOnWithoutNotify(false);
            view.TermsOfUse.SetIsOnWithoutNotify(false);

            view.TermsOfUseAndPrivacyLink.OnLinkClicked -= OpenClickableURL;
        }

        private void OpenClickableURL(string url) =>
            webBrowser.OpenUrl(url);

        private async UniTask InitializeAvatarAsync()
        {
            await LoadBaseWearablesAsync(ct);
            UpdateCharacterPreview(RandomizeAvatar());
            UpdateAvatarNavigationButtons();
            InitializeAvatarHistory(newUserProfile.Avatar);
        }

        private async UniTask LoadBaseWearablesAsync(CancellationToken ct)
        {
            if (baseWearablesLoaded)
                return;

            try
            {
                // Load base wearables catalog from backend (pageSize 300 to get all)
                (IReadOnlyList<ITrimmedWearable> wearables, _) = await wearablesProvider.GetAsync(
                    pageSize: 300,
                    pageNumber: 1,
                    ct,
                    collectionType: IWearablesProvider.CollectionType.Base);

                maleWearablesByCategory = new Dictionary<string, List<URN>>();
                femaleWearablesByCategory = new Dictionary<string, List<URN>>();

                foreach (ITrimmedWearable? wearable in wearables)
                {
                    string category = wearable.GetCategory();

                    // Skip body shapes
                    if (category == "body_shape")
                        continue;

                    // Add to male dictionary if compatible
                    if (wearable.IsCompatibleWithBodyShape(BodyShape.MALE))
                    {
                        if (!maleWearablesByCategory.ContainsKey(category))
                            maleWearablesByCategory[category] = new List<URN>();

                        maleWearablesByCategory[category].Add(wearable.GetUrn());
                    }

                    // Add to female dictionary if compatible
                    if (wearable.IsCompatibleWithBodyShape(BodyShape.FEMALE))
                    {
                        if (!femaleWearablesByCategory.ContainsKey(category))
                            femaleWearablesByCategory[category] = new List<URN>();

                        femaleWearablesByCategory[category].Add(wearable.GetUrn());
                    }
                }

                baseWearablesLoaded = true;
                ReportHub.Log(ReportCategory.AUTHENTICATION, $"Base wearables catalog loaded: {wearables.Count} items, male categories: {maleWearablesByCategory.Count}, female categories: {femaleWearablesByCategory.Count}");
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                // Fallback to hardcoded defaults will be used
                baseWearablesLoaded = false;
            }
        }

        private void InitializeAvatarHistory(Avatar initialAvatar)
        {
            avatarHistory.Clear();
            avatarHistory.Add(initialAvatar);
            currentAvatarIndex = 0;
            UpdateAvatarNavigationButtons();
        }

        private void FinalizeNewUser()
        {
            JumpIntoWorld();
            PublishNewProfile(ct).Forget();

            if (view.SubscribeToggle.isOn && !string.IsNullOrEmpty(userEmail))
                SubscribeToNewsletterAsync(userEmail, ct).Forget();

            return;

            async UniTaskVoid PublishNewProfile(CancellationToken ct)
            {
                newUserProfile.Name = view.ProfileNameInputField.Text;
                Profile? publishedProfile = await selfProfile.UpdateProfileAsync(newUserProfile, ct, updateAvatarInWorld: false);
                newUserProfile = publishedProfile ?? throw new ProfileNotFoundException();
            }
        }

        private async UniTaskVoid SubscribeToNewsletterAsync(string email, CancellationToken ct)
        {
            try
            {
                string url = decentralandUrlsSource.Url(DecentralandUrl.BuilderApiNewsletter);
                var jsonBody = $"{{\"email\":\"{email}\",\"source\":\"auth\"}}";

                await webRequestController.PostAsync(
                                               new CommonArguments(URLAddress.FromString(url)),
                                               GenericPostArguments.CreateJson(jsonBody),
                                               ct,
                                               ReportCategory.AUTHENTICATION)
                                          .WithNoOpAsync();
            }
            catch (OperationCanceledException)
            { /* Ignore cancellation */
            }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.AUTHENTICATION); }
        }

        private void OnBackButtonClicked()
        {
            view.Hide(UIAnimationHashes.SLIDE);
            controller.ChangeAccount();
        }

        private Avatar RandomizeAvatar()
        {
            // If we're not at the end of history, remove all avatars after current position
            if (currentAvatarIndex < avatarHistory.Count - 1)
                avatarHistory.RemoveRange(currentAvatarIndex + 1, avatarHistory.Count - currentAvatarIndex - 1);

            // Create and add new avatar to history
            Avatar newAvatar = CreateRandomAvatar();
            avatarHistory.Add(newAvatar);
            currentAvatarIndex = avatarHistory.Count - 1;

            return newAvatar;
        }

        private void UpdateCharacterPreview(Avatar newAvatar)
        {
            newUserProfile.Avatar = newAvatar;
            characterPreviewController?.Initialize(newAvatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();
        }

        private void OnRandomizeButtonPressed()
        {
            UpdateCharacterPreview(RandomizeAvatar());
            UpdateAvatarNavigationButtons();
        }

        private void PrevRandomAvatar()
        {
            if (currentAvatarIndex <= 0)
                return;
            currentAvatarIndex--;

            UpdateCharacterPreview(avatarHistory[currentAvatarIndex]);
            UpdateAvatarNavigationButtons();
        }

        private void NextRandomAvatar()
        {
            if (currentAvatarIndex >= avatarHistory.Count - 1)
                return;
            currentAvatarIndex++;

            UpdateCharacterPreview(avatarHistory[currentAvatarIndex]);
            UpdateAvatarNavigationButtons();
        }

        private void UpdateAvatarNavigationButtons()
        {
            if (viewInstance == null)
                return;

            view.PrevRandomButton.interactable = currentAvatarIndex > 0;
            view.NextRandomButton.interactable = currentAvatarIndex < avatarHistory.Count - 1;
        }

        private void OnToggleChanged(bool _) =>
            UpdateFinalizeButtonState();

        private void OnProfileNameChanged(bool _) =>
            UpdateFinalizeButtonState();

        private void UpdateFinalizeButtonState() =>
            view.FinalizeNewUserButton.interactable =
                view.ProfileNameInputField.IsValidName &&
                view.TermsOfUse.isOn;

        private Avatar CreateRandomAvatar()
        {
            BodyShape bodyShape = Random.value > 0.5f ? BodyShape.MALE : BodyShape.FEMALE;

            // If base wearables loaded from backend - use randomizer
            if (baseWearablesLoaded)
            {
                Dictionary<string, List<URN>>? wearablesByCategory = bodyShape.Equals(BodyShape.MALE)
                    ? maleWearablesByCategory
                    : femaleWearablesByCategory;

                HashSet<URN> wearablesSet = GetRandomWearablesFromCategories(wearablesByCategory);

                return new Avatar(
                    bodyShape,
                    wearablesSet,
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor());
            }

            // Fallback to hardcoded defaults
            return new Avatar(
                bodyShape,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(bodyShape),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());
        }

        private static HashSet<URN> GetRandomWearablesFromCategories(Dictionary<string, List<URN>> wearablesByCategory)
        {
            var result = new HashSet<URN>();

            foreach (List<URN>? categoryWearables in wearablesByCategory.Values)
            {
                if (categoryWearables.Count > 0)
                    result.Add(categoryWearables[Random.Range(0, categoryWearables.Count)]);
            }

            return result;
        }

        private void JumpIntoWorld()
        {
            view.FinalizeNewUserButton.interactable = false;

            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                view.Hide(UIAnimationHashes.OUT);
                await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                characterPreviewController?.OnHide();

                fsm.Enter<InitAuthState>();
                controller.TrySetLifeCycle();
            }
        }
    }
}
