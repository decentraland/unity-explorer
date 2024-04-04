using DCL.DebugUtilities.Views;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    public class DebugContainerBuilder : IDebugContainerBuilder, IComparer<DebugWidgetBuilder>
    {
        private readonly List<DebugWidgetBuilder> widgets = new (100);

        private readonly Func<DebugWidget> widgetFactoryMethod;
        private readonly Func<DebugControl> controlFactoryMethod;
        private readonly Dictionary<Type, IDebugElementFactory> factories;

        public DebugContainer Container { get; private set; }

        public DebugContainerBuilder(
            Func<DebugWidget> widgetFactoryMethod,
            Func<DebugControl> controlFactoryMethod,
            Dictionary<Type, IDebugElementFactory> factories)
        {
            this.widgetFactoryMethod = widgetFactoryMethod;
            this.controlFactoryMethod = controlFactoryMethod;
            this.factories = factories;
        }

        public DebugWidgetBuilder AddWidget(string name)
        {
            var w = new DebugWidgetBuilder(name.ToUpper());
            widgets.Add(w);
            return w;
        }

        public DebugContainer Build(UIDocument debugRootCanvas)
        {
            // Sort by name
            widgets.Sort(this);

            Container = debugRootCanvas.rootVisualElement.Q<DebugContainer>();
            Container.Initialize();

            debugRootCanvas.rootVisualElement.Add(Container);

            // Instantiate widgets
            foreach (DebugWidgetBuilder widgetBuilder in widgets)
            {
                DebugWidget widget = widgetBuilder.Build(widgetFactoryMethod, controlFactoryMethod, factories);
                widget.name = widgetBuilder.name;
                Container.containerRoot.Add(widget);
            }

            Container.visible = false;
            return Container;
        }

        public int Compare(DebugWidgetBuilder x, DebugWidgetBuilder y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            return string.Compare(x.name, y.name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
