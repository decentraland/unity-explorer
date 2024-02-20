using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using UnityEngine;

namespace DCL.Quality
{
    /// <summary>
    ///     Allows to modify the given quality setting at runtime
    /// </summary>
    public interface IQualitySettingRuntime
    {
        bool IsActive { get; }

        void SetActive(bool active);
    }

    public interface IQualitySettingDebugBinding
    {
        void AddElements(DebugWidgetBuilder builder);
    }

    public interface IQualityLevelController
    {
        void SetLevel(int index);
    }

    public class QualityLevelController
    {
        public QualityLevelController()
        {
            QualitySettings.activeQualityLevelChanged += OnQualityLevelChanged;
        }

        private void OnQualityLevelChanged(int from, int to)
        {
            // Change all
        }
    }

    public interface IQualitySettingSerialization { }

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
            // Add label
            builder.AddControl(new DebugConstLabelDef(name), null);

            // Add toggle
            builder.AddToggleField("enabled", evt => runtime.SetActive(evt.newValue), runtime.IsActive);

            AddElementsInternal(builder);
        }

        protected virtual void AddElementsInternal(DebugWidgetBuilder builder) { }
    }

    public class FogSettingsDebugBinding : QualitySettingDebugBinding<FogQualitySettingRuntime>
    {
        private ElementBinding<float>? densityBinding;

        public FogSettingsDebugBinding(FogQualitySettingRuntime runtime) : base(runtime, "Fog") { }

        protected override void AddElementsInternal(DebugWidgetBuilder builder)
        {
            densityBinding = new ElementBinding<float>(RenderSettings.fogDensity, f => RenderSettings.fogDensity = f);

            builder.AddFloatField("Density", densityBinding);

            // TODO add other parameters
        }
    }
}
