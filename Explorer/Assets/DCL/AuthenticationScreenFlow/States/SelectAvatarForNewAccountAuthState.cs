using DCL.AvatarRendering.Loading.Components;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.Utilities;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.AuthenticationScreenFlow
{
    public class SelectAvatarForNewAccountAuthState : AuthStateBase, IPayloadedState<(Profile profile, string email, bool isRestoredSession, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> fsm;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly SelectAvatarForNewAccountAuthView view;
        private readonly CharacterPreviewView characterPreviewView;
        private readonly Vector3 characterPreviewOrigPosition;
        private readonly AvatarPresetProvider avatarPresetProvider = new ();

        private BodyShape selectedBodyType = BodyShape.MALE;
        private int selectedPresetSlot;

        private Profile newUserProfile = null!;
        private string userEmail = string.Empty;
        private bool isRestoredSession;
        private CancellationToken loginCt;

        public SelectAvatarForNewAccountAuthState(
            MVCStateMachine<AuthStateBase> fsm,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            view = viewInstance.SelectAvatarForNewAccountAuthView;
            characterPreviewView = viewInstance.CharacterPreviewView;
            characterPreviewOrigPosition = characterPreviewView.transform.localPosition;

            this.fsm = fsm;
            this.controller = controller;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;
        }

        public void Enter((Profile profile, string email, bool isRestoredSession, CancellationToken ct) payload)
        {
            base.Enter();

            newUserProfile = payload.profile;
            userEmail = payload.email;
            isRestoredSession = payload.isRestoredSession;
            loginCt = payload.ct;

            selectedBodyType = BodyShape.MALE;
            selectedPresetSlot = 0;

            currentState.Value = AuthStatus.AvatarSelection;

            view.Show();

            characterPreviewView.transform.SetParent(view.transform);
            characterPreviewView.transform.SetAsFirstSibling();
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;

            for (var slot = 0; slot < view.MaleAvatarPresets.Length; slot++)
            {
                int presetSlot = slot;
                view.MaleAvatarPresets[slot].onClick.AddListener(() => SelectPreset(BodyShape.MALE, presetSlot));
            }

            for (var slot = 0; slot < view.FemaleAvatarPresets.Length; slot++)
            {
                int presetSlot = slot;
                view.FemaleAvatarPresets[slot].onClick.AddListener(() => SelectPreset(BodyShape.FEMALE, presetSlot));
            }

            view.BackButton.onClick.AddListener(OnBackButtonClicked);
            view.ContinueButton.onClick.AddListener(OnContinueButtonClicked);

            // Body type selector
            view.BodyTypeDropdownButton.onClick.AddListener(ToggleBodyTypeDropdown);
            view.BodyTypeOptionA.onClick.AddListener(() => SelectBodyType(BodyShape.MALE));
            view.BodyTypeOptionB.onClick.AddListener(() => SelectBodyType(BodyShape.FEMALE));
            view.SetBodyTypeDropdownOpen(false);
            view.UpdateBodyTypeUi(selectedBodyType.Equals(BodyShape.MALE));

            UpdateSelectedSlot();
            UpdateCharacterPreview(avatarPresetProvider.Get(selectedBodyType, selectedPresetSlot));
        }

        public override void Exit()
        {
            characterPreviewController.OnHide();

            // Returned to the screen root here instead of on the hide animation, so it cannot land after the
            // next state has already parented the preview to its own view
            characterPreviewView.transform.SetParent(viewInstance.transform);
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;

            foreach (Button preset in view.MaleAvatarPresets)
                preset.onClick.RemoveAllListeners();

            foreach (Button preset in view.FemaleAvatarPresets)
                preset.onClick.RemoveAllListeners();

            view.BackButton.onClick.RemoveAllListeners();
            view.ContinueButton.onClick.RemoveAllListeners();

            view.BodyTypeDropdownButton.onClick.RemoveAllListeners();
            view.BodyTypeOptionA.onClick.RemoveAllListeners();
            view.BodyTypeOptionB.onClick.RemoveAllListeners();
            view.SetBodyTypeDropdownOpen(false);

            base.Exit();
        }

        private void SelectPreset(BodyShape bodyShape, int slot)
        {
            selectedBodyType = bodyShape;
            selectedPresetSlot = slot;

            UpdateSelectedSlot();
            UpdateCharacterPreview(avatarPresetProvider.Get(bodyShape, slot));
        }

        private void UpdateSelectedSlot()
        {
            Button[] presets = selectedBodyType.Equals(BodyShape.FEMALE) ? view.FemaleAvatarPresets : view.MaleAvatarPresets;

            view.MoveSelectedSlotTo(presets[Mathf.Min(selectedPresetSlot, presets.Length - 1)]);
        }

        private void ToggleBodyTypeDropdown() =>
            view.SetBodyTypeDropdownOpen(!view.BodyTypeDropdownPanel.activeSelf);

        private void SelectBodyType(BodyShape bodyShape)
        {
            selectedBodyType = bodyShape;
            view.SetBodyTypeDropdownOpen(false);
            view.UpdateBodyTypeUi(bodyShape.Equals(BodyShape.MALE));

            UpdateSelectedSlot();
            UpdateCharacterPreview(avatarPresetProvider.Get(selectedBodyType, selectedPresetSlot));
        }

        private void UpdateCharacterPreview(Avatar newAvatar)
        {
            newUserProfile.Avatar = newAvatar;
            characterPreviewController.Initialize(newAvatar, CharacterPreviewUtils.AUTH_SCREEN_PREVIEW_POSITION);
            characterPreviewController.OnBeforeShow();
            characterPreviewController.OnShow();
        }

        private void OnBackButtonClicked()
        {
            view.Hide();
            controller.ChangeAccount();
        }

        private void OnContinueButtonClicked()
        {
            controller.RaiseAvatarSelected(selectedBodyType.ToString(), selectedPresetSlot);

            view.Hide();
            fsm.Enter<LobbyForNewAccountAuthState, (Profile, string, bool, CancellationToken)>((newUserProfile, userEmail, isRestoredSession, loginCt));
        }
    }
}
