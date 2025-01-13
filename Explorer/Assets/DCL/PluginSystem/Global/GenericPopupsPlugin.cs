using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Clipboard;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace DCL.PluginSystem.Global
{
    public class GenericPopupsPlugin : IDCLGlobalPlugin<GenericPopupsPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly ISystemClipboard systemClipboard;

        private PastePopupToastController? pasteToastButtonController;
        private ChatEntryMenuPopupController chatEntryMenuPopupController;

        public GenericPopupsPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ISystemClipboard systemClipboard)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.systemClipboard = systemClipboard;
        }

        public void Dispose()
        {
            pasteToastButtonController?.Dispose();
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
            pasteToastButtonController = new PastePopupToastController(pasteViewFactoryMethod, systemClipboard);
            mvcManager.RegisterController(pasteToastButtonController);

            ChatEntryMenuPopupView chatMenuPopupView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ChatEntryMenuPopupPrefab, ct)).Value;
            ControllerBase<ChatEntryMenuPopupView, ChatEntryMenuPopupData>.ViewFactoryMethod viewFactoryMethod =
                ChatEntryMenuPopupController.Preallocate(chatMenuPopupView, null, out ChatEntryMenuPopupView popupView);
            chatEntryMenuPopupController = new ChatEntryMenuPopupController(viewFactoryMethod, systemClipboard);
            mvcManager.RegisterController(chatEntryMenuPopupController);
        }

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


            [field: SerializeField] public PastePopupToastRef PastePopupToastPrefab;
            [field: SerializeField] public ChatEntryMenuPopupRef ChatEntryMenuPopupPrefab;
        }

    }
}
