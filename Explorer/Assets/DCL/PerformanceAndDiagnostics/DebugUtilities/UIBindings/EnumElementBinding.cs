using System;
using System.Collections.Generic;
using System.Linq;
using Utility;

namespace DCL.DebugUtilities.UIBindings
{
    public class EnumElementBinding<T> : IndexedElementBinding where T: struct, Enum
    {
        public readonly IReadOnlyList<T> Values;

        public EnumElementBinding(T defaultValue, IReadOnlyList<T> values = null, Action<T>? onValueChange = null)
            : base(GetValues(values).Select(x => x.ToString()).ToList(), defaultValue.ToString(), callback => onValueChange?.Invoke(GetValues(values)[callback.index]))
        {
            Values = GetValues(values);
        }

        public new T Value => Values[Index];

        private static IReadOnlyList<T> GetValues(IReadOnlyList<T>? values) =>
            values ?? EnumUtils.Values<T>();
    }
}
