using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Quality.Runtime;
using UnityEngine;

namespace DCL.Quality.Debug
{
    public class FogSettingsDebugBinding : QualitySettingDebugBinding<FogQualitySettingRuntime>
    {
        private ElementBinding<float>? densityBinding;

        public FogSettingsDebugBinding(FogQualitySettingRuntime runtime) : base(runtime, "Fog") { }

        protected override void AddElementsInternal(DebugWidgetBuilder builder)
        {
            densityBinding = new ElementBinding<float>(RenderSettings.fogDensity, f => RenderSettings.fogDensity = f);

            builder.AddFloatField("Density", densityBinding);
        }
    }
}
