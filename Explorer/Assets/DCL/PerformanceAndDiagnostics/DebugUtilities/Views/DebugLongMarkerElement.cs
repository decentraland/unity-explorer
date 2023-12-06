using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugLongMarkerElement : DebugElementBase<DebugLongMarkerElement, DebugLongMarkerDef>, INotifyValueChanged<ulong>
    {
        private Label cachedLabel;

        private ulong value;

        internal Label label => cachedLabel ?? this.Q<Label>();

        ulong INotifyValueChanged<ulong>.value
        {
            get => value;

            set
            {
                this.value = value;
                label.text = FormatValue(value, definition.MarkerUnit);
            }
        }

        protected override void ConnectBindings()
        {
            // Assign the real bindable element so the binding is updated
            label.binding = definition.Binding;

            definition.Binding.Connect(this);
        }

        void INotifyValueChanged<ulong>.SetValueWithoutNotify(ulong newValue)
        {
            value = newValue;
            ((INotifyValueChanged<string>)label).SetValueWithoutNotify(FormatValue(newValue, definition.MarkerUnit));
        }

        private static string FormatValue(ulong value, DebugLongMarkerDef.Unit unit)
        {
            switch (unit)
            {
                case DebugLongMarkerDef.Unit.TimeNanoseconds:
                    return $"{value * 1e-6} ms";
                case DebugLongMarkerDef.Unit.Bytes:
                    return BytesFormatter.Normalize(value, false);
                case DebugLongMarkerDef.Unit.Bits:
                    return BytesFormatter.Normalize(value, true);
                case DebugLongMarkerDef.Unit.NoFormat:
                    return $"{value}";
                default:
                    return $"{value}";
            }
        }

        public new class UxmlFactory : UxmlFactory<DebugLongMarkerElement> { }
    }
}
