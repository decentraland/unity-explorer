using DCL.DebugUtilities;
using DCL.Quality.Debug;
using System;
using System.Collections.Generic;

namespace DCL.Quality.Runtime
{
    public partial class FogQualitySettingRuntime
    {
        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate)
        {
            QualitySettingDebugBinding.AddElements(debugWidgetBuilder, this, "Fog", onUpdate);

            debugWidgetBuilder.AddFloatField("Density",
                this.AddTwoWayBinding(onUpdate, (runtime, f) => runtime.OverrideDensity(f), r => r.Density));

            debugWidgetBuilder.AddFloatField("Start Distance",
                this.AddTwoWayBinding(onUpdate, (runtime, f) => runtime.OverrideStartDistance(f), r => r.StartDistance));

            debugWidgetBuilder.AddFloatField("End Distance",
                this.AddTwoWayBinding(onUpdate, (runtime, f) => runtime.OverrideEndDistance(f), r => r.EndDistance));

            // TODO enum and color support

            // debugWidgetBuilder.AddEnumField("Mode",
            //     this.AddTwoWayBinding(onUpdate, (runtime, f) => runtime.OverrideMode(f), r => r.Mode));
            //
            // debugWidgetBuilder.AddColorField("Color",
            //     this.AddTwoWayBinding(onUpdate, (runtime, f) => runtime.OverrideColor(f), r => r.Color));
        }
    }
}
