using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Crosshair;
using DCL.Input.Systems;
using DCL.Multiplayer.Emotes;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class InputSettings : IDCLPluginSettings
    {
        [field: Header(nameof(InputPlugin))]
        [field: Space]
        [field: SerializeField] public UIDocumentRef CrosshairUIDocument { get; set; }
        [field: SerializeField] public AssetReferenceSprite CrossHairNormal { get; set; }
        [field: SerializeField] public AssetReferenceSprite CrossHairInteraction { get; set; }
        [field: SerializeField] public WarningNotificationViewRef ShowUIToast { get; private set; }

        [Serializable]
        public class WarningNotificationViewRef : ComponentReference<WarningNotificationView>
        {
            public WarningNotificationViewRef(string guid) : base(guid)
            {
            }
        }
    }

    public class InputPlugin : IDCLGlobalPlugin<InputSettings>
    {
        private readonly MultiplayerEmotesMessageBus messageBus;
        private readonly IEventSystem eventSystem;
        private readonly ICursor cursor;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private CrosshairCanvas crosshairCanvas = null!;
        private WarningNotificationView showUIToast = null!;

        public InputPlugin(
            ICursor cursor,
            IEventSystem eventSystem,
            IAssetsProvisioner assetsProvisioner,
            MultiplayerEmotesMessageBus messageBus,
            IMVCManager mvcManager)
        {
            this.cursor = cursor;
            this.eventSystem = eventSystem;
            this.assetsProvisioner = assetsProvisioner;
            this.messageBus = messageBus;
            this.mvcManager = mvcManager;

            DCLInput.Instance.Enable();
        }

        public async UniTask InitializeAsync(InputSettings settings, CancellationToken ct)
        {
            crosshairCanvas = (await assetsProvisioner.ProvideInstanceAsync(settings.CrosshairUIDocument, ct: ct))
                             .Value.rootVisualElement.Q<CrosshairCanvas>();

            // if these sprites count is more than 3, please turn this into an array of (CursorStyle, Sprite)
            Sprite crosshair = (await assetsProvisioner.ProvideMainAssetAsync(settings.CrossHairNormal, ct)).Value;
            Sprite crosshairInteractable = (await assetsProvisioner.ProvideMainAssetAsync(settings.CrossHairInteraction, ct)).Value;

            crosshairCanvas.Initialize(crosshair, crosshairInteractable);

            showUIToast = (await assetsProvisioner.ProvideInstanceAsync(settings.ShowUIToast, ct: ct)).Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            builder.World.Create(new InputMapComponent(InputMapComponent.Kind.NONE));

            ApplyInputMapsSystem.InjectToWorld(ref builder);
            UpdateInputJumpSystem.InjectToWorld(ref builder, DCLInput.Instance.Player.Jump);
            UpdateInputMovementSystem.InjectToWorld(ref builder);
            UpdateCameraInputSystem.InjectToWorld(ref builder);
            DropPlayerFromFreeCameraSystem.InjectToWorld(ref builder, DCLInput.Instance.FreeCamera.DropPlayer);
            UpdateEmoteInputSystem.InjectToWorld(ref builder, messageBus, mvcManager);
            UpdateCursorInputSystem.InjectToWorld(ref builder, eventSystem, cursor, crosshairCanvas);
            UpdateShowHideUIInputSystem.InjectToWorld(ref builder, mvcManager, showUIToast);
        }

        public void Dispose()
        {
            DCLInput.Reset();
        }
    }
}
