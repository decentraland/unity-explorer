using DCL.DebugUtilities;
using DCL.Quality.Runtime;

namespace DCL.Quality.Debug
{
    public abstract class QualitySettingDebugBinding<T> : IQualitySettingDebugBinding where T: IQualitySettingRuntime
    {
        protected readonly T runtime;
        private readonly string name;

        protected QualitySettingDebugBinding(T runtime, string name)
        {
            this.runtime = runtime;
            this.name = name;
        }

        public void AddElements(DebugWidgetBuilder builder)
        {
            // Add label and toggle
            builder.AddControl(new DebugConstLabelDef(name), new DebugToggleDef(evt => runtime.SetActive(evt.newValue), runtime.IsActive));

            AddElementsInternal(builder);
        }

        protected virtual void AddElementsInternal(DebugWidgetBuilder builder) { }
    }
}
