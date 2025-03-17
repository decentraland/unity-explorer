using System;
using System.Collections.Generic;

namespace DCL.DebugUtilities.UIBindings
{
    /// <summary>
    ///     Workaround to get an index from the dropdown list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class IndexedElementBinding : ElementBinding<string>
    {
        internal readonly List<string> values;

        public IndexedElementBinding(List<string> values, string defaultValue, Action<(string value, int index)>? onValueChange = null) : base(defaultValue)
        {
            this.values = values;
            SetIndex();

            OnValueChanged += evt =>
            {
                SetIndex();
                onValueChange?.Invoke((evt.newValue, Index));
            };
        }

        public int Index { get; private set; }

        private void SetIndex()
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == Value)
                {
                    Index = i;
                    return;
                }
            }
        }
    }
}
