using DCL.DebugUtilities;
using DCL.PluginSystem;
using DCL.Quality.Runtime;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

#nullable disable

namespace DCL.Quality
{
    public class QualityContainer
    {
        public IRendererFeaturesCache RendererFeaturesCache { get; init; }

        public IQualityLevelController QualityLevelController { get; init; }

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

            QualityLevelController.AddDebugViews(widget);
        }

        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(QualityContainer))]
            [field: SerializeField]
            public QualitySettingsAsset QualitySettings { get; private set; }
        }
    }
}
