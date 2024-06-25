using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.Input;
using DCL.Passport.Modules;
using DCL.Profiles;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;

namespace DCL.Passport
{
    public partial class PassportController : ControllerBase<PassportView, PassportController.Params>
    {
        private static readonly int BG_SHADER_COLOR_1 = Shader.PropertyToID("_Color1");

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ICursor cursor;
        private readonly IProfileRepository profileRepository;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;

        private string currentUserId;
        private CancellationTokenSource characterPreviewLoadingCts;
        private World? world;
        private PassportCharacterPreviewController characterPreviewController;
        private IPassportModuleController userBasicInfoModuleController;
        private IPassportModuleController userDetailedInfoModuleController;
        private IPassportModuleController equippedItemsModuleController;

        public PassportController(
            [NotNull] ViewFactoryMethod viewFactory,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration) : base(viewFactory)
        {
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
            this.chatEntryConfiguration = chatEntryConfiguration;
        }

        protected override void OnViewInstantiated()
        {
            Assert.IsNotNull(world);
            characterPreviewController = new PassportCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world!);
            userBasicInfoModuleController = new UserBasicInfo_PassportModuleController(viewInstance.UserBasicInfoModuleView, chatEntryConfiguration);
            userDetailedInfoModuleController = new UserDetailedInfo_PassportModuleController(viewInstance.UserDetailedInfoModuleView);
            equippedItemsModuleController = new EquippedItems_PassportModuleController(viewInstance.EquippedItemsModuleView);
        }

        protected override void OnViewShow()
        {
            currentUserId = inputData.UserId;
            cursor.Unlock();
            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadUserProfileAsync(currentUserId, characterPreviewLoadingCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            characterPreviewController.OnHide();
            userBasicInfoModuleController.Clear();
            userDetailedInfoModuleController.Clear();
            equippedItemsModuleController.Clear();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.BackgroundButton.OnClickAsync(ct));

        public void SetWorld(World world) =>
            this.world = world;

        public override void Dispose()
        {
            characterPreviewLoadingCts.SafeCancelAndDispose();
            characterPreviewController.Dispose();
            userBasicInfoModuleController.Dispose();
            userDetailedInfoModuleController.Dispose();
            equippedItemsModuleController.Dispose();
        }

        private async UniTaskVoid LoadUserProfileAsync(string userId, CancellationToken ct)
        {
            // Load user profile
            var profile = await profileRepository.GetAsync(userId, 0, ct);
            if (profile == null)
                return;

            viewInstance.BackgroundImage.material.SetColor(BG_SHADER_COLOR_1, chatEntryConfiguration.GetNameColor(profile.Name));

            // Load avatar preview
            characterPreviewController.Initialize(profile.Avatar);
            characterPreviewController.OnShow();

            // Load passport modules
            userBasicInfoModuleController.Setup(profile);
            userDetailedInfoModuleController.Setup(profile);
            equippedItemsModuleController.Setup(profile);
        }
    }
}
