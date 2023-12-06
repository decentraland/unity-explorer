using System;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.UIBindings
{
    /// <summary>
    ///     Two-way way binding from a typed value to an element of the corresponding type
    /// </summary>
    public class ElementBinding<T> : IBinding
    {
        public event Action<T> OnValueChanged;

        private T tempValue;

        private bool tempValueIsDirty;

        private INotifyValueChanged<T> element;

        public T Value
        {
            get => tempValueIsDirty ? tempValue : element.value;

            set
            {
                tempValue = value;
                tempValueIsDirty = true;
            }
        }

        public ElementBinding(T defaultValue)
        {
            Value = defaultValue;
        }

        /// <summary>
        ///     Called from the Builder
        /// </summary>
        /// <param name="element"></param>
        internal void Connect(INotifyValueChanged<T> element)
        {
            this.element = element;
            this.element.value = Value;

            Update();
        }

        /// <summary>
        ///     Called from the builder, a shortcut to bind and start tracking the element
        /// </summary>
        internal void Connect<TElement>(TElement element) where TElement: class, INotifyValueChanged<T>, IBindable
        {
            element.binding = this;
            Connect((INotifyValueChanged<T>)element);
        }

        public void PreUpdate() { }

        public void Update()
        {
            if (tempValueIsDirty)
            {
                element.value = tempValue;
                OnValueChanged?.Invoke(element.value);
            }

            tempValueIsDirty = false;
        }

        public void Release()
        {
            element = null;
        }
    }
}
