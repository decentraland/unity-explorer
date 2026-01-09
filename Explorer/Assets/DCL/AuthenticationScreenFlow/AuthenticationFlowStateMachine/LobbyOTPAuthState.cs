using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Utilities;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyOTPAuthState : AuthStateBase, IPayloadedState<(Profile profile, bool isCached, bool baseWearablesLoaded, CancellationToken ct)>
    {
        private readonly StringVariable profileNameLabel;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileFetchingOTPAuthState profileFetchingState;
        private bool baseWearablesLoaded;

        public LobbyOTPAuthState(
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController,
            ISelfProfile selfProfile,
            ProfileFetchingOTPAuthState profileFetchingState) : base(viewInstance)
        {
            this.controller = controller;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;
            this.selfProfile = selfProfile;
            this.profileFetchingState = profileFetchingState;

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["back_profileName"];
        }

        private Profile newUserProfile;
        private CancellationToken ct;

        public void Enter((Profile profile, bool isCached, bool baseWearablesLoaded, CancellationToken ct) payload)
        {
            viewInstance.NextRandomButton.enabled = false;
            baseWearablesLoaded = payload.baseWearablesLoaded;
            ct = ct;

            currentState.Value = payload.isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;

            newUserProfile = payload.profile;

            profileNameLabel!.Value = IsNewUser() ? newUserProfile.Name : "back " + newUserProfile.Name;

            viewInstance.FinalizeContainer.SetActive(true);

            viewInstance.FinalizeAnimator.ResetAnimator();
            viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);

            viewInstance.JumpIntoWorldButton.interactable = true;

            characterPreviewController?.Initialize(newUserProfile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

            viewInstance.JumpIntoWorldButton.gameObject.SetActive(false);
            viewInstance.ProfileNameLabel.gameObject.SetActive(false);
            viewInstance.Description.SetActive(false);
            viewInstance.DiffAccountButton.SetActive(false);

            viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);
            viewInstance.FinalizeContainer.SetActive(true);
            viewInstance.NewUserContainer.SetActive(true);

            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

            viewInstance.FinalizeNewUserButton.onClick.AddListener(FinalizeNewUser);
            viewInstance.RandomizeButton.onClick.AddListener(RandomizeAvatar);
            viewInstance.PrevRandomButton.onClick.AddListener(PrevRandomAvatar);
            viewInstance.NextRandomButton.onClick.AddListener(NextRandomAvatar);

            // Toggle listeners for terms agreement
            viewInstance.SubscribeToggle.SetIsOnWithoutNotify(false);
            viewInstance.AgreeLicenseToggle.SetIsOnWithoutNotify(false);
            viewInstance.SubscribeToggle.onValueChanged.AddListener(OnToggleChanged);
            viewInstance.AgreeLicenseToggle.onValueChanged.AddListener(OnToggleChanged);
            viewInstance.ProfileNameInputField.onValueChanged.AddListener(OnProfileNameChanged);
            UpdateFinalizeButtonState();

            return;

            bool IsNewUser() =>
                newUserProfile.Version == 1;
        }

        public override void Exit()
        {
            viewInstance.NextRandomButton.enabled = false;

            viewInstance!.FinalizeContainer.SetActive(false);
            viewInstance.NewUserContainer.SetActive(false);

            characterPreviewController?.OnHide();

            viewInstance.FinalizeNewUserButton.onClick.RemoveListener(FinalizeNewUser);
            viewInstance.RandomizeButton.onClick.RemoveListener(RandomizeAvatar);
            viewInstance.PrevRandomButton.onClick.RemoveListener(PrevRandomAvatar);
            viewInstance.NextRandomButton.onClick.RemoveListener(NextRandomAvatar);
            viewInstance.SubscribeToggle.onValueChanged.RemoveListener(OnToggleChanged);
            viewInstance.AgreeLicenseToggle.onValueChanged.RemoveListener(OnToggleChanged);
            viewInstance.ProfileNameInputField.onValueChanged.RemoveListener(OnProfileNameChanged);
            viewInstance.SubscribeToggle.SetIsOnWithoutNotify(false);
            viewInstance.AgreeLicenseToggle.SetIsOnWithoutNotify(false);
        }

        private readonly List<Avatar> avatarHistory = new ();
        private int currentAvatarIndex = -1;

        private void FinalizeNewUser()
        {
            PublishNewProfile(ct).Forget();

            async UniTaskVoid PublishNewProfile(CancellationToken ct)
            {
                newUserProfile.Name = viewInstance.ProfileNameInputField.text;
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

            viewInstance.PrevRandomButton.interactable = currentAvatarIndex > 0;
            viewInstance.NextRandomButton.interactable = currentAvatarIndex < avatarHistory.Count - 1;
        }

        private void OnToggleChanged(bool _) =>
            UpdateFinalizeButtonState();

        private void OnProfileNameChanged(string _) =>
            UpdateFinalizeButtonState();

        private void UpdateFinalizeButtonState()
        {
            viewInstance.FinalizeNewUserButton.interactable =
                viewInstance.SubscribeToggle.isOn &&
                viewInstance.AgreeLicenseToggle.isOn &&
                !string.IsNullOrWhiteSpace(viewInstance.ProfileNameInputField.text);
        }

        private Avatar CreateDefaultAvatar()
        {
            BodyShape bodyShape = UnityEngine.Random.value > 0.5f ? BodyShape.MALE : BodyShape.FEMALE;

            // If base wearables loaded from backend - use randomizer
            if (baseWearablesLoaded && profileFetchingState.maleWearablesByCategory != null && profileFetchingState.femaleWearablesByCategory != null)
            {
                Dictionary<string, List<URN>>? wearablesByCategory = bodyShape.Equals(BodyShape.MALE)
                    ? profileFetchingState.maleWearablesByCategory
                    : profileFetchingState.femaleWearablesByCategory;

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
                    result.Add(categoryWearables[UnityEngine.Random.Range(0, categoryWearables.Count)]);
            }

            return result;
        }

        private void JumpIntoWorld()
        {
            viewInstance!.JumpIntoWorldButton.interactable = false;
            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                //Disabled animation until proper animation is setup, otherwise we get animation hash errors
                //viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.JUMP_IN);
                await UniTask.Delay(ANIMATION_DELAY);
                characterPreviewController?.OnHide();

                controller.TrySetLifeCycle();
            }
        }
    }
}
