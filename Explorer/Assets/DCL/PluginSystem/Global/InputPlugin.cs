using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterCamera.Systems;
using DCL.CharacterMotion.Systems;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Crosshair;
using DCL.Input.Systems;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.PluginSystem.Global
{
    public class InputPluginSettings : IDCLPluginSettings
    {
        [field: Header(nameof(InputPlugin))]
        [field: Space]
        [field: SerializeField] public AssetReferenceVisualTreeAsset CrosshairCanvasAsset { get; }
        [field: SerializeField] public AssetReferenceSprite CrossHairNormal { get; }
        [field: SerializeField] public AssetReferenceSprite CrossHairInteraction { get; }
    }

    public class InputPlugin : IDCLGlobalPlugin<InputPluginSettings>
    {
        private readonly DCLInput dclInput;
        private readonly ICursor cursor;
        private readonly UnityEventSystem unityEventSystem;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly UIDocument canvas;
        private CrosshairCanvas crosshairCanvas;

        public InputPlugin(
            DCLInput dclInput,
            ICursor cursor,
            UnityEventSystem eventSystem,
            IAssetsProvisioner assetsProvisioner,
            UIDocument canvas)
        {
            this.dclInput = dclInput;
            this.cursor = cursor;
            unityEventSystem = eventSystem;
            this.assetsProvisioner = assetsProvisioner;
            this.canvas = canvas;
            dclInput.Enable();
        }

        public async UniTask InitializeAsync(InputPluginSettings settings, CancellationToken ct)
        {
            crosshairCanvas =
                (await assetsProvisioner.ProvideMainAssetAsync(settings.CrosshairCanvasAsset, ct: ct))
               .Value.InstantiateForElement<CrosshairCanvas>();

            Sprite crossHair = (await assetsProvisioner.ProvideMainAssetAsync(settings.CrossHairNormal, ct)).Value;
            Sprite crossHairInteractable = (await assetsProvisioner.ProvideMainAssetAsync(settings.CrossHairInteraction, ct)).Value;

            crosshairCanvas.Initialize(crossHair, crossHairInteractable);
            canvas.rootVisualElement.Add(crosshairCanvas);
            crosshairCanvas.SetDisplayed(false);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            builder.World.Create(new InputMapComponent((InputMapComponent.Kind)(~0)));

            ApplyInputMapsSystem.InjectToWorld(ref builder, dclInput);
            UpdateInputJumpSystem.InjectToWorld(ref builder, dclInput.Player.Jump);
            UpdateInputMovementSystem.InjectToWorld(ref builder, dclInput);
            UpdateCameraInputSystem.InjectToWorld(ref builder, dclInput);
            DropPlayerFromFreeCameraSystem.InjectToWorld(ref builder, dclInput.FreeCamera.DropPlayer);
            UpdateCursorInputSystem.InjectToWorld(ref builder, dclInput, unityEventSystem, cursor, crosshairCanvas);
        }

        public void Dispose()
        {
            dclInput.Dispose();
        }
    }
}
