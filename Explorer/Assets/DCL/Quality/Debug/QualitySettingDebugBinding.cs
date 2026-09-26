using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Quality.Runtime;
using System;
using System.Collections.Generic;

namespace DCL.Quality.Debug
{
    public static class QualitySettingDebugBinding
    {
        public static void AddElements<T>(DebugWidgetBuilder builder, T runtime, string name, List<Action> onUpdate) where T: IQualitySettingRuntime
        {
            builder.AddControl(
                new DebugConstLabelDef(name),
                new DebugToggleDef(AddTwoWayBinding(runtime, onUpdate, (r, value) => r.SetActive(value), r => r.IsActive)));
        }

        internal static ElementBinding<TValue> AddTwoWayBinding<TRuntime, TValue>(
            this TRuntime runtime,
            List<Action> onUpdate,
            Action<TRuntime, TValue> fromUiToRuntime,
            Func<TRuntime, TValue> fromRuntimeToUi)
        {
            // from UI to runtime
            var binding = new ElementBinding<TValue>(fromRuntimeToUi(runtime), evt => fromUiToRuntime(runtime, evt.newValue));

            // from runtime to UI
            onUpdate.Add(() => binding.Value = fromRuntimeToUi(runtime));
            return binding;
        }
    }
}
