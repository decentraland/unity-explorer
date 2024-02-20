using DCL.DebugUtilities;
using DCL.Quality.Debug;

namespace DCL.Quality.Runtime
{
    public partial class RendererFeatureQualitySettingRuntime<T>
    {
        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder)
        {
            QualitySettingDebugBinding.AddElements(debugWidgetBuilder, this, name);
        }
    }
}
