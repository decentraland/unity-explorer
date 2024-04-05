using UnityEngine.UIElements;
using Utility.Storage;

namespace DCL.DebugUtilities.UIBindings
{
    public class PersistentElementBinding<T> : IElementBinding<T>
    {
        private readonly ElementBinding<T> elementBinding;
        private readonly PersistentSetting<T> persistentSetting;

        public T Value => persistentSetting.Value;

        public PersistentElementBinding(PersistentSetting<T> persistentSetting)
        {
            this.persistentSetting = persistentSetting;

            elementBinding = new ElementBinding<T>(
                this.persistentSetting.Value,
                changeEvent => this.persistentSetting.Value = changeEvent.newValue
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
