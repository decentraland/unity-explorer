using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace DCL.PluginSystem.Global
{
    public class ColorPickerPlugin : IDCLGlobalPlugin<ColorPickerPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;

        private ColorPickerController? colorPickerController;

        public ColorPickerPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
        }

        public void Dispose()
        {
            colorPickerController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // No need to inject anything into the world
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            ColorPickerView colorPickerView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ColorPickerPrefab, ct)).Value;
            ColorToggleView colorToggleView = (await assetsProvisioner.ProvideMainAssetAsync(settings.ColorTogglePrefab, ct)).Value;

            ControllerBase<ColorPickerView, ColorPickerPopupData>.ViewFactoryMethod viewFactoryMethod =
                ColorPickerController.Preallocate(colorPickerView, null, out ColorPickerView colorPickerViewInstance);

            colorPickerController = new ColorPickerController(viewFactoryMethod, colorToggleView);
            mvcManager.RegisterController(colorPickerController);
        }

        public class Settings : IDCLPluginSettings
        {
            [Serializable]
            public class ColorPickerRef : ComponentReference<ColorPickerView>
            {
                public ColorPickerRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class ColorToggleRef : ComponentReference<ColorToggleView>
            {
                public ColorToggleRef(string guid) : base(guid) { }
            }

            [field: SerializeField] public ColorPickerRef ColorPickerPrefab;
            [field: SerializeField] public ColorToggleRef ColorTogglePrefab;
        }
    }
}
