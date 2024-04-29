using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Quality.Debug;
using DCL.Quality.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

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
            UnityEngine.Debug.Log($"QualityContainer.pluginSettingsContainer.GetSettings<Settings>()");
            
            Settings settings = pluginSettingsContainer.GetSettings<Settings>();

            UnityEngine.Debug.Log($"QualityContainer.assetsProvisioner.ProvideMainAssetAsync(RealmPartitionSettings: {settings.RealmPartitionSettings})");
            var realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.RealmPartitionSettings, CancellationToken.None);
            UnityEngine.Debug.Log($"QualityContainer.assetsProvisioner.ProvideMainAssetAsync(LODSettingAsset: {settings.LODSettingAsset})");
            var lodSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.LODSettingAsset, CancellationToken.None);
            UnityEngine.Debug.Log($"QualityContainer.assetsProvisioner.ProvideMainAssetAsync(LandscapeData: {settings.LandscapeData})");
            var landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.LandscapeData, CancellationToken.None);

            var rendererFeaturesCache = new RendererFeaturesCache();
            UnityEngine.Debug.Log($"QualityContainer.QualityRuntimeFactory.Create");
            IQualityLevelController controller = QualityRuntimeFactory.Create(
                rendererFeaturesCache,
                settings.QualitySettings,
                realmPartitionSettings.Value,
                lodSettingsAsset.Value,
                landscapeData.Value);
            
            UnityEngine.Debug.Log($"QualityContainer.return.ctor");

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
            DebugWidgetBuilder widget = debugContainerBuilder.AddWidget("Quality");

            // Add settings selector
            AddSettingsSelector(widget);

            QualityLevelController.AddDebugViews(widget, onDebugViewUpdate);
        }

        private void AddSettingsSelector(DebugWidgetBuilder builder)
        {
            // changing quality presets at runtime won't be reflected
            string[] presets = QualitySettings.names;

            var binding = new ElementBinding<string>(presets[QualitySettings.GetQualityLevel()],
                evt => QualitySettings.SetQualityLevel(Array.IndexOf(presets, evt.newValue)));

            QualitySettings.activeQualityLevelChanged += (_, to) => binding.Value = presets[to];

            builder.AddControl(new DebugDropdownDef(presets.ToList(), binding, "Level"), null);
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
            public StaticSettings.RealmPartitionSettingsRef RealmPartitionSettings { get; private set; }

            [field: SerializeField]
            public StaticSettings.LODSettingsRef LODSettingAsset { get; set; }

            [field: SerializeField]
            public LandscapeSettings.LandscapeDataRef LandscapeData { get; private set; }
        }
    }
}
