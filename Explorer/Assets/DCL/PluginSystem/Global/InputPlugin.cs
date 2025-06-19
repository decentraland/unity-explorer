using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Crosshair;
using DCL.Input.Systems;
using DCL.Multiplayer.Emotes;
using DCL.UI.SharedSpaceManager;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class InputSettings : IDCLPluginSettings
    {
        [field: Header(nameof(InputPlugin))]
        [field: Space]
        [field: SerializeField] public AssetReferenceVisualTreeAsset CrosshairCanvasAsset { get; set; }
        [field: SerializeField] public AssetReferenceSprite CrossHairNormal { get; set; }
        [field: SerializeField] public AssetReferenceSprite CrossHairInteraction { get; set; }
    }

    public class InputPlugin : IDCLGlobalPlugin<InputSettings>
    {
        private readonly MultiplayerEmotesMessageBus messageBus;
        private readonly IEventSystem eventSystem;
        private readonly ICursor cursor;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly UIDocument canvas;
        private readonly IMVCManager mvcManager;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly UIDocument rootUIDocument;
        private readonly UIDocument sceneUIDocument;
        private readonly UIDocument cursorUIDocument;
        private CrosshairCanvas crosshairCanvas = null!;

        public InputPlugin(
            ICursor cursor,
            IEventSystem eventSystem,
            IAssetsProvisioner assetsProvisioner,
            UIDocument canvas,
            MultiplayerEmotesMessageBus messageBus,
            IMVCManager mvcManager,
            IDebugContainerBuilder debugContainerBuilder,
            UIDocument rootUIDocument,
            UIDocument sceneUIDocument,
            UIDocument cursorUIDocument)
        {
            this.cursor = cursor;
            this.eventSystem = eventSystem;
            this.assetsProvisioner = assetsProvisioner;
            this.canvas = canvas;
            this.messageBus = messageBus;
            this.mvcManager = mvcManager;
            this.debugContainerBuilder = debugContainerBuilder;
            this.rootUIDocument = rootUIDocument;
            this.sceneUIDocument = sceneUIDocument;
            this.cursorUIDocument = cursorUIDocument;

            DCLInput.Instance.Enable();
        }

        public async UniTask InitializeAsync(InputSettings settings, CancellationToken ct)
        {
            crosshairCanvas =
                (await assetsProvisioner.ProvideMainAssetAsync(settings.CrosshairCanvasAsset, ct: ct))
               .Value.InstantiateForElement<CrosshairCanvas>();

            // if these sprites count is more than 3, please turn this into an array of (CursorStyle, Sprite)
            Sprite crosshair = (await assetsProvisioner.ProvideMainAssetAsync(settings.CrossHairNormal, ct)).Value;
            Sprite crosshairInteractable = (await assetsProvisioner.ProvideMainAssetAsync(settings.CrossHairInteraction, ct)).Value;

            crosshairCanvas.Initialize(crosshair, crosshairInteractable);
            crosshairCanvas.SetDisplayed(false);

            canvas.rootVisualElement.Add(crosshairCanvas);
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
            UpdateShowHideUIInputSystem.InjectToWorld(ref builder, mvcManager, debugContainerBuilder, rootUIDocument, sceneUIDocument, cursorUIDocument);
        }

        public void Dispose()
        {
            DCLInput.Instance.Dispose();
            DCLInput.Reset();
        }
    }
}
