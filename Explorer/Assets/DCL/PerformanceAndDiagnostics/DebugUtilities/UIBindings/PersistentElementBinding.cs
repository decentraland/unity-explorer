using UnityEngine.UIElements;
using Utility.Storage;

namespace DCL.DebugUtilities.UIBindings
{
    public class PersistentElementBinding<T> : IElementBinding<T>
    {
        private readonly ElementBinding<T> elementBinding;
        private readonly PersistentSetting<T> setting;

        public T Value => setting.Value;

        public PersistentElementBinding(PersistentSetting<T> setting)
        {
            this.setting = setting;

            elementBinding = new ElementBinding<T>(
                this.setting.Value,
                changeEvent => this.setting.Value = changeEvent.newValue
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
