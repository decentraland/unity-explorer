using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Crosshair;
using DCL.Input.Systems;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Utility.UIToolkit;
using DCL.Multiplayer.Emotes;
using UpdateEmoteInputSystem = DCL.AvatarRendering.Emotes.UpdateEmoteInputSystem;

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
        private readonly DCLInput dclInput;
        private readonly MultiplayerEmotesMessageBus messageBus;
        private readonly IEventSystem eventSystem;
        private readonly ICursor cursor;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly UIDocument canvas;
        private CrosshairCanvas crosshairCanvas = null!;

        public InputPlugin(
            DCLInput dclInput,
            ICursor cursor,
            IEventSystem eventSystem,
            IAssetsProvisioner assetsProvisioner,
            UIDocument canvas,
            MultiplayerEmotesMessageBus messageBus)
        {
            this.dclInput = dclInput;
            this.cursor = cursor;
            this.eventSystem = eventSystem;
            this.assetsProvisioner = assetsProvisioner;
            this.canvas = canvas;
            this.messageBus = messageBus;

            dclInput.Enable();
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
            builder.World.Create(new InputMapComponent((InputMapComponent.Kind)(~0)));

            ApplyInputMapsSystem.InjectToWorld(ref builder, dclInput);
            UpdateInputJumpSystem.InjectToWorld(ref builder, dclInput.Player.Jump);
            UpdateInputMovementSystem.InjectToWorld(ref builder, dclInput);
            UpdateCameraInputSystem.InjectToWorld(ref builder, dclInput);
            DropPlayerFromFreeCameraSystem.InjectToWorld(ref builder, dclInput.FreeCamera.DropPlayer);
            UpdateEmoteInputSystem.InjectToWorld(ref builder, dclInput.Emotes, messageBus);
            UpdateCursorInputSystem.InjectToWorld(ref builder, dclInput, eventSystem, cursor, crosshairCanvas);
        }

        public void Dispose()
        {
            dclInput.Dispose();
        }
    }
}
