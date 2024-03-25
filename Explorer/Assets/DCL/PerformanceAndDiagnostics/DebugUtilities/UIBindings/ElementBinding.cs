using System;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.UIBindings
{
    /// <summary>
    ///     Two-way way binding from a typed value to an element of the corresponding type
    /// </summary>
    public class ElementBinding<T> : IBinding
    {
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

        /// <summary>
        ///     Called when the value changes from the VisualElement
        /// </summary>
        public event EventCallback<ChangeEvent<T>> OnValueChanged;

        public ElementBinding(T defaultValue, EventCallback<ChangeEvent<T>> onValueChange = null)
        {
            Value = defaultValue;
            if (onValueChange != null) OnValueChanged += onValueChange;
        }

        /// <summary>
        ///     Called from the Builder
        /// </summary>
        /// <param name="element"></param>
        internal void Connect(INotifyValueChanged<T> element)
        {
            this.element = element;
            this.element.value = Value;

            this.element.RegisterValueChangedCallback(evt => OnValueChanged?.Invoke(evt));

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
                if (element == null)
                    throw new Exception("Element is not attached, did you forget to add it to the builder?");

                element.value = tempValue;
            }

            tempValueIsDirty = false;
        }

        public void SetAndUpdate(T value)
        {
            Value = value;
            Update();
        }

        public void Release()
        {
            element.UnregisterValueChangedCallback(OnValueChanged);
            element = null;
        }
    }
}
