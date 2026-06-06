using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Utilities;
using DCL.WebRequests;
using MVC;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using Avatar = DCL.Profiles.Avatar;
using Random = UnityEngine.Random;

namespace DCL.AuthenticationScreenFlow
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
        private readonly ProfileChangesBus profileChangesBus;

        private Dictionary<string, List<URN>>? maleWearablesByCategory;
        private Dictionary<string, List<URN>>? femaleWearablesByCategory;

        private BodyShape selectedBodyType = BodyShape.MALE;

        private Profile newUserProfile;
        private string userEmail;
        private CancellationToken loginCt;

        private readonly CharacterPreviewView characterPreviewView;
        private readonly Vector3 characterPreviewOrigPosition;
        private IReadOnlyList<ITrimmedWearable>? loadedWearables;

        private static readonly HashSet<string> FEMALE_EXCLUDED_CATEGORIES = new ()
        {
            WearableCategories.Categories.FACIAL_HAIR,
        };

        private static readonly HashSet<string> OPTIONAL_CATEGORIES = new ()
        {
            WearableCategories.Categories.FACIAL_HAIR,
            WearableCategories.Categories.HAT,
            WearableCategories.Categories.MASK,
            WearableCategories.Categories.TIARA,
            WearableCategories.Categories.HELMET,
            WearableCategories.Categories.EARRING,
            WearableCategories.Categories.EYEWEAR,
            WearableCategories.Categories.TOP_HEAD,
            WearableCategories.Categories.HANDS_WEAR,
        };

        private const float OPTIONAL_CATEGORY_INCLUDE_CHANCE = 0.75f;

        public LobbyForNewAccountAuthState(MVCStateMachine<AuthStateBase> fsm,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController,
            ISelfProfile selfProfile,
            IWearablesProvider wearablesProvider,
            IWebBrowser webBrowser,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            ProfileChangesBus profileChangesBus) : base(viewInstance)
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
            this.profileChangesBus = profileChangesBus;

            characterPreviewView = viewInstance.CharacterPreviewView;
            characterPreviewOrigPosition = characterPreviewView.transform.localPosition;

            view.OnViewHidden += ReparentCharacterPreview;
        }

        public void Enter((Profile profile, string email, bool isCached, CancellationToken ct) payload)
        {
            base.Enter();

            loginCt = payload.ct;
            userEmail = payload.email;
            selectedBodyType = BodyShape.MALE;
            newUserProfile = payload.profile;

            InitializeAvatarAsync().Forget();

            controller.IsCurrentlyNewAccount = true;
            currentState.Value = payload.isCached ? AuthStatus.LoggedInCached : AuthStatus.LoggedIn;

            view.Show();
            characterPreviewView.transform.SetParent(view.transform);
            characterPreviewView.transform.SetAsFirstSibling();
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;

            view.ProfileNameInputField.InputValueChanged += OnProfileNameChanged;

            view.FinalizeNewUserButton.onClick.AddListener(FinalizeNewUser);
            view.BackButton.onClick.AddListener(OnBackButtonClicked);

            view.RandomizeButton.onClick.AddListener(OnRandomizeButtonPressed);

            // Body type selector
            view.BodyTypeDropdownButton.onClick.AddListener(ToggleBodyTypeDropdown);
            view.BodyTypeOptionA.onClick.AddListener(() => SelectBodyType(BodyShape.MALE));
            view.BodyTypeOptionB.onClick.AddListener(() => SelectBodyType(BodyShape.FEMALE));
            view.SetBodyTypeDropdownOpen(false);
            view.UpdateBodyTypeUI(selectedBodyType.Equals(BodyShape.MALE));

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
            characterPreviewController.OnHide();

            maleWearablesByCategory = null;
            femaleWearablesByCategory = null;

            // Listeners
            view.ProfileNameInputField.InputValueChanged -= OnProfileNameChanged;

            view.FinalizeNewUserButton.onClick.RemoveAllListeners();
            view.BackButton.onClick.RemoveAllListeners();

            view.RandomizeButton.onClick.RemoveAllListeners();
            view.BodyTypeDropdownButton.onClick.RemoveAllListeners();
            view.BodyTypeOptionA.onClick.RemoveAllListeners();
            view.BodyTypeOptionB.onClick.RemoveAllListeners();

            // Toggle listeners for terms agreement
            view.SubscribeToggle.onValueChanged.RemoveAllListeners();
            view.TermsOfUse.onValueChanged.RemoveAllListeners();

            view.SubscribeToggle.SetIsOnWithoutNotify(false);
            view.TermsOfUse.SetIsOnWithoutNotify(false);

            view.TermsOfUseAndPrivacyLink.OnLinkClicked -= OpenClickableURL;
            base.Exit();
        }

        private void ReparentCharacterPreview()
        {
            characterPreviewView.transform.SetParent(viewInstance.transform);
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;
        }

        private void OpenClickableURL(string url) =>
            webBrowser.OpenUrl(url);

        private async UniTask InitializeAvatarAsync()
        {
            try
            {
                loadedWearables ??= await LoadBaseWearablesAsync(loginCt);

                if (loadedWearables != null)
                    PopulateWearablesCatalogs(loadedWearables);
                UpdateCharacterPreview(CreateRandomAvatar());
            }
            catch (OperationCanceledException)
            { /* Expected on cancellation */
            }
        }

        private async UniTask<IReadOnlyList<ITrimmedWearable>?> LoadBaseWearablesAsync(CancellationToken ct)
        {
            try
            {
                // Load base wearables catalog from backend (pageSize 300 to get all)
                (IReadOnlyList<ITrimmedWearable> wearables, _) = await wearablesProvider.GetTrimmedByParamsAsync(
                    new IWearablesProvider.Params(300, 1)
                    {
                        CollectionType = IWearablesProvider.CollectionType.Base
                    },
                    ct);

                ReportHub.Log(ReportCategory.AUTHENTICATION, $"Base wearables catalog loaded: {wearables.Count} items");
                return wearables;
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw to be handled by caller
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
            }

            return null;
        }

        private void PopulateWearablesCatalogs(IReadOnlyList<ITrimmedWearable> wearables)
        {
            maleWearablesByCategory = new Dictionary<string, List<URN>>();
            femaleWearablesByCategory = new Dictionary<string, List<URN>>();

            foreach (ITrimmedWearable? wearable in wearables)
            {
                string category = wearable.GetCategory();

                if (category == WearableCategories.Categories.BODY_SHAPE)
                    continue;

                URN urn = wearable.GetUrn();

                if (wearable.IsCompatibleWithBodyShape(BodyShape.MALE)
                    && !HasBodyTypePrefix(urn, "f_"))
                {
                    if (!maleWearablesByCategory.ContainsKey(category))
                        maleWearablesByCategory[category] = new List<URN>();

                    maleWearablesByCategory[category].Add(urn);
                }

                if (wearable.IsCompatibleWithBodyShape(BodyShape.FEMALE)
                    && !FEMALE_EXCLUDED_CATEGORIES.Contains(category)
                    && !HasBodyTypePrefix(urn, "m_"))
                {
                    if (!femaleWearablesByCategory.ContainsKey(category))
                        femaleWearablesByCategory[category] = new List<URN>();

                    femaleWearablesByCategory[category].Add(urn);
                }
            }

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Base wearables catalogs populated: male categories: {maleWearablesByCategory.Count}, female categories: {femaleWearablesByCategory.Count}");
        }

        private static bool HasBodyTypePrefix(URN urn, string prefix)
        {
            string urnStr = urn.ToString();
            int lastColon = urnStr.LastIndexOf(':');

            if (lastColon < 0 || lastColon >= urnStr.Length - 1)
                return false;

            return urnStr.AsSpan(lastColon + 1).StartsWith(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        private void OnBackButtonClicked()
        {
            view.Hide(UIAnimationHashes.SLIDE);
            controller.ChangeAccount();
        }

        private void UpdateCharacterPreview(Avatar newAvatar)
        {
            newUserProfile.Avatar = newAvatar;
            characterPreviewController.Initialize(newAvatar, CharacterPreviewUtils.AUTH_SCREEN_PREVIEW_POSITION);
            characterPreviewController.OnBeforeShow();
            characterPreviewController.OnShow();
        }

        private void OnRandomizeButtonPressed()
        {
            UpdateCharacterPreview(CreateRandomAvatar());
        }

        private void ToggleBodyTypeDropdown()
        {
            bool isOpen = !view.BodyTypeDropdownPanel.activeSelf;
            view.SetBodyTypeDropdownOpen(isOpen);
        }

        private void SelectBodyType(BodyShape bodyShape)
        {
            selectedBodyType = bodyShape;
            view.SetBodyTypeDropdownOpen(false);
            view.UpdateBodyTypeUI(bodyShape.Equals(BodyShape.MALE));
            // Regenerate avatar with the new body type
            UpdateCharacterPreview(CreateRandomAvatar());
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
            BodyShape bodyShape = selectedBodyType;

            // If base wearables loaded from backend - use randomizer
            if (loadedWearables != null && loadedWearables.Count > 0)
            {
                Dictionary<string, List<URN>> wearablesByCategory = bodyShape.Equals(BodyShape.MALE)
                    ? maleWearablesByCategory!
                    : femaleWearablesByCategory!;

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

            foreach (KeyValuePair<string, List<URN>> kvp in wearablesByCategory)
            {
                if (kvp.Value.Count == 0)
                    continue;

                if (OPTIONAL_CATEGORIES.Contains(kvp.Key) && Random.value > OPTIONAL_CATEGORY_INCLUDE_CHANCE)
                    continue;

                result.Add(kvp.Value[Random.Range(0, kvp.Value.Count)]);
            }

            return result;
        }

        private void FinalizeNewUser()
        {
            view.FinalizeNewUserButton.interactable = false;
            view.BackButton.interactable = false;

            if (view.SubscribeToggle.isOn && !string.IsNullOrEmpty(userEmail))
                SubscribeToNewsletterAsync(userEmail).Forget();

            PublishNewProfileAsync(loginCt).Forget();

            return;

            async UniTaskVoid PublishNewProfileAsync(CancellationToken ct)
            {
                try
                {
                    newUserProfile.Name = view.ProfileNameInputField.Text;
                    Profile? publishedProfile = await selfProfile.UpdateProfileAsync(newUserProfile, ct, updateAvatarInWorld: false);
                    newUserProfile = publishedProfile ?? throw new ProfileNotFoundException();

                    // Notify profile-bus subscribers (sidebar thumbnail, explore panel, chat) that the
                    // freshly created profile is live
                    profileChangesBus.PushUpdate(newUserProfile);

                    // Mark the analytics-visible end of the onboarding step. Anything between
                    // LOGGED_IN (avatar customization shown) and PROFILE_FINALIZED is the user
                    // setting up their account.
                    controller.RaiseProfileFinalized();

                    await characterPreviewController.PlayJumpInEmoteAndAwaitItAsync();

                    view.Hide(UIAnimationHashes.OUT);
                    await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                    characterPreviewController.OnHide();

                    fsm.Enter<InitAuthState>();
                    controller.TrySetLifeCycle();
                }
                catch (OperationCanceledException)
                { /* Expected on cancellation */
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    spanErrorInfo = new SpanErrorInfo("Exception on finalizing new user", e);

                    view.Hide(UIAnimationHashes.SLIDE);
                    fsm.Enter<LoginSelectionAuthState, ErrorType>(ErrorType.CONNECTION_ERROR);
                }
            }
        }

        private async UniTaskVoid SubscribeToNewsletterAsync(string email)
        {
            try
            {
                string url = decentralandUrlsSource.Url(DecentralandUrl.BuilderApiNewsletter);
                var jsonBody = $"{{\"email\":\"{email}\",\"source\":\"auth\"}}";

                await webRequestController.PostAsync(
                                               new CommonArguments(URLAddress.FromString(url)),
                                               GenericPostArguments.CreateJson(jsonBody),
                                               CancellationToken.None, // no cancellation for newsletter subscription
                                               ReportCategory.AUTHENTICATION)
                                          .WithNoOpAsync();
            }
            catch (OperationCanceledException)
            { /* Ignore cancellation */
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.AUTHENTICATION);
            }
        }
    }
}
