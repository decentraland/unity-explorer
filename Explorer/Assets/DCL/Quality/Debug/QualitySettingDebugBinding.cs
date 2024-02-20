using DCL.DebugUtilities;
using DCL.Quality.Runtime;

namespace DCL.Quality.Debug
{
    public static class QualitySettingDebugBinding
    {
        public static void AddElements<T>(DebugWidgetBuilder builder, T runtime, string name) where T: IQualitySettingRuntime
        {
            // Add label and toggle
            builder.AddControl(new DebugConstLabelDef(name), new DebugToggleDef(evt => runtime.SetActive(evt.newValue), runtime.IsActive));
        }
    }
}
