using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Clipboard;
using DCL.Input;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Identities;
using DCL.UI;
using DCL.UI.UpgradeGuestAccountPopup;
using DCL.Web3.Authenticators;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class GenericPopupsPlugin : IDCLGlobalPlugin<GenericPopupsPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ClipboardManager clipboardManager;
        private readonly ICompositeWeb3Provider accountLinkAuthenticator;
        private readonly ISelfProfile selfProfile;
        private readonly IInputBlock inputBlock;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileCache profileCache;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;

        private PastePopupToastController? pasteToastButtonController;
        private ChatEntryMenuPopupController? chatEntryMenuPopupController;
        private UpgradeGuestAccountPopupController? upgradeGuestAccountPopupController;

        public GenericPopupsPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ClipboardManager clipboardManager,
            ICompositeWeb3Provider accountLinkAuthenticator,
            ISelfProfile selfProfile,
            IInputBlock inputBlock,
            IWeb3IdentityCache identityCache,
            IProfileCache profileCache,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            Arch.Core.World world,
            Entity playerEntity)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.clipboardManager = clipboardManager;
            this.accountLinkAuthenticator = accountLinkAuthenticator;
            this.selfProfile = selfProfile;
            this.inputBlock = inputBlock;
            this.identityCache = identityCache;
            this.profileCache = profileCache;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void Dispose()
        {
            pasteToastButtonController?.Dispose();
            chatEntryMenuPopupController?.Dispose();
            upgradeGuestAccountPopupController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // No need to inject anything into the world
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            PastePopupToastView panelViewAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.PastePopupToastPrefab, ct)).Value;

            ControllerBase<PastePopupToastView, PastePopupToastData>.ViewFactoryMethod pasteViewFactoryMethod =
                PastePopupToastController.Preallocate(panelViewAsset, null, out PastePopupToastView _);

            pasteToastButtonController = new PastePopupToastController(pasteViewFactoryMethod, clipboardManager);
            mvcManager.RegisterController(pasteToastButtonController);

            ChatEntryMenuPopupView chatMenuPopupView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryMenuPopupPrefab, ct)).Value;

            ControllerBase<ChatEntryMenuPopupView, ChatEntryMenuPopupData>.ViewFactoryMethod viewFactoryMethod =
                ChatEntryMenuPopupController.Preallocate(chatMenuPopupView, null, out ChatEntryMenuPopupView _);

            chatEntryMenuPopupController = new ChatEntryMenuPopupController(viewFactoryMethod, clipboardManager);
            mvcManager.RegisterController(chatEntryMenuPopupController);

            UpgradeGuestAccountPopupView upgradeGuestAccountPopupAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.UpgradeGuestAccountPopupPrefab, ct)).Value;

            ControllerBase<UpgradeGuestAccountPopupView, UpgradeGuestAccountPopupController.Params>.ViewFactoryMethod upgradeGuestAccountViewFactoryMethod =
                UpgradeGuestAccountPopupController.Preallocate(upgradeGuestAccountPopupAsset, null, out _);

            upgradeGuestAccountPopupController = new UpgradeGuestAccountPopupController(upgradeGuestAccountViewFactoryMethod, accountLinkAuthenticator, selfProfile, inputBlock,
                identityCache, profileCache, userInAppInitializationFlow, world, playerEntity);
            mvcManager.RegisterController(upgradeGuestAccountPopupController);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [Serializable]
            public class PastePopupToastRef : ComponentReference<PastePopupToastView>
            {
                public PastePopupToastRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class ChatEntryMenuPopupRef : ComponentReference<ChatEntryMenuPopupView>
            {
                public ChatEntryMenuPopupRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class UpgradeGuestAccountPopupRef : ComponentReference<UpgradeGuestAccountPopupView>
            {
                public UpgradeGuestAccountPopupRef(string guid) : base(guid) { }
            }

            [field: SerializeField] public PastePopupToastRef PastePopupToastPrefab = null!;
            [field: SerializeField] public ChatEntryMenuPopupRef ChatEntryMenuPopupPrefab = null!;
            [field: SerializeField] public UpgradeGuestAccountPopupRef UpgradeGuestAccountPopupPrefab = null!;
        }
    }
}
