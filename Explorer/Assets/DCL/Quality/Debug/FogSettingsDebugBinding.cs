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
        }
    }
}
