using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fullscreen panel shown before the world starts loading and, later, on demand during gameplay.
    ///     It only reports the close intent (Jump in or Close); what happens next is up to the caller.
    /// </summary>
    public class LobbyController : ControllerBase<LobbyView, LobbyParameter>
    {
        private readonly IInputBlock inputBlock;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly LobbyAvatarSettings avatarSettings;
        private readonly World world;

        private LobbyCharacterPreviewController? avatarPreview;
        private CancellationTokenSource? avatarCts;
        private UniTaskCompletionSource? closeIntent;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        // Before the world is loaded Jump in is the only way out; once in-world Escape behaves like any other fullscreen panel.
        public override bool CanBeClosedByEscape => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed;

        public LobbyController(ViewFactoryMethod viewFactory,
            IInputBlock inputBlock,
            IReadOnlyLoadingStatus loadingStatus,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            ProfileChangesBus profileChangesBus,
            ICharacterPreviewFactory characterPreviewFactory,
            CharacterPreviewEventBus characterPreviewEventBus,
            LobbyAvatarSettings avatarSettings,
            World world) : base(viewFactory)
        {
            this.inputBlock = inputBlock;
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.profileChangesBus = profileChangesBus;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.avatarSettings = avatarSettings;
            this.world = world;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (viewInstance != null)
            {
                viewInstance.JumpInButton.onClick.RemoveListener(RequestClose);
                viewInstance.CloseButton.onClick.RemoveListener(RequestClose);
                viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent -= OnAvatarClicked;
            }

            avatarCts.SafeCancelAndDispose();
            avatarPreview?.Dispose();
            closeIntent?.TrySetCanceled();
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.JumpInButton.onClick.AddListener(RequestClose);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
            viewInstance.CharacterPreviewView.CharacterPreviewInputDetector.OnPointerClickEvent += OnAvatarClicked;

            avatarPreview = new LobbyCharacterPreviewController(viewInstance.CharacterPreviewView, avatarSettings, characterPreviewFactory, world, characterPreviewEventBus);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            viewInstance!.CloseButton.gameObject.SetActive(!inputData.IsStartup);
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);

            avatarCts = avatarCts.SafeRestart();
            ShowAvatarAsync(avatarCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            avatarCts.SafeCancelAndDispose();
            avatarPreview!.OnHide();

            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent?.TrySetCanceled(ct);
            closeIntent = new UniTaskCompletionSource();
            await closeIntent.Task.AttachExternalCancellation(ct);
        }

        private async UniTaskVoid ShowAvatarAsync(CancellationToken ct)
        {
            try
            {
                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (ct.IsCancellationRequested) return;

                if (profile == null)
                {
                    ReportHub.LogWarning(ReportCategory.PROFILE, "Own profile is not available, the lobby avatar is not shown");
                    return;
                }

                avatarPreview!.Initialize(profile.Avatar, CharacterPreviewUtils.LOBBY_PREVIEW_POSITION);
                avatarPreview.OnBeforeShow();
                avatarPreview.OnShow();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.PROFILE); }
        }

        private void OnProfileUpdated(Profile profile) =>
            avatarPreview!.Refresh(profile.Avatar);

        // Until the backpack gets its own modal the Explore panel takes over; being fullscreen it also closes this panel.
        private void OnAvatarClicked(PointerEventData _) =>
            mvcManager.ShowAndForget(ExplorePanelController.IssueCommand(new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Avatar)));

        private void RequestClose()
        {
            closeIntent?.TrySetResult();
            closeIntent = null;
        }
    }

    public readonly struct LobbyParameter
    {
        /// <summary>
        ///     True when the lobby gates the in-app initialization flow (first show of the session), false when the user opened it on demand in-world.
        ///     At startup Jump in is the only way out, so no close button is offered.
        /// </summary>
        public readonly bool IsStartup;

        public LobbyParameter(bool isStartup)
        {
            IsStartup = isStartup;
        }
    }
}
