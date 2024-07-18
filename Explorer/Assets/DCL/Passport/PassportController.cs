using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Passport.Modules;
using DCL.Profiles;
using DCL.Profiles.Self;
using JetBrains.Annotations;
using MVC;
using System;
using System.Collections.Generic;
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
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly DCLInput dclInput;
        private readonly IWebBrowser webBrowser;

        private string currentUserId;
        private CancellationTokenSource characterPreviewLoadingCts;
        private PassportErrorsController passportErrorsController;
        private PassportCharacterPreviewController characterPreviewController;
        private readonly List<IPassportModuleController> passportModules = new ();

        public PassportController(
            [NotNull] ViewFactoryMethod viewFactory,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            CharacterPreviewEventBus characterPreviewEventBus,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            World world,
            Entity playerEntity,
            IThumbnailProvider thumbnailProvider,
            DCLInput dclInput,
            IWebBrowser webBrowser) : base(viewFactory)
        {
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.world = world;
            this.playerEntity = playerEntity;
            this.thumbnailProvider = thumbnailProvider;
            this.dclInput = dclInput;
            this.webBrowser = webBrowser;
        }

        protected override void OnViewInstantiated()
        {
            Assert.IsNotNull(world);
            passportErrorsController = new PassportErrorsController(viewInstance.ErrorNotification);
            characterPreviewController = new PassportCharacterPreviewController(viewInstance.CharacterPreviewView, characterPreviewFactory, world, characterPreviewEventBus);
            passportModules.Add(new UserBasicInfo_PassportModuleController(viewInstance.UserBasicInfoModuleView, chatEntryConfiguration, selfProfile, passportErrorsController));
            passportModules.Add(new UserDetailedInfo_PassportModuleController(viewInstance.UserDetailedInfoModuleView, mvcManager, selfProfile, profileRepository, world, playerEntity, viewInstance.AddLinkModal, passportErrorsController));
            passportModules.Add(new EquippedItems_PassportModuleController(viewInstance.EquippedItemsModuleView, world, rarityBackgrounds, rarityColors, categoryIcons, thumbnailProvider, viewInstance.MainContainer, webBrowser, passportErrorsController));
        }

        protected override void OnViewShow()
        {
            currentUserId = inputData.UserId;
            cursor.Unlock();
            characterPreviewLoadingCts = characterPreviewLoadingCts.SafeRestart();
            LoadUserProfileAsync(currentUserId, characterPreviewLoadingCts.Token).Forget();
            viewInstance.MainScroll.verticalNormalizedPosition = 1;
            dclInput.Shortcuts.Disable();
            viewInstance.ErrorNotification.Hide(true);
        }

        protected override void OnViewClose()
        {
            passportErrorsController.Hide(true);
            dclInput.Shortcuts.Enable();
            characterPreviewController.OnHide();

            foreach (IPassportModuleController module in passportModules)
                module.Clear();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.BackgroundButton.OnClickAsync(ct));

        public override void Dispose()
        {
            passportErrorsController.Hide(true);
            characterPreviewLoadingCts.SafeCancelAndDispose();
            characterPreviewController.Dispose();

            foreach (IPassportModuleController module in passportModules)
                module.Dispose();
        }

        private async UniTaskVoid LoadUserProfileAsync(string userId, CancellationToken ct)
        {
            try
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
                foreach (IPassportModuleController module in passportModules)
                    module.Setup(profile);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to load the profile. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
