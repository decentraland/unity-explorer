using UnityEngine.UIElements;
using Utility.Storage;

namespace DCL.DebugUtilities.UIBindings
{
    public class PersistentElementBinding<T> : IElementBinding<T>
    {
        private readonly ElementBinding<T> elementBinding;
        private readonly ISetting<T> setting; //cannot use generics here due capturing in the lambda on 19'th line

        public T Value => setting.Value;

        public PersistentElementBinding(ISetting<T> setting)
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
