using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenController : ControllerBase<AuthenticationScreenView>
    {
        private const int ANIMATION_DELAY = 300;

        private enum ViewState
        {
            Login,
            LoginInProgress,
            Loading,
            Finalize,
        }

        private const string DISCORD_LINK = "https://decentraland.org/discord/";
        private const string REQUEST_BETA_ACCESS_LINK = "https://68zbqa0m12c.typeform.com/to/y9fZeNWm";

        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly Animator splashScreenAnimator;
        private readonly IFeatureFlagsCache featureFlagsCache;

        private AuthenticationScreenCharacterPreviewController? characterPreviewController;
        private CancellationTokenSource? loginCancellationToken;
        private CancellationTokenSource? verificationCountdownCancellationToken;
        private UniTaskCompletionSource? lifeCycleTask;
        private StringVariable? profileNameLabel;
        private World? world;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public AuthenticationScreenController(
            ViewFactoryMethod viewFactory,
            IWeb3VerifiedAuthenticator web3Authenticator,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IWeb3IdentityCache storedIdentityProvider,
            ICharacterPreviewFactory characterPreviewFactory,
            Animator splashScreenAnimator,
            IFeatureFlagsCache featureFlagsCache)
            : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.storedIdentityProvider = storedIdentityProvider;
            this.characterPreviewFactory = characterPreviewFactory;
            this.splashScreenAnimator = splashScreenAnimator;
            this.featureFlagsCache = featureFlagsCache;
        }

        public override void Dispose()
        {
            base.Dispose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            characterPreviewController?.Dispose();
            web3Authenticator.SetVerificationListener(null);
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileNameLabel = (StringVariable)viewInstance.ProfileNameLabel.StringReference["profileName"];

            viewInstance.LoginButton.onClick.AddListener(StartLoginFlowUntilEnd);
            viewInstance.CancelAuthenticationProcess.onClick.AddListener(CancelLoginProcess);
            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);

            foreach (Button button in viewInstance.UseAnotherAccountButton)
                button.onClick.AddListener(ChangeAccount);

            viewInstance.VerificationCodeHintButton.onClick.AddListener(OpenOrCloseVerificationCodeHint);
            viewInstance.DiscordButton.onClick.AddListener(OpenDiscord);
            viewInstance.RequestAlphaAccessButton.onClick.AddListener(RequestAlphaAccess);
            viewInstance.VersionText.text = Application.version;
#if UNITY_EDITOR
            viewInstance.VersionText.text = "editor-version";
