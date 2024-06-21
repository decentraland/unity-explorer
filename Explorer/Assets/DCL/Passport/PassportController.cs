using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Input;
using DCL.Profiles;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using Utility;

namespace DCL.Passport
{
    public partial class PassportController : ControllerBase<PassportView, PassportController.Params>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ICursor cursor;
        private PassportCharacterPreviewController characterPreviewController;
        private readonly IProfileRepository profileRepository;
        private readonly ICharacterPreviewFactory characterPreviewFactory;

        private string currentUserId;
        private CancellationTokenSource characterPreviewLoadingCts;
        private World? world;

        public PassportController(
            [NotNull] ViewFactoryMethod viewFactory,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory) : base(viewFactory)
        {
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
        }

        protected override void OnViewInstantiated()
        {
            Assert.IsNotNull(world);
            characterPreviewController = new PassportCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world!);

            viewInstance.CopyUserNameButton.onClick.AddListener(() => CopyToClipboard(viewInstance.UserNameText.text));
            viewInstance.CopyWalletAddressButton.onClick.AddListener(() => CopyToClipboard(currentUserId));
        }

        protected override void OnViewShow()
        {
            currentUserId = inputData.UserId;
            cursor.Unlock();
            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadCharacterPreviewAsync(currentUserId, characterPreviewLoadingCts.Token).Forget();
        }

        protected override void OnViewClose() =>
            characterPreviewController.OnHide();

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.BackgroundButton.OnClickAsync(ct));

        public void SetWorld(World world)
        {
            this.world = world;
        }

        public override void Dispose()
        {
            characterPreviewLoadingCts.SafeCancelAndDispose();
            characterPreviewController.Dispose();
            viewInstance.CopyUserNameButton.onClick.RemoveAllListeners();
            viewInstance.CopyWalletAddressButton.onClick.RemoveAllListeners();
        }

        private async UniTaskVoid LoadCharacterPreviewAsync(string userId, CancellationToken ct)
        {
            var profile = await profileRepository.GetAsync(userId, 0, ct);
            if (profile == null)
                return;

            characterPreviewController.Initialize(profile.Avatar);
            characterPreviewController.OnShow();

            viewInstance.UserNameText.text = profile.Name;
            viewInstance.VerifiedMark.SetActive(profile.HasClaimedName);
            viewInstance.UserWalletAddressText.text = $"{profile.UserId[..3]}...{profile.UserId[^3..]}";

            LayoutRebuilder.ForceRebuildLayoutImmediate(viewInstance.UserNameContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewInstance.WalletAddressContainer);
        }

        private void CopyToClipboard(string text) =>
            GUIUtility.systemCopyBuffer = text;
    }
}
