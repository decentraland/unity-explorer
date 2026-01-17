using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using Random = UnityEngine.Random;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class NewAccountLobbyAuthState : AuthStateBase, IPayloadedState<(Profile profile, bool isCached, CancellationToken ct)>
    {
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly ISelfProfile selfProfile;
        private readonly NewAccountLobbyScreenSubView newAccountSubView;

        private readonly IWearablesProvider wearablesProvider;

        private readonly List<Avatar> avatarHistory = new ();

        private Dictionary<string, List<URN>>? maleWearablesByCategory;
        private Dictionary<string, List<URN>>? femaleWearablesByCategory;

        private int currentAvatarIndex = -1;
        private bool baseWearablesLoaded;

        public NewAccountLobbyAuthState(
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController,
            ISelfProfile selfProfile,
            IWearablesProvider wearablesProvider) : base(viewInstance)
        {
            newAccountSubView = viewInstance.NewAccountLobbyScreenSubView;

            this.controller = controller;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;
            this.selfProfile = selfProfile;
            this.wearablesProvider = wearablesProvider;
        }

        private Profile newUserProfile;

        private CancellationToken ct;

        public void Enter((Profile profile, bool isCached, CancellationToken ct) payload)
        {
            ct = payload.ct;

            InitializeAvatarAsync().Forget();

            newAccountSubView.PrevRandomButton.interactable = false;
            newAccountSubView.NextRandomButton.interactable = false;

            currentState.Value = payload.isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;

            newUserProfile = payload.profile;

            newAccountSubView.gameObject.SetActive(true);

            newAccountSubView.ProfileNameInputField.NameValidityChanged += OnProfileNameChanged;

            newAccountSubView.FinalizeNewUserButton.onClick.AddListener(FinalizeNewUser);
            newAccountSubView.RandomizeButton.onClick.AddListener(RandomizeAvatar);
            newAccountSubView.PrevRandomButton.onClick.AddListener(PrevRandomAvatar);
            newAccountSubView.NextRandomButton.onClick.AddListener(NextRandomAvatar);

            // Toggle listeners for terms agreement
            newAccountSubView.SubscribeToggle.SetIsOnWithoutNotify(false);
            newAccountSubView.TermsOfUse.SetIsOnWithoutNotify(false);

            newAccountSubView.SubscribeToggle.onValueChanged.AddListener(OnToggleChanged);
            newAccountSubView.TermsOfUse.onValueChanged.AddListener(OnToggleChanged);

            UpdateFinalizeButtonState();
        }

        public override void Exit()
        {
            newAccountSubView.gameObject.SetActive(false);
            newAccountSubView.NextRandomButton.interactable = false;

            characterPreviewController?.OnHide();

            newAccountSubView.ProfileNameInputField.NameValidityChanged -= OnProfileNameChanged;

            newAccountSubView.FinalizeNewUserButton.onClick.RemoveListener(FinalizeNewUser);
            newAccountSubView.RandomizeButton.onClick.RemoveListener(RandomizeAvatar);
            newAccountSubView.PrevRandomButton.onClick.RemoveListener(PrevRandomAvatar);
            newAccountSubView.NextRandomButton.onClick.RemoveListener(NextRandomAvatar);

            // Toggle listeners for terms agreement
            newAccountSubView.SubscribeToggle.onValueChanged.RemoveListener(OnToggleChanged);
            newAccountSubView.TermsOfUse.onValueChanged.RemoveListener(OnToggleChanged);

            newAccountSubView.SubscribeToggle.SetIsOnWithoutNotify(false);
            newAccountSubView.TermsOfUse.SetIsOnWithoutNotify(false);
        }

        private async UniTask InitializeAvatarAsync()
        {
            await LoadBaseWearablesAsync(ct);
            RandomizeAvatar();

            characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

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
            PublishNewProfile(ct).Forget();

            async UniTaskVoid PublishNewProfile(CancellationToken ct)
            {
                newUserProfile.Name = newAccountSubView.ProfileNameInputField.CurrentNameText;
                Profile? publishedProfile = await selfProfile.UpdateProfileAsync(newUserProfile, ct, updateAvatarInWorld: false);
                newUserProfile = publishedProfile ?? throw new ProfileNotFoundException();
                JumpIntoWorld();
            }
        }

        private void RandomizeAvatar()
        {
            // If we're not at the end of history, remove all avatars after current position
            if (currentAvatarIndex < avatarHistory.Count - 1)
                avatarHistory.RemoveRange(currentAvatarIndex + 1, avatarHistory.Count - currentAvatarIndex - 1);

            // Create and add new avatar to history
            Avatar newAvatar = CreateDefaultAvatar();
            avatarHistory.Add(newAvatar);
            currentAvatarIndex = avatarHistory.Count - 1;

            // Apply to profile and preview
            newUserProfile.Avatar = newAvatar;
            characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnShow();

            UpdateAvatarNavigationButtons();
        }

        private void PrevRandomAvatar()
        {
            if (currentAvatarIndex <= 0)
                return;

            currentAvatarIndex--;
            ApplyAvatarFromHistory();
            UpdateAvatarNavigationButtons();
        }

        private void NextRandomAvatar()
        {
            if (currentAvatarIndex >= avatarHistory.Count - 1)
                return;

            currentAvatarIndex++;
            ApplyAvatarFromHistory();
            UpdateAvatarNavigationButtons();
        }

        private void ApplyAvatarFromHistory()
        {
            Avatar avatar = avatarHistory[currentAvatarIndex];
            newUserProfile.Avatar = avatar;
            characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnShow();
        }

        private void UpdateAvatarNavigationButtons()
        {
            if (viewInstance == null)
                return;

            newAccountSubView.PrevRandomButton.interactable = currentAvatarIndex > 0;
            newAccountSubView.NextRandomButton.interactable = currentAvatarIndex < avatarHistory.Count - 1;
        }

        private void OnToggleChanged(bool _) =>
            UpdateFinalizeButtonState();

        private void OnProfileNameChanged(bool _) =>
            UpdateFinalizeButtonState();

        private void UpdateFinalizeButtonState() =>
            newAccountSubView.FinalizeNewUserButton.interactable =
                newAccountSubView.ProfileNameInputField.IsValidName &&
                newAccountSubView.SubscribeToggle.isOn &&
                newAccountSubView.TermsOfUse.isOn;

        private Avatar CreateDefaultAvatar()
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
            // subView!.JumpIntoWorldButton.interactable = false;
            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                //Disabled animation until proper animation is setup, otherwise we get animation hash errors
                //viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.JUMP_IN);
                await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                characterPreviewController?.OnHide();

                controller.TrySetLifeCycle();
            }
        }
    }
}
