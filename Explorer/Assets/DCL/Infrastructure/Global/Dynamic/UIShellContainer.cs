using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Friends.Passport;
using DCL.Input;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.UI.MainUI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using MVC;
using MVC.PopupsController.PopupCloser;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Global.Dynamic
{
    /// <summary>
    ///     Core UI shell: the MVC manager, main UI view, cursor, event system and clipboard.
    /// </summary>
    public class UIShellContainer : DCLGlobalContainer<UIShellContainer.Settings>
    {
        public IMVCManager MvcManager { get; private set; } = null!;

        public MainUIView MainUIView { get; private set; } = null!;

        public DCLCursor Cursor { get; private set; } = null!;

        public UnityEventSystem EventSystem { get; }

        public ISystemClipboard Clipboard { get; }

        public ClipboardManager ClipboardManager { get; }

        public SupportRequestService SupportRequestService { get; }

        public MVCPassportBridge PassportBridge { get; private set; } = null!;

        private UIShellContainer(BootstrapContainer bootstrapContainer)
        {
            EventSystem = new UnityEventSystem(UnityEngine.EventSystems.EventSystem.current.EnsureNotNull());
            Clipboard = new UnityClipboard();
            ClipboardManager = new ClipboardManager(Clipboard);
            SupportRequestService = new SupportRequestService(bootstrapContainer.WebBrowser);
        }

        public static async UniTask<(UIShellContainer? container, bool success)> CreateAsync(
            IPluginSettingsContainer settingsContainer,
            IAssetsProvisioner assetsProvisioner,
            BootstrapContainer bootstrapContainer,
            bool enableAnalytics,
            CancellationToken ct)
        {
            var uiShellContainer = new UIShellContainer(bootstrapContainer);

            return await uiShellContainer.InitializeContainerAsync<UIShellContainer, Settings>(settingsContainer, ct, async c =>
            {
                CursorSettings cursorSettings = (await assetsProvisioner.ProvideMainAssetAsync(c.settings.CursorSettings, ct)).Value;
                ProvidedAsset<Texture2D> normalCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.NormalCursor, ct);
                ProvidedAsset<Texture2D> interactionCursorAsset = await assetsProvisioner.ProvideMainAssetAsync(cursorSettings.InteractionCursor, ct);

                c.Cursor = new DCLCursor(normalCursorAsset.Value, interactionCursorAsset.Value, cursorSettings.NormalCursorHotspot, cursorSettings.InteractionCursorHotspot);

                PopupCloserView popupCloserView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(c.settings.PopupCloserView, CancellationToken.None)).Value.GetComponent<PopupCloserView>()).EnsureNotNull();
                c.MainUIView = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(c.settings.MainUIView, CancellationToken.None)).Value.GetComponent<MainUIView>()).EnsureNotNull();

                var coreMvcManager = new MVCManager(new WindowStackManager(), new CancellationTokenSource(), popupCloserView);

                c.MvcManager = enableAnalytics
                    ? new MVCManagerAnalyticsDecorator(coreMvcManager, bootstrapContainer.Analytics.Controller, c.SupportRequestService)
                    : coreMvcManager;

                c.PassportBridge = new MVCPassportBridge(c.MvcManager);
            });
        }

        public MainUIPlugin CreateMainUIPlugin(bool includeFriends) =>
            new (MvcManager, MainUIView, includeFriends);

        public InputPlugin CreateInputPlugin(IAssetsProvisioner assetsProvisioner, EmoteWheelShortcutHandler emoteWheelShortcutHandler) =>
            new (Cursor, EventSystem, assetsProvisioner, emoteWheelShortcutHandler, MvcManager);

        public ErrorPopupPlugin CreateErrorPopupPlugin(IAssetsProvisioner assetsProvisioner) =>
            new (MvcManager, assetsProvisioner);

        public GenericPopupsPlugin CreateGenericPopupsPlugin(IAssetsProvisioner assetsProvisioner) =>
            new (assetsProvisioner, MvcManager, ClipboardManager);

        public ColorPickerPlugin CreateColorPickerPlugin(IAssetsProvisioner assetsProvisioner) =>
            new (assetsProvisioner, MvcManager);

        public GenericContextMenuPlugin CreateGenericContextMenuPlugin(IAssetsProvisioner assetsProvisioner, ProfileRepositoryWrapper profileRepositoryWrapper) =>
            new (assetsProvisioner, MvcManager, profileRepositoryWrapper);

        public ConfirmationDialogPlugin CreateConfirmationDialogPlugin(IAssetsProvisioner assetsProvisioner, ProfileRepositoryWrapper profileRepositoryWrapper) =>
            new (assetsProvisioner, MvcManager, profileRepositoryWrapper);

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceT<CursorSettings> CursorSettings { get; private set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject PopupCloserView { get; private set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject MainUIView { get; private set; } = null!;
        }
    }
}
