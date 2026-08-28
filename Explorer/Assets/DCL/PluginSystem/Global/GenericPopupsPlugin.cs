using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Clipboard;
using DCL.UI;
using DCL.UI.UpgradeGuestAccountPopup;
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

        private PastePopupToastController? pasteToastButtonController;
        private ChatEntryMenuPopupController? chatEntryMenuPopupController;
        private UpgradeGuestAccountPopupController? upgradeGuestAccountPopupController;

        public GenericPopupsPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ClipboardManager clipboardManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.clipboardManager = clipboardManager;
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
                PastePopupToastController.Preallocate(panelViewAsset, null, out PastePopupToastView panelView);

            pasteToastButtonController = new PastePopupToastController(pasteViewFactoryMethod, clipboardManager);
            mvcManager.RegisterController(pasteToastButtonController);

            ChatEntryMenuPopupView chatMenuPopupView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryMenuPopupPrefab, ct)).Value;

            ControllerBase<ChatEntryMenuPopupView, ChatEntryMenuPopupData>.ViewFactoryMethod viewFactoryMethod =
                ChatEntryMenuPopupController.Preallocate(chatMenuPopupView, null, out ChatEntryMenuPopupView popupView);

            chatEntryMenuPopupController = new ChatEntryMenuPopupController(viewFactoryMethod, clipboardManager);
            mvcManager.RegisterController(chatEntryMenuPopupController);

            UpgradeGuestAccountPopupView upgradeGuestAccountPopupAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.UpgradeGuestAccountPopupPrefab, ct)).Value;

            ControllerBase<UpgradeGuestAccountPopupView, ControllerNoData>.ViewFactoryMethod upgradeGuestAccountViewFactoryMethod =
                UpgradeGuestAccountPopupController.Preallocate(upgradeGuestAccountPopupAsset, null, out _);

            upgradeGuestAccountPopupController = new UpgradeGuestAccountPopupController(upgradeGuestAccountViewFactoryMethod);
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

            [field: SerializeField] public PastePopupToastRef PastePopupToastPrefab;
            [field: SerializeField] public ChatEntryMenuPopupRef ChatEntryMenuPopupPrefab;
            [field: SerializeField] public UpgradeGuestAccountPopupRef UpgradeGuestAccountPopupPrefab;
        }
    }
}