#endif

            Assert.IsNotNull(world);
            characterPreviewController = new AuthenticationScreenCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world!);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            CheckValidIdentityAndStartInitialFlowAsync().Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            CancelLoginProcess();
            CancelVerificationCountdown();
            viewInstance.FinalizeContainer.SetActive(false);
            web3Authenticator.SetVerificationListener(null);
        }

        private async UniTaskVoid CheckValidIdentityAndStartInitialFlowAsync()
        {
            IWeb3Identity? storedIdentity = storedIdentityProvider.Identity;

            if (storedIdentity is { IsExpired: false })
            {
                CancelLoginProcess();
                loginCancellationToken = new CancellationTokenSource();

                try
                {
                    if (IsUserAllowedToAccessToBeta(storedIdentity))
                    {
                        await FetchProfileAsync(loginCancellationToken.Token);
                        SwitchState(ViewState.Finalize);
                    }
                    else
                    {
                        SwitchState(ViewState.Login);
                        ShowRestrictedUserPopup();
                    }
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    SwitchState(ViewState.Login);
                }
            }
            else
                SwitchState(ViewState.Login);

            splashScreenAnimator.SetTrigger(AnimationHashes.OUT);
        }

        private void ShowRestrictedUserPopup()
        {
            viewInstance.RestrictedUserContainer.SetActive(true);
        }

        private bool IsUserAllowedToAccessToBeta(IWeb3Identity storedIdentity)
        {
            if (featureFlagsCache.Configuration == null) return true;
            if (!featureFlagsCache.Configuration.IsEnabled("user-allow-list", "wallets")) return true;
            if (!featureFlagsCache.Configuration.TryGetCsvPayload("user-allow-list", "wallets", out List<List<string>>? allowedUsersCsv))
                return true;

            bool isUserAllowed = allowedUsersCsv![0]
               .Exists(s => new Web3Address(s).Equals(storedIdentity.Address));

            return isUserAllowed;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            (lifeCycleTask ??= new UniTaskCompletionSource()).Task.AttachExternalCancellation(ct);

        private void StartLoginFlowUntilEnd()
        {
            async UniTaskVoid StartLoginFlowUntilEndAsync(CancellationToken ct)
            {
                try
                {
                    viewInstance.ConnectingToServerContainer.SetActive(true);
                    viewInstance.LoginButton.interactable = false;

                    web3Authenticator.SetVerificationListener(ShowVerification);

                    IWeb3Identity identity = await web3Authenticator.LoginAsync(ct);

                    web3Authenticator.SetVerificationListener(null);

                    if (IsUserAllowedToAccessToBeta(identity))
                    {
                        SwitchState(ViewState.Loading);

                        await FetchProfileAsync(ct);

                        SwitchState(ViewState.Finalize);
                    }
                    else
                    {
                        SwitchState(ViewState.Login);
                        ShowRestrictedUserPopup();
                    }
                }
                catch (OperationCanceledException) { SwitchState(ViewState.Login); }
                catch (Exception e)
                {
                    SwitchState(ViewState.Login);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                }
            }

            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            StartLoginFlowUntilEndAsync(loginCancellationToken.Token).Forget();
        }

        private void ShowVerification(int code, DateTime expiration)
        {
            viewInstance.VerificationCodeLabel.text = code.ToString();

            CancelVerificationCountdown();
            verificationCountdownCancellationToken = new CancellationTokenSource();

            viewInstance.StartVerificationCountdownAsync(expiration,
                             verificationCountdownCancellationToken.Token)
                        .Forget();

            SwitchState(ViewState.LoginInProgress);
        }

        private async UniTask FetchProfileAsync(CancellationToken ct)
        {
            Profile profile = await selfProfile.ProfileOrPublishIfNotAsync(ct);

            // When the profile was already in cache, for example your previous account after logout, we need to ensure that all systems related to the profile will update
            profile.IsDirty = true;
            profileNameLabel!.Value = profile.Name;
            characterPreviewController?.Initialize(profile.Avatar);
        }

        private void ChangeAccount()
        {
            async UniTaskVoid ChangeAccountAsync(CancellationToken ct)
            {
                viewInstance.FinalizeAnimator.SetTrigger(AnimationHashes.TO_OTHER);
                await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                await web3Authenticator.LogoutAsync(ct);
                SwitchState(ViewState.Login);
            }

            characterPreviewController?.OnHide();
            CancelLoginProcess();
            loginCancellationToken = new CancellationTokenSource();
            ChangeAccountAsync(loginCancellationToken.Token).Forget();
        }

        private void JumpIntoWorld()
        {
            async UniTaskVoid AnimateAndAwaitAsync()
            {
                viewInstance.FinalizeAnimator.SetTrigger(AnimationHashes.JUMP_IN);
                await UniTask.Delay(ANIMATION_DELAY);
                characterPreviewController?.OnHide();
                lifeCycleTask?.TrySetResult();
                lifeCycleTask = null;
            }

            AnimateAndAwaitAsync().Forget();
        }

        private void SwitchState(ViewState state)
        {
            switch (state)
            {
                case ViewState.Login:
                    ResetAnimator(viewInstance.LoginAnimator);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.LoginContainer.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(AnimationHashes.IN);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = true;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.LoginInProgress:
                    ResetAnimator(viewInstance.VerificationAnimator);
                    viewInstance.PendingAuthentication.SetActive(true);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.LoginAnimator.SetTrigger(AnimationHashes.OUT);
                    viewInstance.VerificationAnimator.SetTrigger(AnimationHashes.IN);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.Loading:
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.Slides.SetActive(true);
                    viewInstance.ProgressContainer.SetActive(true);
                    viewInstance.FinalizeContainer.SetActive(false);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                case ViewState.Finalize:
                    ResetAnimator(viewInstance.FinalizeAnimator);
                    viewInstance.Slides.SetActive(false);
                    viewInstance.PendingAuthentication.SetActive(false);
                    viewInstance.LoginContainer.SetActive(false);
                    viewInstance.ProgressContainer.SetActive(false);
                    viewInstance.FinalizeContainer.SetActive(true);
                    viewInstance.FinalizeAnimator.SetTrigger(AnimationHashes.IN);
                    viewInstance.ConnectingToServerContainer.SetActive(false);
                    viewInstance.VerificationCodeHintContainer.SetActive(false);
                    viewInstance.LoginButton.interactable = false;
                    viewInstance.RestrictedUserContainer.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void ResetAnimator(Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.gameObject.SetActive(false);
        }

        private void CancelLoginProcess()
        {
            loginCancellationToken?.SafeCancelAndDispose();
            loginCancellationToken = null;
        }

        private void OpenOrCloseVerificationCodeHint()
        {
            viewInstance.VerificationCodeHintContainer.SetActive(!viewInstance.VerificationCodeHintContainer.activeSelf);
        }

        private void OpenDiscord() =>
            webBrowser.OpenUrl(DISCORD_LINK);

        private void CancelVerificationCountdown()
        {
            verificationCountdownCancellationToken?.SafeCancelAndDispose();
            verificationCountdownCancellationToken = null;
        }

        private void RequestAlphaAccess()
        {
            webBrowser.OpenUrl(REQUEST_BETA_ACCESS_LINK);
        }

        public void SetWorld(World world)
        {
            this.world = world;
        }
    }
}
