using UnityEngine.UIElements;

namespace DCL.DebugUtilities.UIBindings
{
    public interface IElementBinding<T> : IBinding
    {
        T Value { get; }

        void Connect(INotifyValueChanged<T> element);
    }
}
