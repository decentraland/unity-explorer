using System;
using UnityEngine.UIElements;
using Utility.Storage;

namespace DCL.DebugUtilities.UIBindings
{
    public class PersistentElementBinding<T> : IElementBinding<T>
    {
        private readonly ElementBinding<T> elementBinding;
        private readonly PersistentSetting<T> setting;

        public T Value => setting.Value;

        /// <summary>
        ///     Raised on the main thread after a UI edit has been written back to the persistent setting.
        ///     Lets consumers cache the value instead of reading the (main-thread-only) setting on a hot path.
        /// </summary>
        public event Action<T>? OnValueChanged;

        public PersistentElementBinding(PersistentSetting<T> setting)
        {
            this.setting = setting;

            elementBinding = new ElementBinding<T>(
                this.setting.Value,
                changeEvent =>
                {
                    this.setting.Value = changeEvent.newValue;
                    OnValueChanged?.Invoke(changeEvent.newValue);
                }
            );
        }

        public void Connect(INotifyValueChanged<T> element)
        {
            elementBinding.Connect(element);
        }

        public void PreUpdate()
        {
            elementBinding.PreUpdate();
        }

        public void Update()
        {
            elementBinding.Update();
        }

        public void Release()
        {
            elementBinding.Release();
        }
    }
}
