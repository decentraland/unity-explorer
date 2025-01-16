using DCL.DebugUtilities.UIBindings;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    public class DebugList : INotifyValueChanged<IReadOnlyList<(string name, string value)>>
    {
        private readonly Func<DebugControl> controlFactoryMethod;
        private readonly IReadOnlyDictionary<Type, IDebugElementFactory> factories;
        private IReadOnlyList<(string name, string value)> currentValue = ArraySegment<(string name, string value)>.Empty;
        private readonly List<VisualItem> instantiatedVisualItems = new ();

        public DebugWidget Widget { get; }

        public IReadOnlyList<(string name, string value)> value
        {
            get => currentValue;
            set => SetValueWithoutNotify(value);
        }

        private DebugList(DebugWidget debugWidget, Func<DebugControl> controlFactoryMethod, IReadOnlyDictionary<Type, IDebugElementFactory> factories)
        {
            this.Widget = debugWidget;
            this.controlFactoryMethod = controlFactoryMethod;
            this.factories = factories;
        }

        public static DebugList NewDebugList(
            Func<DebugWidget> widgetFactoryMethod,
            Func<DebugControl> controlFactoryMethod,
            IReadOnlyDictionary<Type, IDebugElementFactory> factories,
            string listName,
            IElementBinding<IReadOnlyList<(string name, string value)>> list,
            string? foldKey = null
        )
        {
            var widget = widgetFactoryMethod();
            widget!.Initialize(listName, foldKey);

            var debugList = new DebugList(widget, controlFactoryMethod, factories);
            list.Connect(debugList);

            return debugList;
        }

        public void SetValueWithoutNotify(IReadOnlyList<(string name, string value)> newValue)
        {
            currentValue = newValue;

            InstantiateRemainingElements(newValue.Count);
            HideExtraVisualElements(newValue.Count);

            for (int i = 0; i < newValue.Count; i++)
            {
                var pair = newValue[i];
                instantiatedVisualItems[i].Show(pair.name, pair.value);
            }
        }

        private void InstantiateRemainingElements(int requiredCount)
        {
            int delta = requiredCount - instantiatedVisualItems.Count;

            for (int i = 0; i < delta; i++)
            {
                var item = VisualItem.NewVisualItem(controlFactoryMethod, factories);
                instantiatedVisualItems.Add(item);
                Widget.AddElement(item.Control);
            }
        }

        private void HideExtraVisualElements(int itemsCount)
        {
            for (int i = itemsCount; i < instantiatedVisualItems.Count; i++)
                instantiatedVisualItems[i].Hide();
        }

        private readonly struct VisualItem
        {
            private readonly ElementBinding<string> nameBinding;
            private readonly ElementBinding<string> valueBinding;

            public DebugControl Control { get; }

            public VisualItem(DebugControl control, ElementBinding<string> nameBinding, ElementBinding<string> valueBinding)
            {
                this.Control = control;
                this.nameBinding = nameBinding;
                this.valueBinding = valueBinding;
            }

            public void Show(string name, string value)
            {
                Control.visible = true;
                nameBinding.Value = name;
                valueBinding.Value = value;
            }

            public void Hide()
            {
                Control.visible = false;
            }

            public static VisualItem NewVisualItem(Func<DebugControl> controlFactoryMethod, IReadOnlyDictionary<Type, IDebugElementFactory> factories)
            {
                var nameBinding = new ElementBinding<string>(string.Empty);
                var valueBinding = new ElementBinding<string>(string.Empty);
                var control = DebugWidgetBuilder.CreateControl(controlFactoryMethod, factories, new DebugTextFieldDef(nameBinding), new DebugTextFieldDef(valueBinding));
                return new VisualItem(control, nameBinding, valueBinding);
            }
        }
    }
}
