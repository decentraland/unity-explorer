using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.DebugUtilities.Views
{
    public abstract class DebugElementBase<TElement, TDef> : VisualElement where TElement: DebugElementBase<TElement, TDef> where TDef: IDebugElementDef
    {
        protected TDef definition { get; private set; }

        public void Initialize(TDef definition)
        {
            this.definition = definition;
            ConnectBindings();
        }

        protected virtual void ConnectBindings() { }

        public class Factory : IDebugElementFactory<TElement, TDef>
        {
            private readonly VisualTreeAsset asset;

            public Factory(VisualTreeAsset asset)
            {
                this.asset = asset;
            }

            public TElement Create(TDef def)
            {
                TElement el = asset.InstantiateForElement<TElement>();
                el.Initialize(def);
                return el;
            }
        }
    }
}
