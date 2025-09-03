using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Landscape.Settings;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Quality.Debug;
using DCL.Quality.Runtime;
using DCL.SDKComponents.LightSource;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

#nullable disable

namespace DCL.Quality
{
    public class QualityContainer : IDisposable
    {
        private readonly List<Action> onDebugViewUpdate = new (50);

        public IRendererFeaturesCache RendererFeaturesCache { get; init; }

        public IQualityLevelController QualityLevelController { get; init; }

        public Plugin CreatePlugin() =>
            new (onDebugViewUpdate);

        public static async UniTask<QualityContainer> CreateAsync(IPluginSettingsContainer pluginSettingsContainer, IAssetsProvisioner assetsProvisioner)
        {
            Settings settings = pluginSettingsContainer.GetSettings<Settings>();

            var lodSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.LODSettingAsset, CancellationToken.None);
            var landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.LandscapeData, CancellationToken.None);
            var lightSourceSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.LightSourceSettings, CancellationToken.None);

            var rendererFeaturesCache = new RendererFeaturesCache();
            IQualityLevelController controller = QualityRuntimeFactory.Create(
                rendererFeaturesCache,
                settings.QualitySettings,
                settings.RealmPartitionSettings,
                settings.VideoPrioritizationSettings,
                lodSettingsAsset.Value,
                landscapeData.Value,
                lightSourceSettings.Value);

            return new QualityContainer
            {
                RendererFeaturesCache = rendererFeaturesCache,
                QualityLevelController = controller,
            };
        }

        public void Dispose()
        {
            QualityLevelController.Dispose();
        }

        public void AddDebugViews(IDebugContainerBuilder debugContainerBuilder)
        {
            var widget = debugContainerBuilder.AddWidget("Quality");

            if (widget.Success == false)
                return;

            // Add settings selector
            AddSettingsSelector(widget.Value);

            QualityLevelController.AddDebugViews(widget.Value, onDebugViewUpdate);
        }

        private void AddSettingsSelector(DebugWidgetBuilder builder)
        {
            // changing quality presets at runtime won't be reflected
            var presets = QualitySettings.names.ToList();

            var binding = new IndexedElementBinding(presets, presets[QualitySettings.GetQualityLevel()], evt => QualitySettings.SetQualityLevel(evt.index));

            QualitySettings.activeQualityLevelChanged += (_, to) => binding.Value = presets[to];

            builder.AddControl(new DebugDropdownDef(binding, "Level"), null);
        }

        public class Plugin : IDCLGlobalPluginWithoutSettings
        {
            private readonly List<Action> onDebugViewUpdate;

            public Plugin(List<Action> onDebugViewUpdate)
            {
                this.onDebugViewUpdate = onDebugViewUpdate;
            }

            public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
            {
                QualitySettingsSyncSystem.InjectToWorld(ref builder, onDebugViewUpdate);
            }
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(QualityContainer))]
            [field: SerializeField]
            public QualitySettingsAsset QualitySettings { get; private set; }

            [field: SerializeField]
            public RealmPartitionSettingsAsset RealmPartitionSettings { get; private set; }

            [field: SerializeField]
            public VideoPrioritizationSettings VideoPrioritizationSettings { get; private set; }

            [field: SerializeField]
            public StaticSettings.LODSettingsRef LODSettingAsset { get; set; }

            [field: SerializeField]
            public LandscapeDataRef LandscapeData { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<LightSourceSettings> LightSourceSettings { get; private set; }
        }
    }
}
