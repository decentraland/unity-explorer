using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Quality.Debug;
using DCL.Quality.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#nullable disable

namespace DCL.Quality
{
    public class QualityContainer
    {
        private readonly List<Action> onDebugViewUpdate = new (50);

        public IRendererFeaturesCache RendererFeaturesCache { get; init; }

        public IQualityLevelController QualityLevelController { get; init; }

        public Plugin CreatePlugin() =>
            new (onDebugViewUpdate);

        public static QualityContainer Create(IPluginSettingsContainer pluginSettingsContainer)
        {
            Settings settings = pluginSettingsContainer.GetSettings<Settings>();

            var rendererFeaturesCache = new RendererFeaturesCache();
            IQualityLevelController controller = QualityRuntimeFactory.Create(rendererFeaturesCache, settings.QualitySettings);

            return new QualityContainer
            {
                RendererFeaturesCache = rendererFeaturesCache,
                QualityLevelController = controller,
            };
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
        }
    }
}
