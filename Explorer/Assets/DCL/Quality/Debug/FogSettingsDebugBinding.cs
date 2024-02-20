using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Quality.Debug;

namespace DCL.Quality.Runtime
{
    public partial class FogQualitySettingRuntime
    {
        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder)
        {
            QualitySettingDebugBinding.AddElements(debugWidgetBuilder, this, "Fog");

            debugWidgetBuilder.AddFloatField("Density", new ElementBinding<float>(Density, OverrideDensity));
            debugWidgetBuilder.AddFloatField("End Distance", new ElementBinding<float>(EndDistance, OverrideEndDistance));
            debugWidgetBuilder.AddFloatField("Start Distance", new ElementBinding<float>(StartDistance, OverrideStartDistance));

            // TODO enum and color support
            // debugWidgetBuilder.AddEnumField("Mode", new ElementBinding<FogMode>(runtime.Mode, runtime.OverrideMode));
            // debugWidgetBuilder.AddColorField("Color", new ElementBinding<Color>(runtime.Color, runtime.OverrideColor));
        }
    }
}
